using NodaTime;

namespace Salenix.Crypto;

/// <summary>IANA time zones from NodaTime's embedded TZDB (no dependency on the container's tzdata).</summary>
internal static class TimeZones
{
    public static DateTime ConvertUtcToZone(DateTime utc, string ianaZone)
    {
        var zone = Resolve(ianaZone);
        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        };
        return Instant.FromDateTimeUtc(asUtc).InZone(zone).LocalDateTime.ToDateTimeUnspecified();
    }

    public static DateTime TodayIn(string ianaZone, DateTime nowUtc) =>
        DateTime.SpecifyKind(ConvertUtcToZone(nowUtc, ianaZone).Date, DateTimeKind.Unspecified);

    private static DateTimeZone Resolve(string ianaZone)
    {
        var name = (ianaZone ?? "").Trim();
        var zone = name.Length == 0 ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(name);
        return zone ?? throw new SalenixCryptoException($"TIMEZONE_UNKNOWN: '{name}' is not an IANA time zone.");
    }
}
