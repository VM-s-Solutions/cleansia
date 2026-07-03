using System.Text.Json;
using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Features.Users;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The generated Swift client encodes <c>DateOnly</c> command fields as full ISO date-times
/// ("1990-05-01T00:00:00.000Z"), which the default System.Text.Json <c>DateOnly</c> handling
/// rejects with a 400 before the command reaches its validator. These tests run against the
/// REAL host serializer configuration (<see cref="CleansiaStartupBase.ConfigureJsonSerialization"/>)
/// so a dropped converter registration fails here, not on a device.
/// </summary>
public class TolerantDateOnlyConverterTests
{
    private static readonly JsonSerializerOptions HostOptions = CreateHostOptions();

    private static JsonSerializerOptions CreateHostOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        CleansiaStartupBase.ConfigureJsonSerialization(options);
        return options;
    }

    private static UpdateCurrentUser.Command? DeserializeCommand(string birthDateJson)
    {
        var json =
            $$"""
              {"id":"u-1","firstName":"Jane","lastName":"Doe","phoneNumber":"+420123456789","birthDate":{{birthDateJson}},"languageCode":"en"}
              """;
        return JsonSerializer.Deserialize<UpdateCurrentUser.Command>(json, HostOptions);
    }

    [Fact]
    public void Deserializes_BirthDate_From_Date_Only_String()
    {
        var command = DeserializeCommand("\"1990-05-01\"");

        Assert.Equal(new DateOnly(1990, 5, 1), command!.BirthDate);
    }

    [Theory]
    [InlineData("1990-05-01T00:00:00.000Z")]
    [InlineData("1990-05-01T00:00:00Z")]
    [InlineData("1990-05-01T22:30:00+02:00")]
    [InlineData("1990-05-01T14:32:11.1234567Z")]
    public void Deserializes_BirthDate_From_Iso_DateTime_By_Truncating_The_Time(string wireValue)
    {
        var command = DeserializeCommand($"\"{wireValue}\"");

        Assert.Equal(new DateOnly(1990, 5, 1), command!.BirthDate);
    }

    [Fact]
    public void Deserializes_Null_BirthDate()
    {
        var command = DeserializeCommand("null");

        Assert.Null(command!.BirthDate);
    }

    [Theory]
    [InlineData("\"not-a-date\"")]
    [InlineData("\"1990-05-01Tgarbage\"")]
    [InlineData("\"05/01/1990\"")]
    [InlineData("\"\"")]
    [InlineData("12345")]
    public void Rejects_Garbage_BirthDate(string birthDateJson)
    {
        Assert.Throws<JsonException>(() => DeserializeCommand(birthDateJson));
    }

    [Fact]
    public void Still_Writes_DateOnly_As_Date_Only_On_The_Wire()
    {
        var command = new UpdateCurrentUser.Command(
            Id: "u-1",
            FirstName: "Jane",
            LastName: "Doe",
            PhoneNumber: "+420123456789",
            BirthDate: new DateOnly(1990, 5, 1),
            Photo: null,
            LanguageCode: "en");

        var json = JsonSerializer.Serialize(command, HostOptions);

        Assert.Contains("\"birthDate\":\"1990-05-01\"", json);
    }
}
