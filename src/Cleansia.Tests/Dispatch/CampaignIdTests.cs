using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// ADR-0002 verify #6 (TC-KEY-0 shape) for the sitewide-promo campaign identity. A campaign's
/// <c>CampaignId</c> is a PURE function of its domain inputs (tenant + the per-locale title/body the
/// admin authored) -- two invocations the producer intends as the SAME campaign emit the SAME id, with
/// no Guid.NewGuid()/timestamp that would defeat the producer dedup and the consumer resume cursor. Two
/// distinct campaigns hash differently.
/// </summary>
public class CampaignIdTests
{
    private static Dictionary<string, string> Titles() => new()
    {
        ["en"] = "Spring sale", ["cs"] = "Jarni vyprodej", ["sk"] = "Jarny vypredaj",
        ["uk"] = "Vesnyanyy rozprodazh", ["ru"] = "Vesennyaya rasprodazha",
    };

    private static Dictionary<string, string> Bodies() => new()
    {
        ["en"] = "20% off", ["cs"] = "Sleva 20 %", ["sk"] = "Zlava 20 %",
        ["uk"] = "Znyzhka 20%", ["ru"] = "Skidka 20%",
    };

    [Fact]
    public void CampaignId_Is_Deterministic_For_Same_Inputs()
    {
        var first = SendSitewidePromoMessage.DeriveCampaignId("TENANT-A", Titles(), Bodies());
        var second = SendSitewidePromoMessage.DeriveCampaignId("TENANT-A", Titles(), Bodies());

        Assert.Equal(first, second);
    }

    [Fact]
    public void CampaignId_Follows_The_Promo_Key_Shape()
    {
        var campaignId = SendSitewidePromoMessage.DeriveCampaignId("TENANT-A", Titles(), Bodies());

        Assert.StartsWith("promo:TENANT-A:", campaignId);
    }

    [Fact]
    public void CampaignId_Differs_For_Different_Content()
    {
        var bodies = Bodies();
        var other = new Dictionary<string, string>(bodies) { ["en"] = "50% off" };

        Assert.NotEqual(
            SendSitewidePromoMessage.DeriveCampaignId("TENANT-A", Titles(), bodies),
            SendSitewidePromoMessage.DeriveCampaignId("TENANT-A", Titles(), other));
    }

    [Fact]
    public void CampaignId_Differs_For_Different_Tenant()
    {
        Assert.NotEqual(
            SendSitewidePromoMessage.DeriveCampaignId("TENANT-A", Titles(), Bodies()),
            SendSitewidePromoMessage.DeriveCampaignId("TENANT-B", Titles(), Bodies()));
    }

    [Fact]
    public void CampaignId_Handles_Null_Tenant_In_Single_Tenant_Mode()
    {
        var first = SendSitewidePromoMessage.DeriveCampaignId(null, Titles(), Bodies());
        var second = SendSitewidePromoMessage.DeriveCampaignId(null, Titles(), Bodies());

        Assert.Equal(first, second);
        Assert.StartsWith("promo::", first);
    }
}
