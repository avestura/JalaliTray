using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JalaliTray;

/// <summary>
/// Two-way Jalali / Gregorian converter: edit either side and the other follows.
/// </summary>
public partial class ConverterWindow : Window
{
    bool _busy;
    DateTime _current;

    public ConverterWindow()
    {
        InitializeComponent();
        FlowDirection = L.IsFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        for (int m = 1; m <= 12; m++)
        {
            JMonth.Items.Add(L.JMonth(m));
            GMonth.Items.Add(L.GMonth(m));
        }

        TodayBtn.Content = L.T("today");
        NowruzBtn.Content = L.T("conv.nowruz");
        CopyBtn.Content = L.T("conv.copy");

        JYear.TextChanged += (_, _) => FromJalali();
        JDay.TextChanged += (_, _) => FromJalali();
        JMonth.SelectionChanged += (_, _) => FromJalali();
        GYear.TextChanged += (_, _) => FromGregorian();
        GDay.TextChanged += (_, _) => FromGregorian();
        GMonth.SelectionChanged += (_, _) => FromGregorian();

        TodayBtn.Click += (_, _) => SetDate(TimeService.Today);
        NowruzBtn.Click += (_, _) =>
        {
            int year = CalendarService.ToJalali(_current).Year;
            SetDate(CalendarService.FromJalali(year, 1, 1));
        };
        CopyBtn.Click += (_, _) => Copy();

        SetDate(TimeService.Today);
    }

    static int? ParseInt(string text)
    {
        var chars = text.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '۰' and <= '۹') chars[i] = (char)('0' + (chars[i] - '۰'));
            else if (chars[i] is >= '٠' and <= '٩') chars[i] = (char)('0' + (chars[i] - '٠'));
        }
        return int.TryParse(new string(chars), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    void FromJalali()
    {
        if (_busy) return;
        if (ParseInt(JYear.Text) is { } y && ParseInt(JDay.Text) is { } d && JMonth.SelectedIndex >= 0)
        {
            int m = JMonth.SelectedIndex + 1;
            if (y is >= 1 and <= 9377 && d >= 1 && d <= CalendarService.DaysInMonth(y, m))
            {
                _current = CalendarService.FromJalali(y, m, d);
                ShowResults(updateJalali: false);
                _busy = true;
                GYear.Text = L.Num(_current.Year);
                GMonth.SelectedIndex = _current.Month - 1;
                GDay.Text = L.Num(_current.Day);
                _busy = false;
                return;
            }
        }
        ShowInvalid(JResult);
    }

    void FromGregorian()
    {
        if (_busy) return;
        if (ParseInt(GYear.Text) is { } y && ParseInt(GDay.Text) is { } d && GMonth.SelectedIndex >= 0)
        {
            int m = GMonth.SelectedIndex + 1;
            // Persian calendar support starts on 622-03-22.
            if (y is >= 623 and <= 9999 && d >= 1 && d <= DateTime.DaysInMonth(y, m))
            {
                _current = new DateTime(y, m, d);
                ShowResults(updateJalali: true);
                var j = CalendarService.ToJalali(_current);
                _busy = true;
                JYear.Text = L.Num(j.Year);
                JMonth.SelectedIndex = j.Month - 1;
                JDay.Text = L.Num(j.Day);
                _busy = false;
                return;
            }
        }
        ShowInvalid(GResult);
    }

    void SetDate(DateTime date)
    {
        _current = date;
        var j = CalendarService.ToJalali(date);
        _busy = true;
        JYear.Text = L.Num(j.Year);
        JMonth.SelectedIndex = j.Month - 1;
        JDay.Text = L.Num(j.Day);
        GYear.Text = L.Num(date.Year);
        GMonth.SelectedIndex = date.Month - 1;
        GDay.Text = L.Num(date.Day);
        _busy = false;
        ShowResults(updateJalali: true);
    }

    void ShowResults(bool updateJalali)
    {
        _ = updateJalali;
        JResult.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        GResult.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        JResult.Text = L.JalaliLong(_current);
        GResult.Text = L.GregorianLong(_current);

        int diff = (_current.Date - TimeService.Today).Days;
        string relative = diff switch
        {
            0 => L.T("conv.today.is"),
            1 => L.T("conv.tomorrow"),
            -1 => L.T("conv.yesterday"),
            > 1 => string.Format(L.T("conv.in"), L.Num(diff)),
            _ => string.Format(L.T("conv.ago"), L.Num(-diff)),
        };
        Summary.Text = $"{L.T("conv.hijri")}: {L.HijriLong(_current)}   ·   {relative}";
    }

    void ShowInvalid(TextBlock card)
    {
        card.Text = L.T("conv.invalid");
        card.SetResourceReference(TextBlock.ForegroundProperty, "Holiday");
    }

    void Copy()
    {
        var j = CalendarService.ToJalali(_current);
        try
        {
            Clipboard.SetText(
                $"{L.JalaliLong(_current)}\n{L.GregorianLong(_current)}\n" +
                $"{j.Year:0000}/{j.Month:00}/{j.Day:00} = {_current:yyyy-MM-dd}");
        }
        catch { return; }

        CopyBtn.Content = L.T("conv.copied");
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        t.Tick += (_, _) => { t.Stop(); CopyBtn.Content = L.T("conv.copy"); };
        t.Start();
    }
}
