using System.Text;
using System.Text.RegularExpressions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.IncidentFile;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Fluent;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// The incident file's document model and its render (Q-AUD-L6 ruling: a PDF). QuestPDF subsets its
/// fonts and nothing can be read back out of the bytes, so the content claims are made on the section
/// model the layout draws from — the same model the printed SHA-256 is computed over — and the render
/// claims follow the invoice's shape: one render at a time, byte-identical for identical data once the
/// wall-clock CreationDate/ModDate are zeroed, different when the trail, the generator or the printed
/// hash differs.
/// </summary>
[Collection("QuestPdfRenderer")]
public sealed class IncidentFileDocumentTests
{
    private static readonly QuestPdfService Pdf = new(
        new LayoutBuilderFactory([], []),
        NullLogger<QuestPdfService>.Instance);

    private static readonly DateTimeOffset Booked = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private const string OperatorName = "Cleansia CZ s.r.o.";
    private const string Market = "Czechia (CZ)";

    [Fact]
    public void The_Data_Section_Carries_Every_Heading_The_Order_Number_And_The_Trail_Labels()
    {
        var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(Fixture()));

        foreach (var heading in new[]
                 {
                     IncidentFileSections.IdentityTitle, IncidentFileSections.OrdersTitle, IncidentFileSections.DisputesTitle,
                     IncidentFileSections.ConsentsTitle, IncidentFileSections.TrailTitle,
                 })
        {
            Assert.Contains($"# {heading}\n", text);
        }

        Assert.Contains("## Order ORD-INC-1\n", text);
        Assert.Contains("Name: Jane Doe\n", text);
        Assert.Contains("E-mail: jane.doe@example.test\n", text);
        Assert.Contains("Total: 1,500.00 CZK\n", text);
        Assert.Contains("Service | Deep clean | 1,500.00 CZK\n", text);
        Assert.Contains("Assigned cleaners: Tomas (emp-1)\n", text);
        Assert.Contains("Confirmed | 2026-03-01 10:05:00 UTC\n", text);
        Assert.Contains("## Dispute disp-1 on order ORD-INC-1\n", text);
        Assert.Contains("Customer user-1 | The room was not cleaned.\n", text);
        Assert.Contains("TermsOfService | 2026-09-14 | 2026-09-14 | granted", text);

