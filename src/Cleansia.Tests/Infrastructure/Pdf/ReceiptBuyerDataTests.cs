using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// Owner ruling 2026-09-28: a receipt names the buyer by name and address only. The e-mail and the phone add
/// nothing to a tax document and would outlive the account on every stored PDF, so the document model
/// has no place for them and the layout no label for them.
/// </summary>
public sealed class ReceiptBuyerDataTests
{
    [Fact]
    public void The_Receipt_Model_Carries_The_Buyers_Name_And_Address_And_No_Contact_Detail()
    {
        var members = typeof(ReceiptPdfData).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains(nameof(ReceiptPdfData.CustomerName), members);
        Assert.Contains(nameof(ReceiptPdfData.CustomerAddress), members);
        Assert.DoesNotContain(members, m => m.Contains("Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("Phone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_Locale_Has_A_Label_For_A_Contact_Detail()
    {
        var labels = typeof(ReceiptLabels).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(labels, l => l.Contains("Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(labels, l => l.Contains("Phone", StringComparison.OrdinalIgnoreCase));
    }
}
