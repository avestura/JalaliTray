using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JalaliTray;

public enum EventKind { Holiday, Religious, National, Other }

public sealed record DayEvent(string Title, bool IsHoliday, EventKind Kind);

public sealed record EventResult(IReadOnlyList<DayEvent> Events, bool FromApi);

public sealed class CacheEntry
{
    public DateTime FetchedUtc { get; set; }
    public List<DayEvent> Events { get; set; } = new();
}

/// <summary>
/// Events for a day: api.time.ir (needs the user's x-api-key) -> local cache -> bundled offline data.
/// </summary>
public sealed class EventService
{
    const string ApiBase = "https://api.time.ir";
    static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    readonly string _cachePath = Path.Combine(AppSettings.DataDir, "events-cache.json");
    readonly Dictionary<string, CacheEntry> _cache;

    public EventService()
    {
        try
        {
            _cache = File.Exists(_cachePath)
                ? JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(_cachePath)) ?? new()
                : new();
        }
        catch { _cache = new(); }
    }

    static string Key(DateTime d) => d.ToString("yyyy-MM-dd");

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

    /// <summary>Instant answer without network: cache if present, otherwise bundled data.</summary>
    public IReadOnlyList<DayEvent> GetCached(DateTime date) =>
        Filter(_cache.TryGetValue(Key(date), out var e) ? e.Events : BundledEvents.For(date));

    public bool IsHoliday(DateTime date) =>
        GetCached(date).Any(e => e.IsHoliday);

    public async Task<EventResult> GetAsync(DateTime date, CancellationToken ct)
    {
        var key = Key(date);
        _cache.TryGetValue(key, out var cached);
        if (cached != null && DateTime.UtcNow - cached.FetchedUtc < CacheTtl)
            return new EventResult(Filter(cached.Events), true);

        var apiKey = Program.Settings.ApiKey.Trim();
        if (apiKey.Length == 0)
            return new EventResult(Filter(BundledEvents.For(date)), false);

        try
        {
            var events = await FetchAsync(date, apiKey, ct);
            _cache[key] = new CacheEntry { FetchedUtc = DateTime.UtcNow, Events = events };
            SaveCache();
            return new EventResult(Filter(events), true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return cached != null
                ? new EventResult(Filter(cached.Events), true)
                : new EventResult(Filter(BundledEvents.For(date)), false);
        }
    }

    async Task<List<DayEvent>> FetchAsync(DateTime date, string apiKey, CancellationToken ct)
    {
        var j = CalendarService.ToJalali(date);
        var url = $"{ApiBase}/v1/event/fa/events/calendar?year={j.Year}&month={j.Month}&day={j.Day}&base1=0&base2=1&base3=2";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = FindProperty(doc.RootElement, "event_list")
                   ?? throw new InvalidDataException("event_list not found in response");

        var result = new List<DayEvent>();
        foreach (var el in list.EnumerateArray())
        {
            if (!el.TryGetProperty("title", out var t) || t.ValueKind != JsonValueKind.String) continue;
            var title = t.GetString()!.Trim();
            if (title.Length == 0 || result.Any(r => r.Title == title)) continue;
            var holiday = el.TryGetProperty("is_holiday", out var h) && h.ValueKind == JsonValueKind.True;
            result.Add(new DayEvent(title, holiday, holiday ? EventKind.Holiday : EventKind.Other));
        }
        return result;
    }

    static JsonElement? FindProperty(JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                if (p.Name == name && p.Value.ValueKind == JsonValueKind.Array) return p.Value;
                var inner = FindProperty(p.Value, name);
                if (inner != null) return inner;
            }
        }
        return null;
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

/// <summary>
/// Offline fallback. Solar dates are exact; lunar (Hijri) ones are approximate because the
/// Iranian moon-sighting calendar can differ by a day (use the Hijri adjustment setting).
/// </summary>
public static class BundledEvents
{
    sealed record Item(int Month, int Day, string Fa, string En, bool Holiday, EventKind Kind);

    const int LastDay = -1;

