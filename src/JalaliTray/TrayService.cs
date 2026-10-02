using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;

namespace JalaliTray;

public sealed class TrayService : IDisposable
{
    NotifyIcon _notify = null!;
    DispatcherTimer _timer = null!;
    CalendarWindow? _flyout;
    SettingsWindow? _settings;
    DateTime _lastClosedUtc = DateTime.MinValue;
    DateTime _renderedDay = DateTime.MinValue;
    DateTime _lastNtpAttemptUtc = DateTime.MinValue;
    bool _ntpBusy;
    Icon? _icon;

    public void Start()
    {
        _notify = new NotifyIcon { Visible = true };
        _notify.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleFlyout();
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        SystemEvents.TimeChanged += (_, _) => OnUi(() => Refresh(true));
        SystemEvents.UserPreferenceChanged += (_, _) => OnUi(() => Refresh(true));
        SystemEvents.PowerModeChanged += (_, e) =>
        {
            if (e.Mode == PowerModes.Resume) OnUi(() => { _lastNtpAttemptUtc = DateTime.MinValue; Refresh(true); });
        };

        Refresh(true);
        Tick();
    }

    static void OnUi(Action a) => System.Windows.Application.Current.Dispatcher.BeginInvoke(a);

    void Tick()
    {
        if (TimeService.Today != _renderedDay) Refresh(false);
        _ = MaybeSyncNtpAsync();
    }

    async System.Threading.Tasks.Task MaybeSyncNtpAsync()
    {
        var s = Program.Settings;
        if (!s.NtpEnabled)
        {
            if (TimeService.Offset != TimeSpan.Zero) { TimeService.Reset(); Refresh(true); }
            return;
        }
        if (_ntpBusy) return;

        // Retry after 1 minute on failure, otherwise honour the configured interval.
        var due = TimeService.LastSyncUtc is { } last
            ? last + TimeSpan.FromHours(Math.Max(1, s.NtpIntervalHours))
            : _lastNtpAttemptUtc + TimeSpan.FromMinutes(1);
        if (DateTime.UtcNow < due) return;

        _ntpBusy = true;
        _lastNtpAttemptUtc = DateTime.UtcNow;
        try
        {
            if (await TimeService.SyncAsync(s.NtpServer)) Refresh(true);
        }
        finally { _ntpBusy = false; }
    }

    /// <summary>Re-renders the icon, tooltip and menu.</summary>
    public void Refresh(bool force)
    {
        var s = Program.Settings;
        var now = TimeService.Now;
        _renderedDay = now.Date;

        var j = CalendarService.ToJalali(now);
        var size = SystemInformation.SmallIconSize.Width;
        var old = _icon;
        try
        {
            _icon = IconRenderer.Render(L.Num(j.Day), ParseColor(s.IconBackground, Color.FromArgb(0x1E, 0x6F, 0xD9)),
                ParseColor(s.IconForeground, Color.White), s.IconFont, s.IconBold, size);
            _notify.Icon = _icon;
            old?.Dispose();
        }
        catch { _icon = old; }

        var tip = $"{L.JalaliLong(now)}\n{L.GregorianLong(now)}";
        if (s.ShowHijri) tip += $"\n{L.HijriLong(now)}";
        _notify.Text = tip.Length > 127 ? tip[..127] : tip;

        _notify.ContextMenuStrip?.Dispose();
        var menu = new ContextMenuStrip { RightToLeft = L.IsFa ? RightToLeft.Yes : RightToLeft.No };
        menu.Items.Add(L.T("menu.open"), null, (_, _) => OpenFlyout());
        menu.Items.Add(L.T("menu.settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("menu.exit"), null, (_, _) => System.Windows.Application.Current.Shutdown());
        _notify.ContextMenuStrip = menu;
    }

    public static Color ParseColor(string hex, Color fallback)
    {
        try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
    }

    void ToggleFlyout()
    {
        if (_flyout != null) { _flyout.Close(); return; }
        // Clicking the tray icon deactivates (and closes) the flyout before this handler runs;
        // without this guard it would instantly reopen.
        if ((DateTime.UtcNow - _lastClosedUtc).TotalMilliseconds < 400) return;
        OpenFlyout();
    }

    public void OpenFlyout()
    {
        if (_flyout != null) return;
        var w = new CalendarWindow();
        _flyout = w;
        w.Closed += (_, _) => { _flyout = null; _lastClosedUtc = DateTime.UtcNow; };
        w.Show();
        w.Activate();
    }

    public void OpenSettings()
    {
        _flyout?.Close();
        if (_settings != null) { _settings.Activate(); return; }
        var w = new SettingsWindow();
        _settings = w;
        w.Closed += (_, _) => _settings = null;
        w.Show();
        w.Activate();
    }

    public void Dispose()
    {
        _timer.Stop();
        _notify.Visible = false;
        _notify.Dispose();
        _icon?.Dispose();
    }
}
