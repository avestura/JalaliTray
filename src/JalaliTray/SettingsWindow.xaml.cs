using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JalaliTray;

public partial class SettingsWindow : Window
{
    readonly AppSettings _copy = Program.Settings.Clone();

    public SettingsWindow()
    {
        InitializeComponent();
        FlowDirection = L.IsFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        FontBox.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        HijriBox.ItemsSource = new[] { -2, -1, 0, 1, 2 };
        DataContext = _copy;
        PlaceFooter();

        SaveBtn.Click += (_, _) => Save();
        CancelBtn.Click += (_, _) => Close();
        TestBtn.Click += async (_, _) => await TestNtp();
    }

    // Save is always physically left of Cancel. The pair hugs the start edge of the window
    // (left in RTL, right in LTR) like standard Windows dialogs.
    void PlaceFooter()
    {
        var cols = Footer.ColumnDefinitions;
        if (L.IsFa)
        {
            cols[0].Width = GridLength.Auto; cols[1].Width = GridLength.Auto; cols[2].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(SaveBtn, 0); Grid.SetColumn(CancelBtn, 1);
        }
        else
        {
            cols[0].Width = new GridLength(1, GridUnitType.Star); cols[1].Width = GridLength.Auto; cols[2].Width = GridLength.Auto;
            Grid.SetColumn(SaveBtn, 1); Grid.SetColumn(CancelBtn, 2);
        }
    }

    void Save()
    {
        foreach (var hex in new[] { _copy.IconBackground, _copy.IconForeground })
        {
            try { System.Windows.Media.ColorConverter.ConvertFromString(hex); }
            catch
            {
                MessageBox.Show(this, L.T("bad.color"), L.T("settings.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        _copy.NtpServer = string.IsNullOrWhiteSpace(_copy.NtpServer) ? "ntp.time.ir" : _copy.NtpServer.Trim();
        _copy.NtpIntervalHours = Math.Clamp(_copy.NtpIntervalHours, 1, 168);
        _copy.HijriOffset = Math.Clamp(_copy.HijriOffset, -2, 2);
        if (string.IsNullOrWhiteSpace(_copy.IconFont)) _copy.IconFont = "Segoe UI";

        // Force a fresh NTP sync against the (possibly changed) server.
        TimeService.Reset();
        Program.ApplySettings(_copy);
        Close();
    }

    async System.Threading.Tasks.Task TestNtp()
    {
        TestBtn.IsEnabled = false;
        TestResult.Text = L.T("ntp.testing");
        try
        {
            var offset = await NtpClient.GetOffsetAsync(_copy.NtpServer.Trim());
            TestResult.Text = $"{L.T("ntp.offset")}: {L.Num(offset.TotalMilliseconds.ToString("+0;-0;0"))} ms";
        }
        catch (Exception ex)
        {
            TestResult.Text = $"{L.T("ntp.fail")}: {ex.Message}";
        }
        finally { TestBtn.IsEnabled = true; }
    }
}
