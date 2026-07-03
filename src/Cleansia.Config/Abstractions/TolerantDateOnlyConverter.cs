using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// JSON converter for <see cref="DateOnly"/> that is tolerant on the read
/// side but preserves the default date-only write behaviour every existing
/// web client relies on.
///
/// <para>
/// <b>Why:</b> the OpenAPI Generator's swift5 template has no date-only
/// type — it maps <c>format: date</c> to <c>Date</c> and serializes it as
/// a full ISO date-time (<c>"1990-05-01T00:00:00.000Z"</c>). Default
/// System.Text.Json <see cref="DateOnly"/> handling only accepts
/// <c>"yyyy-MM-dd"</c>, so payloads from the iOS apps fail with
/// "The JSON value could not be converted to System.DateOnly".
/// </para>
///
/// <para>
/// <b>What this converter accepts on read:</b> <c>"yyyy-MM-dd"</c>
/// (Android / web format) and a full ISO 8601 date-time, whose time part
/// is truncated literally (no time-zone conversion — the day is taken as
/// the client wrote it). Anything else still throws, so garbage keeps
/// producing a 400.
/// </para>
///
/// <para>
/// <b>What it writes:</b> always <c>"yyyy-MM-dd"</c>, matching the
/// System.Text.Json default the Angular and Kotlin clients already expect.
/// </para>
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
