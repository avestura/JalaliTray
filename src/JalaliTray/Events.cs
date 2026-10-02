using System;
using System.Collections.Generic;
using System.Linq;

namespace JalaliTray;

public enum EventKind { Holiday, Religious, National, Other }

public sealed record DayEvent(string Title, bool IsHoliday, EventKind Kind);

public sealed record EventResult(IReadOnlyList<DayEvent> Events, bool FromApi);

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
