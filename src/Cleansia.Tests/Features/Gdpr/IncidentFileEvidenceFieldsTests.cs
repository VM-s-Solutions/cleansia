using Cleansia.Core.AppServices.Features.Gdpr;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The evidence table of a trail entry (Q-AUD-L6 ruling): an audit payload flattened to key/value
/// pairs in declaration order, a currency id read as its code, enums left as the names the pipeline
/// already serialised them under. Pure logic, red-first.
/// </summary>
public sealed class IncidentFileEvidenceFieldsTests
{
    private static readonly IReadOnlyDictionary<string, string> Currencies = new Dictionary<string, string>
    {
        ["01HZCZK000000000000000000"] = "CZK",
    };

    [Fact]
    public void Nested_Objects_Flatten_To_Dotted_Keys_In_Declaration_Order()
    {
        const string json = """{"tier":"gold","feeRate":0.5,"policyFigures":{"freeHours":24,"partialRate":0.5},"refundInitiated":true}""";

        var fields = IncidentFileEvidenceFields.Flatten(json, Currencies);

        Assert.Equal(
            ["tier", "feeRate", "policyFigures.freeHours", "policyFigures.partialRate", "refundInitiated"],
            fields.Select(f => f.Key).ToArray());
        Assert.Equal(["gold", "0.5", "24", "0.5", "true"], fields.Select(f => f.Value).ToArray());
    }

    [Fact]
    public void A_Currency_Id_Reads_As_Its_Code_And_An_Unknown_Id_Stays_As_Written()
    {
        const string json = """{"currencyId":"01HZCZK000000000000000000","before":{"currencyId":"unknown-id"},"promoCodeId":"01HZCZK000000000000000000"}""";

        var fields = IncidentFileEvidenceFields.Flatten(json, Currencies);

        Assert.Equal("CZK", fields.Single(f => f.Key == "currencyId").Value);
        Assert.Equal("unknown-id", fields.Single(f => f.Key == "before.currencyId").Value);
        // Only a currency-id key is resolved; the same string under another name is another id.
        Assert.Equal("01HZCZK000000000000000000", fields.Single(f => f.Key == "promoCodeId").Value);
    }

    [Fact]
    public void Arrays_Join_And_Null_Reads_As_A_Dash()
    {
        const string json = """{"packageIds":["p-1","p-2"],"serviceIds":[],"promoCodeId":null,"isGuest":false}""";

        var fields = IncidentFileEvidenceFields.Flatten(json, Currencies);

        Assert.Equal("p-1, p-2", fields.Single(f => f.Key == "packageIds").Value);
        Assert.Equal(string.Empty, fields.Single(f => f.Key == "serviceIds").Value);
        Assert.Equal("—", fields.Single(f => f.Key == "promoCodeId").Value);
        Assert.Equal("false", fields.Single(f => f.Key == "isGuest").Value);
    }

    [Fact]
    public void A_Prefix_Names_The_Side_Of_An_Admin_Snapshot()
    {
        var fields = IncidentFileEvidenceFields.Flatten("""{"status":"Completed"}""", Currencies, "after");

        var field = Assert.Single(fields);
        Assert.Equal("after.status", field.Key);
        Assert.Equal("Completed", field.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_Payload_Is_No_Fields(string? json)
    {
        Assert.Empty(IncidentFileEvidenceFields.Flatten(json, Currencies));
    }
}
