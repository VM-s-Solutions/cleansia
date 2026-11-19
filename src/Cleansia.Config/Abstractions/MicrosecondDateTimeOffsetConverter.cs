using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// The <see cref="DateTimeOffset"/> twin of <see cref="MicrosecondDateTimeConverter"/>. Npgsql stores the
/// UTC instant, so the truncation is taken on it: near the epoch the local time can sit on the other side.
/// → /architecture/backend#tolerant-json
/// </summary>
public sealed class MicrosecondDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.AddTicks(-MicrosecondDateTimeConverter.TicksBelowStoredMicrosecond(value.UtcTicks)));
}