    static readonly Item[] Solar =
    {
        new(1, 1, "جشن نوروز / آغاز سال نو", "Nowruz", true, EventKind.National),
        new(1, 2, "تعطیلات نوروز", "Nowruz holiday", true, EventKind.National),
        new(1, 3, "تعطیلات نوروز", "Nowruz holiday", true, EventKind.National),
        new(1, 4, "تعطیلات نوروز", "Nowruz holiday", true, EventKind.National),
        new(1, 12, "روز جمهوری اسلامی", "Islamic Republic Day", true, EventKind.National),
        new(1, 13, "روز طبیعت (سیزده‌بدر)", "Nature Day (Sizdah Bedar)", true, EventKind.National),
        new(2, 12, "روز معلم", "Teacher's Day", false, EventKind.National),
        new(2, 25, "روز بزرگداشت فردوسی", "Ferdowsi Day", false, EventKind.National),
        new(2, 28, "روز بزرگداشت خیام", "Khayyam Day", false, EventKind.National),
        new(3, 14, "رحلت امام خمینی", "Demise of Imam Khomeini", true, EventKind.National),
        new(3, 15, "قیام ۱۵ خرداد", "15 Khordad Uprising", true, EventKind.National),
        new(11, 22, "پیروزی انقلاب اسلامی", "Islamic Revolution Victory Day", true, EventKind.National),
        new(12, 29, "ملی شدن صنعت نفت", "Oil Nationalization Day", true, EventKind.National),
    };

    static readonly Item[] Lunar =
    {
        new(1, 9, "تاسوعای حسینی", "Tasua", true, EventKind.Religious),
        new(1, 10, "عاشورای حسینی", "Ashura", true, EventKind.Religious),
        new(2, 20, "اربعین حسینی", "Arbaeen", true, EventKind.Religious),
        new(2, 28, "رحلت رسول اکرم و شهادت امام حسن مجتبی", "Demise of the Prophet & Imam Hassan", true, EventKind.Religious),
        new(2, LastDay, "شهادت امام رضا", "Martyrdom of Imam Reza", true, EventKind.Religious),
        new(3, 17, "میلاد رسول اکرم و امام جعفر صادق", "Birth of the Prophet & Imam Sadiq", true, EventKind.Religious),
        new(6, 3, "شهادت حضرت فاطمه", "Martyrdom of Fatima", true, EventKind.Religious),
        new(7, 13, "ولادت امام علی", "Birth of Imam Ali", true, EventKind.Religious),
        new(7, 27, "مبعث رسول اکرم", "Mab'ath", true, EventKind.Religious),
        new(8, 15, "ولادت حضرت قائم", "Mid-Sha'ban (Birth of Imam Mahdi)", true, EventKind.Religious),
        new(9, 21, "شهادت امام علی", "Martyrdom of Imam Ali", true, EventKind.Religious),
        new(10, 1, "عید سعید فطر", "Eid al-Fitr", true, EventKind.Religious),
        new(10, 2, "تعطیل به مناسبت عید فطر", "Eid al-Fitr holiday", true, EventKind.Religious),
        new(10, 25, "شهادت امام جعفر صادق", "Martyrdom of Imam Sadiq", true, EventKind.Religious),
        new(12, 10, "عید سعید قربان", "Eid al-Adha", true, EventKind.Religious),
        new(12, 18, "عید سعید غدیر خم", "Eid al-Ghadir", true, EventKind.Religious),
    };

    public static List<DayEvent> For(DateTime date)
    {
        bool fa = L.IsFa;
        var result = new List<DayEvent>();

        var j = CalendarService.ToJalali(date);
        foreach (var i in Solar.Where(i => i.Month == j.Month && i.Day == j.Day))
            result.Add(new DayEvent(fa ? i.Fa : i.En, i.Holiday, i.Kind));

        int off = Program.Settings.HijriOffset;
        var h = CalendarService.ToHijri(date, off);
        bool isLast = CalendarService.ToHijri(date.AddDays(1), off).Day == 1;
        foreach (var i in Lunar.Where(i => i.Month == h.Month && (i.Day == h.Day || (i.Day == LastDay && isLast))))
            result.Add(new DayEvent(fa ? i.Fa : i.En, i.Holiday, i.Kind));

        return result;
    }
}
