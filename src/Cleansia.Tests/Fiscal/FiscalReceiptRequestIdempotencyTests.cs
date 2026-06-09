using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// ADR-0004 — the explicit authority-side idempotency token on the fiscal contract.
/// The token is the natural one (<c>ReceiptNumber</c>), but it is carried as a first-class field so
/// a provider's dedup behaviour is a stated contract, not an implicit per-provider assumption: the
/// register call and the recovery re-register must present the SAME token for the same logical
/// receipt, so the authority can collapse a redelivery onto the prior registration.
/// </summary>
public class FiscalReceiptRequestIdempotencyTests
{
    private const string ReceiptNumber = "RCP-2026-0042";

    private static FiscalReceiptRequest Build(string receiptNumber) =>
        FiscalReceiptRequest.Create(
            receiptNumber: receiptNumber,
            issuedAt: new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
            totalAmount: 1000m,
            vatAmount: null,
            currencyCode: "CZK",
            companyLegalName: "Cleansia s.r.o.",
            companyRegistrationNumber: "12345678",
            companyVatNumber: null,
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            lineItems: [],
            paymentMethod: "Cash",
            countryCode: "DE");

    [Fact]
    public void IdempotencyKey_Is_Derived_From_ReceiptNumber()
    {
        var request = Build(ReceiptNumber);

        Assert.Equal(ReceiptNumber, request.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_Is_Stable_Across_Original_And_Recovery_Requests_For_Same_ReceiptNumber()
    {
        var original = Build(ReceiptNumber);
        var recovery = Build(ReceiptNumber);

        Assert.Equal(original.IdempotencyKey, recovery.IdempotencyKey);
    }

    [Fact]
    public void Distinct_ReceiptNumbers_Yield_Distinct_IdempotencyKeys()
    {
        var first = Build("RCP-2026-0042");
        var second = Build("RCP-2026-0043");

        Assert.NotEqual(first.IdempotencyKey, second.IdempotencyKey);
    }
}