        Assert.Contains("Action: customer.order.cancel\n", text);
        Assert.Contains("Action: order.refund.full\n", text);
        Assert.Contains("Action: employee.order.dropped\n", text);
        Assert.Contains("Outcome: refused: order.in_progress_cannot_cancel\n", text);
        Assert.Contains("currencyId | CZK\n", text);
        Assert.Contains("Who: Customer user-1\n", text);
        Assert.Contains("Who: Administrator admin-1\n", text);
        Assert.Contains("Who: Employee emp-1\n", text);
        Assert.Contains("Request: 203.0.113.9 / iPhone 15\n", text);
    }

    [Fact]
    public void The_Identity_Section_Names_The_Operating_Company_And_The_Market_Never_A_Tenant_Id()
    {
        var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(Fixture()));

        Assert.Contains($"Operator: {OperatorName}\n", text);
        Assert.Contains($"Market: {Market}\n", text);
        Assert.DoesNotContain("cleansia-cz", text);
    }

    [Fact]
    public void A_Subject_Whose_Operator_Serves_No_Configured_Market_Prints_A_Blank_Not_A_Tenant_Id()
    {
        var data = Fixture() with { Subject = Fixture().Subject with { OperatorName = null, Market = null } };

        var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(data));

        Assert.Contains($"Operator: {IncidentFileSections.Empty}\n", text);
        Assert.Contains($"Market: {IncidentFileSections.Empty}\n", text);
        Assert.DoesNotContain("cleansia-cz", text);
    }

    [Fact]
    public void Customer_Acts_Come_First_Then_Admin_Then_Cleaner_Each_Newest_First()
    {
        var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(Fixture()));

        var customer = text.IndexOf("## Customer acts (2)", StringComparison.Ordinal);
        var admin = text.IndexOf("## Admin acts (1)", StringComparison.Ordinal);
        var cleaner = text.IndexOf("## Cleaner acts (1)", StringComparison.Ordinal);
        Assert.True(customer > 0 && customer < admin && admin < cleaner);

        var refused = text.IndexOf("Action: customer.order.cancel\nOutcome: refused", StringComparison.Ordinal);
        var succeeded = text.IndexOf("Action: customer.order.cancel\nOutcome: success", StringComparison.Ordinal);
        Assert.True(refused > 0 && refused < succeeded, "the newer (refused) act is printed before the older one");
    }

    [Fact]
    public void An_Erased_Subject_Reads_Erased_Where_The_Identity_Was_And_The_Trail_Stays()
    {
        var erasedOn = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        var data = Fixture() with
        {
            Subject = Fixture().Subject with
            {
                FirstName = "[DELETED]", LastName = "[DELETED]", Email = "deleted_user-1@anonymized.local", PhoneNumber = null,
                Erased = true, ErasedOn = erasedOn,
            },
        };

        var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(data));

        Assert.Contains("Name: erased\n", text);
        Assert.Contains("E-mail: erased\n", text);
        Assert.Contains("Phone: erased\n", text);
        Assert.Contains("Erased: yes, 2026-09-01 08:00:00 UTC\n", text);
        Assert.DoesNotContain("[DELETED]", text);
        Assert.DoesNotContain("anonymized.local", text);
        Assert.Contains("Action: customer.order.cancel\n", text);
    }

    [Fact]
    public void The_Hash_Covers_The_Data_And_Not_The_Generation()
    {
        var data = Fixture();
        var later = data with { GeneratedAt = data.GeneratedAt.AddDays(3), GeneratedBy = "someone-else@cleansia.test" };
        var otherTrail = data with
        {
            Trail = data.Trail.Select(e => e.Action == "order.refund.full" ? e with { Action = "order.refund.partial" } : e).ToList(),
        };

        var hash = IncidentFileDigest.Sha256Hex(IncidentFileSections.Build(data));

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, IncidentFileDigest.Sha256Hex(IncidentFileSections.Build(later)));
        Assert.NotEqual(hash, IncidentFileDigest.Sha256Hex(IncidentFileSections.Build(otherTrail)));
    }

    [Fact]
    public void The_Render_Is_A_Pdf_Whose_Bytes_Follow_The_Data_And_Whose_Hash_Is_The_Digest()
    {
        var data = Fixture();
        var otherTrail = data with
        {
            Trail = data.Trail.Select(e => e.Action == "order.refund.full" ? e with { Action = "order.refund.partial" } : e).ToList(),
        };

        var rendered = Pdf.GenerateIncidentFilePdf(data);
        var renderedAgain = Pdf.GenerateIncidentFilePdf(data);
        var renderedOther = Pdf.GenerateIncidentFilePdf(otherTrail);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(rendered.Bytes, 0, 4));
        Assert.Equal(IncidentFileDigest.Sha256Hex(IncidentFileSections.Build(data)), rendered.DataSha256);
        // The equality half is what stops the other half being vacuous: without it "two renders
        // differ" would be true even of a layout that dropped the field.
        Assert.Equal(WithoutClock(rendered.Bytes), WithoutClock(renderedAgain.Bytes));
        Assert.NotEqual(WithoutClock(rendered.Bytes), WithoutClock(renderedOther.Bytes));
        Assert.NotEqual(rendered.DataSha256, renderedOther.DataSha256);
    }

    [Fact]
    public void The_Footer_Prints_The_Generator()
    {
        var data = Fixture();
        var byAnotherAdmin = data with { GeneratedBy = "someone-else@cleansia.test" };

        // The generator's e-mail is printed nowhere but the footer, so the only way these bytes differ
        // is that the footer is drawn.
        Assert.NotEqual(WithoutClock(Pdf.GenerateIncidentFilePdf(data).Bytes), WithoutClock(Pdf.GenerateIncidentFilePdf(byAnotherAdmin).Bytes));
    }

    [Fact]
    public void The_Integrity_Block_Prints_The_Hash()
    {
        var data = Fixture();
        var sections = IncidentFileSections.Build(data);

        var printedA = RenderLayout(data, sections, new string('a', 64));
        var printedB = RenderLayout(data, sections, new string('b', 64));

        // Same data, same sections, a different hash handed to the layout: only the Integrity block
        // can tell the two documents apart.
        Assert.NotEqual(WithoutClock(printedA), WithoutClock(printedB));
    }

    [Fact]
    public void An_Empty_Subject_Still_Renders_Every_Section()
    {
        var data = Fixture() with { Orders = [], Disputes = [], Consents = [], Trail = [], OrderIdFilter = "order-x" };

        var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(data));
        var rendered = Pdf.GenerateIncidentFilePdf(data);

        Assert.Contains("Scoped to order order-x.\n", text);
        Assert.Contains("No orders.\n", text);
        Assert.Contains("No disputes.\n", text);
        Assert.Contains("No consents.\n", text);
        Assert.Contains("## Customer acts (0)\nNone.\n", text);
        Assert.NotEmpty(rendered.Bytes);
    }

    private static byte[] RenderLayout(IncidentFilePdfData data, IReadOnlyList<IncidentFileSection> sections, string dataSha256) =>
        Document.Create(c => IncidentFileLayoutBuilder.Build(c, data, sections, dataSha256)).GeneratePdf();

    // The wall-clock CreationDate/ModDate are zeroed IN PLACE, so every byte offset — and therefore the
    // xref table — stays valid and two documents remain comparable byte for byte.
    private static byte[] WithoutClock(byte[] bytes) =>
        Encoding.Latin1.GetBytes(Regex.Replace(Encoding.Latin1.GetString(bytes), @"(?<=D:)\d{14}", new string('0', 14)));

    private static IncidentFilePdfData Fixture()
    {
        var subject = new IncidentFileSubject(
            "user-1", "Jane", "Doe", "jane.doe@example.test", "+420123456789",
            Booked.AddMonths(-6), OperatorName, Market, "en", Erased: false, ErasedOn: null);

        var order = new IncidentFileOrder(
            "order-1", "ORD-INC-1", Booked, Booked.AddDays(3).UtcDateTime, null, null,
            "Order St 9, 60200, Brno, CZ",
            [new IncidentFileOrderLine("Service", "Deep clean", 1500m)],
            1500m, "CZK", "Cash", "Pending", "Confirmed",
            [new IncidentFileStatusChange("New", Booked), new IncidentFileStatusChange("Confirmed", Booked.AddMinutes(5))],
            [new IncidentFileRefund(300m, "CZK", "Dispute", "Admin", "Succeeded", Booked.AddDays(5), Booked.AddDays(5))],
            [new IncidentFileCleaner("emp-1", "Tomas")],
            CancelledBy: null, CancellationReason: null);

        var dispute = new IncidentFileDispute(
            "disp-1", "ORD-INC-1", "ServiceQuality", "Resolved", "Kitchen untouched.", Booked.AddDays(4),
            [new IncidentFileDisputeMessage("Customer", "user-1", Booked.AddDays(4), "The room was not cleaned.")],
            ["photo-1.jpg"], "Partial refund issued.", 300m, "admin-1", Booked.AddDays(5));

        var consent = new IncidentFileConsent(
            "TermsOfService", "2026-09-14", new DateOnly(2026, 9, 14), IsGranted: true, Booked.AddMonths(-6), null,
            "203.0.113.9", "Mozilla/5.0");

        IReadOnlyList<IncidentFileTrailEntry> trail =
        [
            new("Customer", Booked.AddDays(1), "Customer", "user-1", "customer.order.cancel", "Order", "order-1", true, null,
                [new("currencyId", "CZK"), new("feeRate", "0.5")], "203.0.113.9", "iPhone 15"),
            new("Customer", Booked.AddDays(2), "Customer", "user-1", "customer.order.cancel", "Order", "order-1", false,
                "order.in_progress_cannot_cancel", [], "203.0.113.9", "iPhone 15"),
            new("Admin", Booked.AddDays(5), "Administrator", "admin-1", "order.refund.full", "Order", "order-1", true, null,
                [new("reason", "dispute upheld"), new("after.amount", "300")], null, null),
            new("Cleaner", Booked.AddDays(2), "Employee", "emp-1", "employee.order.dropped", "Order", "order-1", true, null, [], null, null),
        ];

        return new IncidentFilePdfData(
            subject, null, [order], [dispute], [consent], trail, TrailTruncated: false,
            new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero), "admin@cleansia.test");
    }
}
