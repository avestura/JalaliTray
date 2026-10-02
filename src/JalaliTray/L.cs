using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Markup;

namespace JalaliTray;

/// <summary>Tiny Persian/English string table plus date formatting helpers.</summary>
public static class L
{
    public static bool IsFa => Program.Settings.Language != "en";

    static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        ["app.name"] = ("تقویم جلالی", "Jalali Tray"),
        ["menu.open"] = ("باز کردن تقویم", "Open calendar"),
        ["menu.settings"] = ("تنظیمات", "Settings"),
        ["menu.exit"] = ("خروج", "Exit"),
        ["today"] = ("امروز", "Today"),
        ["today.paren"] = ("امروز", "today"),
        ["no.events"] = ("مناسبتی برای این روز ثبت نشده", "No events"),
        ["src.api"] = ("منبع: time.ir", "Source: time.ir"),
        ["src.offline"] = ("منبع: داده آفلاین", "Source: offline data"),
        ["ntp.synced"] = ("ساعت NTP", "NTP clock"),

        ["settings.title"] = ("تنظیمات", "Settings"),
        ["sec.general"] = ("عمومی", "General"),
        ["language"] = ("زبان", "Language"),
        ["digits.persian"] = ("استفاده از ارقام فارسی", "Use Persian digits"),
        ["theme"] = ("تم", "Theme"),
        ["theme.system"] = ("مطابق ویندوز", "Follow Windows"),
        ["theme.light"] = ("روشن", "Light"),
        ["theme.dark"] = ("تیره", "Dark"),
        ["startup"] = ("اجرا همراه ویندوز", "Start with Windows"),
        ["sec.tray"] = ("آیکون نوار وظیفه", "Tray icon"),
        ["font"] = ("فونت", "Font"),
        ["bold"] = ("ضخیم", "Bold"),
        ["bgcolor"] = ("رنگ پس‌زمینه (HEX)", "Background (HEX)"),
        ["fgcolor"] = ("رنگ عدد (HEX)", "Number color (HEX)"),
        ["sec.time"] = ("زمان", "Time"),
        ["ntp.enable"] = ("ساعت برنامه را با سرور NTP تنظیم کن (ساعت ویندوز تغییر نمی‌کند)", "Use an NTP server instead of the Windows clock (this app only)"),
        ["ntp.server"] = ("سرور NTP", "NTP server"),
        ["ntp.interval"] = ("هر چند ساعت همگام شود", "Re-sync every (hours)"),
        ["ntp.test"] = ("آزمایش اتصال", "Test connection"),
        ["ntp.testing"] = ("در حال اتصال…", "Contacting server…"),
        ["ntp.offset"] = ("اختلاف با ساعت ویندوز", "Offset from Windows clock"),
        ["ntp.fail"] = ("ناموفق", "Failed"),
        ["sec.calendar"] = ("تقویم", "Calendar"),
        ["show.hijri"] = ("نمایش تاریخ قمری", "Show Hijri dates"),
        ["show.greg"] = ("نمایش تاریخ میلادی", "Show Gregorian dates"),
        ["hijri.offset"] = ("تعدیل تاریخ قمری (روز)", "Hijri adjustment (days)"),
        ["sec.events"] = ("رویدادها", "Events"),
        ["api.key"] = ("کلید API سایت time.ir", "time.ir API key"),
        ["api.hint"] = ("بدون کلید، از داده آفلاین داخلی استفاده می‌شود.", "Without a key, built-in offline data is used."),
        ["ev.holiday"] = ("تعطیلات رسمی", "Official holidays"),
        ["ev.religious"] = ("مناسبت‌های مذهبی", "Religious occasions"),
        ["ev.national"] = ("مناسبت‌های ملی", "National occasions"),
        ["ev.other"] = ("سایر مناسبت‌ها", "Other occasions"),
        ["save"] = ("ذخیره", "Save"),
        ["cancel"] = ("انصراف", "Cancel"),
        ["bad.color"] = ("رنگ واردشده معتبر نیست.", "That color is not valid."),
    };

    public static string T(string key) =>
        Table.TryGetValue(key, out var v) ? (IsFa ? v.Fa : v.En) : key;

    static readonly string[] JMonthFa = { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
    static readonly string[] JMonthEn = { "Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar", "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand" };
    static readonly string[] GMonthFa = { "ژانویه", "فوریه", "مارس", "آوریل", "مه", "ژوئن", "ژوئیه", "اوت", "سپتامبر", "اکتبر", "نوامبر", "دسامبر" };
    static readonly string[] HMonthFa = { "محرم", "صفر", "ربیع‌الاول", "ربیع‌الثانی", "جمادی‌الاول", "جمادی‌الثانی", "رجب", "شعبان", "رمضان", "شوال", "ذی‌القعده", "ذی‌الحجه" };
    static readonly string[] HMonthEn = { "Muharram", "Safar", "Rabi' I", "Rabi' II", "Jumada I", "Jumada II", "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah" };
    static readonly string[] DowFa = { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };
    static readonly string[] DowEn = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
    static readonly string[] ShortSatFa = { "ش", "ی", "د", "س", "چ", "پ", "ج" };
    static readonly string[] ShortSatEn = { "Sa", "Su", "Mo", "Tu", "We", "Th", "Fr" };

    public static string JMonth(int m) => (IsFa ? JMonthFa : JMonthEn)[m - 1];
    public static string HMonth(int m) => (IsFa ? HMonthFa : HMonthEn)[m - 1];
    public static string GMonth(int m) => IsFa ? GMonthFa[m - 1] : CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m);
    public static string Weekday(DateTime d) => (IsFa ? DowFa : DowEn)[(int)d.DayOfWeek];
    public static string ShortWeekdaySatFirst(int i) => (IsFa ? ShortSatFa : ShortSatEn)[i];

    // ---- number + date formatting ------------------------------------------------

    public static string Num(string s)
    {
        if (!Program.Settings.PersianDigits) return s;
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] is >= '0' and <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
        return new string(chars);
    }

    public static string Num(int n) => Num(n.ToString(CultureInfo.InvariantCulture));

    public static string JalaliLong(DateTime d)
    {
        var j = CalendarService.ToJalali(d);
        return $"{Weekday(d)} {Num(j.Day)} {JMonth(j.Month)} {Num(j.Year)}";
    }

    public static string GregorianLong(DateTime d) =>
        IsFa ? $"{Num(d.Day)} {GMonth(d.Month)} {Num(d.Year)}"
             : $"{Weekday(d)}, {d.Day} {GMonth(d.Month)} {d.Year}";

    public static string HijriLong(DateTime d)
    {
        var h = CalendarService.ToHijri(d, Program.Settings.HijriOffset);
        return $"{Num(h.Day)} {HMonth(h.Month)} {Num(h.Year)}";
    }
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public LocExtension() { }
    public LocExtension(string key) => Key = key;
    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
