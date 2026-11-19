using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// Writes a <see cref="DateTime"/> truncated to whole microseconds, the precision Postgres stores and
/// Npgsql truncates to on write, so a reply built before the commit equals every later read of the same
/// value. The format is otherwise the default one; reading is unchanged.
/// → /architecture/backend#tolerant-json
/// </summary>
public sealed class MicrosecondDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond)));
}
