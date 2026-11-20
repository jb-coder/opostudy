using System.Globalization;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Text encoding of the values stored in SQLite: ISO 8601 UTC dates and plain guids.</summary>
internal static class SqliteValues
{
    public static string ToText(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset ToDate(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static string ToText(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);
}