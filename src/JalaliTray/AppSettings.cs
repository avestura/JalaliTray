using System;
using System.IO;
using System.Text.Json;

namespace JalaliTray;

public sealed class AppSettings
{
    // General
    public string Language { get; set; } = "fa";          // "fa" | "en"
    public bool PersianDigits { get; set; } = true;
    public string Theme { get; set; } = "System";         // "System" | "Light" | "Dark"
    public bool StartWithWindows { get; set; }

    // Tray icon
    public string IconFont { get; set; } = "Segoe UI";
    public bool IconBold { get; set; } = true;
    public string IconBackground { get; set; } = "#1E6FD9";
    public string IconForeground { get; set; } = "#FFFFFF";

    // Time
    public bool NtpEnabled { get; set; }
    public string NtpServer { get; set; } = "ntp.time.ir";
    public int NtpIntervalHours { get; set; } = 6;

    // Calendar
    public bool ShowHijri { get; set; } = true;
    public bool ShowGregorian { get; set; } = true;
    public int HijriOffset { get; set; }                   // -2..2 days

    // Events
    public string ApiKey { get; set; } = "";
    public bool ShowHolidays { get; set; } = true;
    public bool ShowReligious { get; set; } = true;
    public bool ShowNational { get; set; } = true;
    public bool ShowOther { get; set; } = true;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JalaliTray");

    static string FilePath => Path.Combine(DataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { /* corrupt file: fall back to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { /* read-only profile: keep running with in-memory settings */ }
    }

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
}
