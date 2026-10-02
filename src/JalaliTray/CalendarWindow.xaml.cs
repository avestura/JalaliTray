using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace JalaliTray;

public partial class CalendarWindow : Window
{
    int _viewYear, _viewMonth;
    DateTime _selected;
    DateTime _today;
    CancellationTokenSource? _cts;
    readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    bool _closing;

    public CalendarWindow()
    {
        InitializeComponent();
        FlowDirection = L.IsFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        _today = TimeService.Today;
        _selected = _today;
        var j = CalendarService.ToJalali(_today);
        (_viewYear, _viewMonth) = (j.Year, j.Month);

        for (int i = 0; i < 7; i++)
        {
            var tb = new TextBlock
            {
                Text = L.ShortWeekdaySatFirst(i),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 4),
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, i == 6 ? "Holiday" : "Muted");
            WeekHeader.Children.Add(tb);
        }

        // In RTL the "previous" button sits on the right, so its arrow points right.
        bool rtl = L.IsFa;
        PrevBtn.Content = rtl ? "▶" : "◀";
        NextBtn.Content = rtl ? "◀" : "▶";
        TodayBtn.Content = L.T("today");
        PrevBtn.Click += (_, _) => Navigate(-1);
        NextBtn.Click += (_, _) => Navigate(1);
        TodayBtn.Click += (_, _) => GoToday();
        SettingsBtn.Click += (_, _) => Program.Tray.OpenSettings();

        PreviewMouseWheel += (_, e) => { Navigate(e.Delta > 0 ? -1 : 1); e.Handled = true; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        Loaded += (_, _) => Position();
        Deactivated += (_, _) => { if (!_closing) Close(); };
        Closing += (_, _) => _closing = true;
        Closed += (_, _) => { _clock.Stop(); _cts?.Cancel(); };

        _clock.Tick += (_, _) => OnClock();
        _clock.Start();

        UpdateHeader();
        Render();
    }

    void Position()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var wa = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        double left = wa.Right / dpi.DpiScaleX - ActualWidth - 12;
        double top = wa.Bottom / dpi.DpiScaleY - ActualHeight - 12;
        Left = Math.Max(wa.Left / dpi.DpiScaleX, left);
        Top = Math.Max(wa.Top / dpi.DpiScaleY, top);
    }

    void OnClock()
    {
        UpdateHeader();
        var today = TimeService.Today;
        if (today != _today) { _today = today; Render(); }
    }

    void UpdateHeader()
    {
        var now = TimeService.Now;
        ClockText.Text = L.Num(now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        JalaliLine.Text = L.JalaliLong(now);
        GregLine.Text = L.GregorianLong(now);
        GregLine.Visibility = Program.Settings.ShowGregorian ? Visibility.Visible : Visibility.Collapsed;
        HijriLine.Text = L.HijriLong(now);
        HijriLine.Visibility = Program.Settings.ShowHijri ? Visibility.Visible : Visibility.Collapsed;
    }

    void Navigate(int delta)
    {
        _viewMonth += delta;
        if (_viewMonth > 12) { _viewMonth = 1; _viewYear++; }
        if (_viewMonth < 1) { _viewMonth = 12; _viewYear--; }
        Render();
    }

    void GoToday()
    {
        _today = TimeService.Today;
        _selected = _today;
        var j = CalendarService.ToJalali(_today);
        (_viewYear, _viewMonth) = (j.Year, j.Month);
        Render();
    }

    void Render()
    {
        MonthTitle.Text = $"{L.JMonth(_viewMonth)} {L.Num(_viewYear)}";
        DaysGrid.Children.Clear();

        var first = CalendarService.FromJalali(_viewYear, _viewMonth, 1);
        int lead = CalendarService.WeekdayIndex(first);
        int days = CalendarService.DaysInMonth(_viewYear, _viewMonth);

        for (int i = 0; i < 42; i++)
        {
            if (i < lead || i >= lead + days) DaysGrid.Children.Add(new Border());
            else DaysGrid.Children.Add(MakeCell(first.AddDays(i - lead), i - lead + 1));
        }
        LoadEvents();
    }

    UIElement MakeCell(DateTime date, int jalaliDay)
    {
        bool isToday = date == _today;
        bool isSelected = date == _selected;
        bool holiday = date.DayOfWeek == DayOfWeek.Friday || Program.Events.IsHoliday(date);
        var s = Program.Settings;

        var grid = new Grid { Height = 46 };
        var day = new TextBlock
        {
            Text = L.Num(jalaliDay),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        day.SetResourceReference(TextBlock.ForegroundProperty, isToday ? "OnAccent" : holiday ? "Holiday" : "Fg");
        grid.Children.Add(day);

        void Small(string text, HorizontalAlignment h)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 9, Margin = new Thickness(4, 0, 4, 2),
                HorizontalAlignment = h, VerticalAlignment = VerticalAlignment.Bottom,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, isToday ? "OnAccent" : "Muted");
            grid.Children.Add(tb);
        }
        if (s.ShowGregorian) Small(L.Num(date.Day), HorizontalAlignment.Left);
        if (s.ShowHijri) Small(L.Num(CalendarService.ToHijri(date, s.HijriOffset).Day), HorizontalAlignment.Right);

        var cell = new Border
        {
            Margin = new Thickness(1),
            BorderThickness = new Thickness(isSelected ? 2 : 0),
            Background = Brushes.Transparent,
            Child = grid,
            Cursor = Cursors.Hand,
        };
        if (isToday) cell.SetResourceReference(Border.BackgroundProperty, "Accent");
        if (isSelected) cell.SetResourceReference(Border.BorderBrushProperty, isToday ? "Fg" : "Accent");
        if (!isToday)
        {
            cell.MouseEnter += (_, _) => cell.SetResourceReference(Border.BackgroundProperty, "Hover");
            cell.MouseLeave += (_, _) => cell.Background = Brushes.Transparent;
        }
        cell.MouseLeftButtonUp += (_, _) => { _selected = date; Render(); };
        return cell;
    }

    async void LoadEvents()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var date = _selected;

        SelTitle.Text = L.JalaliLong(date) + (date == _today ? $"  ({L.T("today.paren")})" : "");
        ShowEvents(Program.Events.GetCached(date), null);

        try
        {
            var result = await Program.Events.GetAsync(date, ct);
            if (!ct.IsCancellationRequested) ShowEvents(result.Events, result.FromApi);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    void ShowEvents(System.Collections.Generic.IReadOnlyList<DayEvent> events, bool? fromApi)
    {
        EventList.Children.Clear();
        if (events.Count == 0)
        {
            var none = new TextBlock { Text = L.T("no.events"), FontSize = 12 };
            none.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            EventList.Children.Add(none);
        }
        foreach (var e in events)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = true };
            var dot = new Ellipse { Width = 6, Height = 6, Margin = new Thickness(0, 6, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            dot.SetResourceReference(Shape.FillProperty, e.IsHoliday ? "Holiday" : "Accent");
            DockPanel.SetDock(dot, Dock.Left);
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = e.Title, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            EventList.Children.Add(row);
        }

        var parts = new System.Collections.Generic.List<string>();
        if (fromApi != null) parts.Add(L.T(fromApi.Value ? "src.api" : "src.offline"));
        if (Program.Settings.NtpEnabled && TimeService.LastSyncUtc != null)
            parts.Add($"{L.T("ntp.synced")}: {Program.Settings.NtpServer}");
        SourceText.Text = string.Join("  ·  ", parts);
    }
}
