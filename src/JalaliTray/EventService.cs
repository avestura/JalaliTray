using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JalaliTray;

public sealed class MonthEntry
{
    public DateTime FetchedUtc { get; set; }

    /// <summary>Events keyed by Gregorian date (yyyy-MM-dd).</summary>
    public Dictionary<string, List<DayEvent>> Days { get; set; } = new();

    /// <summary>Jalali day numbers the calendar marks as holidays.</summary>
    public List<int> HolidayDays { get; set; } = new();
}

/// <summary>
/// Events come a month at a time from the public api.time.ir calendar endpoint
/// (day=0 returns the whole month and needs no key). Results are cached on disk,
/// and bundled offline data is used when the network is unavailable.
/// </summary>
public sealed class EventService
{
    const string ApiBase = "https://api.time.ir";
    static readonly TimeSpan CacheTtl = TimeSpan.FromDays(3);

    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    readonly string _cachePath = Path.Combine(AppSettings.DataDir, "events-cache-v2.json");
    readonly Dictionary<string, MonthEntry> _cache;
    readonly ConcurrentDictionary<string, Task<MonthEntry>> _inflight = new();

    public EventService()
    {
        try
        {
            _cache = File.Exists(_cachePath)
                ? JsonSerializer.Deserialize<Dictionary<string, MonthEntry>>(File.ReadAllText(_cachePath)) ?? new()
                : new();
        }
        catch { _cache = new(); }
    }

    static string MonthKey(JDate j) => $"{j.Year}-{j.Month:00}";
    static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd");

    MonthEntry? Lookup(DateTime date) =>
        _cache.TryGetValue(MonthKey(CalendarService.ToJalali(date)), out var e) ? e : null;

    IReadOnlyList<DayEvent> Filter(IEnumerable<DayEvent> events)
    {
        var s = Program.Settings;
        return events.Where(e => e.IsHoliday ? s.ShowHolidays : e.Kind switch
        {
            EventKind.Religious => s.ShowReligious,
            EventKind.National => s.ShowNational,
            _ => s.ShowOther,
        }).ToList();
    }

    /// <summary>Instant answer without network: cached month if present, otherwise bundled data.</summary>
    public IReadOnlyList<DayEvent> GetCached(DateTime date)
    {
        var month = Lookup(date);
        if (month == null) return Filter(BundledEvents.For(date));
        return Filter(month.Days.TryGetValue(DayKey(date), out var list) ? list : new List<DayEvent>());
    }

    public bool IsHoliday(DateTime date)
    {
        if (!Program.Settings.ShowHolidays) return false;
        var month = Lookup(date);
        if (month == null) return BundledEvents.For(date).Any(e => e.IsHoliday);
        return month.HolidayDays.Contains(CalendarService.ToJalali(date).Day)
               || (month.Days.TryGetValue(DayKey(date), out var l) && l.Any(e => e.IsHoliday));
    }

    /// <summary>True when the month for this date was cached from the API recently.</summary>
    public bool HasFreshMonth(DateTime date) =>
        Lookup(date) is { } e && DateTime.UtcNow - e.FetchedUtc < CacheTtl;

    /// <summary>Makes sure the month is cached. Returns true if API data (fresh or stale) is available.</summary>
    public async Task<bool> EnsureMonthAsync(DateTime date, CancellationToken ct)
    {
        if (HasFreshMonth(date)) return true;
        var j = CalendarService.ToJalali(date);
        var key = MonthKey(j);
        try
        {
            var task = _inflight.GetOrAdd(key, _ => FetchMonthAsync(j));
            _ = task.ContinueWith(_ => _inflight.TryRemove(key, out Task<MonthEntry>? _), TaskScheduler.Default);
            var entry = await task.WaitAsync(ct);
            _cache[key] = entry;
            SaveCache();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Lookup(date) != null;
        }
    }

    public async Task<EventResult> GetAsync(DateTime date, CancellationToken ct)
    {
        if (await EnsureMonthAsync(date, ct) && Lookup(date) is { } month)
        {
            var list = month.Days.TryGetValue(DayKey(date), out var l) ? l : new List<DayEvent>();
            return new EventResult(Filter(list), true);
        }
        return new EventResult(Filter(BundledEvents.For(date)), false);
    }

    async Task<MonthEntry> FetchMonthAsync(JDate j)
    {
        var url = $"{ApiBase}/v1/event/fa/events/calendar?year={j.Year}&month={j.Month}&day=0&base1=0&base2=1&base3=2";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");

        var entry = new MonthEntry { FetchedUtc = DateTime.UtcNow };

        foreach (var el in data.GetProperty("event_list").EnumerateArray())
        {
            var title = el.GetProperty("title").GetString()?.Trim();
            if (string.IsNullOrEmpty(title)) continue;

            var key = $"{el.GetProperty("gregorian_year").GetInt32():0000}-" +
                      $"{el.GetProperty("gregorian_month").GetInt32():00}-" +
                      $"{el.GetProperty("gregorian_day").GetInt32():00}";
            bool holiday = el.TryGetProperty("is_holiday", out var h) && h.ValueKind == JsonValueKind.True;
            int calendarBase = el.TryGetProperty("base", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetInt32() : 0;
            var kind = holiday ? EventKind.Holiday : calendarBase switch
            {
                0 => EventKind.National,   // Jalali-calendar occasions
                2 => EventKind.Religious,  // Hijri-calendar occasions
                _ => EventKind.Other,      // Gregorian / international
            };

            if (!entry.Days.TryGetValue(key, out var list)) entry.Days[key] = list = new List<DayEvent>();
            if (list.All(x => x.Title != title)) list.Add(new DayEvent(title, holiday, kind));
        }

        if (data.TryGetProperty("day_list", out var days))
        {
            foreach (var d in days.EnumerateArray())
            {
                bool enabled = d.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True;
                bool holiday = d.TryGetProperty("is_holiday", out var ih) && ih.ValueKind == JsonValueKind.True;
                if (enabled && holiday && d.TryGetProperty("index_in_base1", out var idx))
                    entry.HolidayDays.Add(idx.GetInt32());
            }
        }
        return entry;
    }

    void SaveCache()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(_cache));
        }
        catch { }
    }
}
