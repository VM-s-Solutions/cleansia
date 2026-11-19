using System.Text.Json;
using Cleansia.Config.Abstractions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Runs against the REAL host serializer configuration so a dropped registration fails here. The API
/// writes timestamps at the precision Postgres stores (microseconds, the 7th tick digit truncated as
/// Npgsql does on write), so a reply built before the commit equals every later read of the same value.
/// </summary>
public class MicrosecondTimestampConverterTests
{
    private static readonly JsonSerializerOptions HostOptions = CreateHostOptions();
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web);

    private static JsonSerializerOptions CreateHostOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        CleansiaStartupBase.ConfigureJsonSerialization(options);
        return options;
    }

    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind);

    [Fact]
    public void Writes_The_Dev_Example_With_Its_Seventh_Digit_Truncated()
    {
        var json = JsonSerializer.Serialize(Utc("2026-11-18T13:32:51.1991616Z"), HostOptions);

        Assert.Equal("\"2026-11-18T13:32:51.199161Z\"", json);
    }

    [Fact]
    public void Truncates_Rather_Than_Rounds()
    {
        var json = JsonSerializer.Serialize(Utc("2026-11-18T13:32:51.9999999Z"), HostOptions);

        Assert.Equal("\"2026-11-18T13:32:51.999999Z\"", json);
    }

    // Npgsql counts microseconds from 2000-01-01 and truncates toward it, so before 2000 the 7th digit
    // rounds UP to the next microsecond. Values taken from Npgsql 10.0.1's own PgTimestamp.Encode.
    [Theory]
    [InlineData("1999-12-31T23:59:59.1991616Z", "1999-12-31T23:59:59.199162Z")]
    [InlineData("1990-05-12T10:00:00.0000005Z", "1990-05-12T10:00:00.000001Z")]
    [InlineData("2000-01-01T00:00:00.0000005Z", "2000-01-01T00:00:00Z")]
    public void Matches_What_Npgsql_Stores_On_Both_Sides_Of_Its_2000_Epoch(string sent, string stored)
    {
        Assert.Equal($"\"{stored}\"", JsonSerializer.Serialize(Utc(sent), HostOptions));
    }

    [Fact]
    public void Writes_A_Pre_2000_DateTimeOffset_As_Npgsql_Stores_It()
    {
        var value = DateTimeOffset.Parse("2000-01-01T00:59:59.1991616+01:00");

        Assert.Equal("\"2000-01-01T00:59:59.199162+01:00\"", JsonSerializer.Serialize(value, HostOptions));
    }

    [Theory]
    [InlineData("2026-11-18T13:32:51.199161Z")]
    [InlineData("2026-11-18T13:32:51.1991Z")]
    [InlineData("2026-11-18T13:32:51Z")]
    public void Leaves_Six_Or_Fewer_Digits_Unchanged(string iso)
    {
        Assert.Equal($"\"{iso}\"", JsonSerializer.Serialize(Utc(iso), HostOptions));
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void Keeps_The_Default_Format_For_Every_Kind(DateTimeKind kind)
    {
        var value = new DateTime(2026, 11, 18, 13, 32, 51, kind).AddTicks(1991616);
        var stored = new DateTime(2026, 11, 18, 13, 32, 51, kind).AddTicks(1991610);

        Assert.Equal(
            JsonSerializer.Serialize(stored, DefaultOptions),
            JsonSerializer.Serialize(value, HostOptions));
        Assert.Contains(".199161", JsonSerializer.Serialize(value, HostOptions));
    }

    [Fact]
    public void Writes_A_DateTimeOffset_Truncated_With_Its_Offset()
    {
        var value = DateTimeOffset.Parse("2026-11-18T14:32:51.1991616+01:00");

        Assert.Equal("\"2026-11-18T14:32:51.199161+01:00\"", JsonSerializer.Serialize(value, HostOptions));
    }

    private sealed record Stamps(DateTime? At, DateTimeOffset? AtOffset);

    [Fact]
    public void Writes_Nullable_Timestamps_Truncated_And_Nulls_As_Null()
    {
        var json = JsonSerializer.Serialize(
            new[]
            {
                new Stamps(Utc("2026-11-18T13:32:51.1991616Z"), DateTimeOffset.Parse("2026-11-18T13:32:51.1991616Z")),
                new Stamps(null, null),
            },
            HostOptions);

        Assert.Equal(
            "[{\"at\":\"2026-11-18T13:32:51.199161Z\",\"atOffset\":\"2026-11-18T13:32:51.199161+00:00\"},"
            + "{\"at\":null,\"atOffset\":null}]",
            json);
    }

    [Fact]
    public void Reads_Seven_Digits_Unchanged()
    {
        var read = JsonSerializer.Deserialize<Stamps>(
            "{\"at\":\"2026-11-18T13:32:51.1991616Z\",\"atOffset\":\"2026-11-18T13:32:51.1991616+01:00\"}",
            HostOptions);

        Assert.Equal(Utc("2026-11-18T13:32:51.1991616Z"), read!.At);
        Assert.Equal(DateTimeKind.Utc, read.At!.Value.Kind);
        Assert.Equal(DateTimeOffset.Parse("2026-11-18T13:32:51.1991616+01:00"), read.AtOffset);
        Assert.Equal(1991616, read.AtOffset!.Value.Ticks % TimeSpan.TicksPerSecond);
    }
}
