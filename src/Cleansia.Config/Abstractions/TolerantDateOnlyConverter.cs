using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// <see cref="DateOnly"/> JSON converter: tolerant on read, fixed on write. Accepts
/// <c>"yyyy-MM-dd"</c> and a full ISO date-time whose time part is truncated <b>literally — no
/// time-zone conversion, the day is taken as the client wrote it.</b> Anything else still throws, so
/// garbage keeps producing a 400.
///
/// <para><b>Always WRITES <c>"yyyy-MM-dd"</c>, and that may not change</b> — the Angular and Kotlin
/// clients already parse it. The read leniency exists because the swift5 generator has no date-only
/// type and sends a full ISO date-time. → /architecture/backend#tolerant-json</para>
/// </summary>
public sealed class TolerantDateOnlyConverter : JsonConverter<DateOnly>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unexpected token {reader.TokenType} for DateOnly.");
        }

        var raw = reader.GetString();
        if (DateOnly.TryParseExact(raw, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (raw is { Length: > 10 } && raw[10] == 'T'
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && DateOnly.TryParseExact(raw.AsSpan(0, 10), DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var datePart))
        {
            return datePart;
        }

        throw new JsonException($"Cannot convert \"{raw}\" to DateOnly.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
