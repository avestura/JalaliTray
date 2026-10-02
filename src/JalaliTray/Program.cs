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
    public static void Main()
    {
        using var mutex = new Mutex(true, @"Local\JalaliTray.SingleInstance", out var created);
        if (!created) return;

        Settings = AppSettings.Load();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(app, Settings.Theme);

        Events = new EventService();
        Tray = new TrayService();
        Tray.Start();

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
