using System;

namespace Capstone.Domain;

public static class FarmClock
{
    public const string DefaultTimeZone = "Asia/Manila";

    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZone : timeZoneId;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception)
        {
            try
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
            }
            catch (Exception)
            {
            }

            return TimeZoneInfo.CreateCustomTimeZone(DefaultTimeZone, TimeSpan.FromHours(8), "Asia/Manila", "Asia/Manila");
        }
    }

    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ToUtc(utc), DateTimeKind.Utc), zone);
    }

    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone)
    {
        return DateOnly.FromDateTime(ToLocal(utc, zone));
    }

    public static DateOnly Today(TimeZoneInfo zone) => LocalDate(DateTime.UtcNow, zone);

    public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static (DateTime FromUtc, DateTime ToUtcExclusive) RangeUtc(DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        return (StartOfDayUtc(from, zone), StartOfDayUtc(to.AddDays(1), zone));
    }

    public static int DaysBetween(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber;
}
