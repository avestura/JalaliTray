using System;
using System.Threading;
using System.Windows;

namespace JalaliTray;

public static class Program
{
    public static AppSettings Settings { get; private set; } = new();
    public static TrayService Tray { get; private set; } = null!;
    public static EventService Events { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, args.Length > 0 ? @"Local\JalaliTray.Dev" : @"Local\JalaliTray.SingleInstance", out var created);
        if (!created) return;

        Settings = AppSettings.Load();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(app, Settings.Theme);

        Events = new EventService();
        Tray = new TrayService();
        Tray.Start();

        // Dev helper: JalaliTray --snapshot flyout|settings|converter <file.png> renders one window to a PNG and exits.
        if (args.Length == 3 && args[0] == "--snapshot")
        {
            app.Dispatcher.BeginInvoke(async () =>
            {
                CalendarWindow.KeepOpen = true;
                Window w = args[1] switch { "settings" => new SettingsWindow(), "converter" => new ConverterWindow(), _ => new CalendarWindow() };
                w.ShowActivated = false;
                w.Show();
                await System.Threading.Tasks.Task.Delay(1500);
                Snapshot.Save(w, args[2]);
                app.Shutdown();
            });
        }

        // Dev helper: JalaliTray --show flyout|settings|converter
        if (args.Length == 2 && args[0] == "--show")
        {
            app.Dispatcher.BeginInvoke(() =>
            {
                switch (args[1])
                {
                    case "flyout": Tray.OpenFlyout(); break;
                    case "settings": Tray.OpenSettings(); break;
                    case "converter": Tray.OpenConverter(); break;
                }
            });
        }

        app.Run();
        Tray.Dispose();
    }

    public static void ApplySettings(AppSettings s)
    {
        Settings = s;
        s.Save();
        ThemeService.Apply(Application.Current, s.Theme);
        StartupService.Set(s.StartWithWindows);
        Tray.Refresh(true);
    }
}
