using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// Writes a <see cref="DateTime"/> at whole microseconds exactly as Npgsql stores it: counted from
/// Postgres's 2000-01-01 epoch and truncated toward it, so a reply built before the commit equals every
/// later read of the same value. The format is otherwise the default one; reading is unchanged.
/// → /architecture/backend#tolerant-json
/// </summary>
public sealed class MicrosecondDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    private const long PostgresEpochTicks = 630822816000000000; // 2000-01-01T00:00:00

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.AddTicks(-TicksBelowStoredMicrosecond(value.Ticks)));

    // Negative before 2000, so the value moves up toward the epoch, as Npgsql's integer division does.
    internal static long TicksBelowStoredMicrosecond(long ticks) =>
        (ticks - PostgresEpochTicks) % TimeSpan.TicksPerMicrosecond;
}
