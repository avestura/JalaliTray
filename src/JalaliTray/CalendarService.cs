using System;
using System.Globalization;

namespace JalaliTray;

public readonly record struct JDate(int Year, int Month, int Day);

public static class CalendarService
{
    static readonly PersianCalendar Persian = new();
    static readonly HijriCalendar[] Hijri = new HijriCalendar[5];

    public static JDate ToJalali(DateTime d) =>
        new(Persian.GetYear(d), Persian.GetMonth(d), Persian.GetDayOfMonth(d));

    public static DateTime FromJalali(int y, int m, int d) =>
        Persian.ToDateTime(y, m, d, 0, 0, 0, 0);

    public static int DaysInMonth(int y, int m) => Persian.GetDaysInMonth(y, m);

    public static JDate ToHijri(DateTime d, int offset)
    {
        offset = Math.Clamp(offset, -2, 2);
        var cal = Hijri[offset + 2] ??= new HijriCalendar { HijriAdjustment = offset };
        return new JDate(cal.GetYear(d), cal.GetMonth(d), cal.GetDayOfMonth(d));
    }

    /// <summary>0 = Saturday ... 6 = Friday.</summary>
    public static int WeekdayIndex(DateTime d) => ((int)d.DayOfWeek + 1) % 7;
}
