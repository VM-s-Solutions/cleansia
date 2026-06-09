using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Fiscal.Countries.Czechia;
using Cleansia.Infra.Fiscal.NoOp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// ADR-0004 — the per-provider register-idempotency go-live gate. A redelivery / recovery re-calls
/// <see cref="IFiscalService.RegisterReceiptAsync"/> with the SAME idempotency token; for a
/// BlockingOnline regime (DE TSE / AT RKSV / ES VeriFactu) a double authority registration is a
/// compliance incident. Two things are asserted: (1) a provider that declares itself
/// register-idempotent must collapse a repeat call onto the prior registration; (2) the go-live gate
/// refuses to run a non-idempotent provider under a blocking enforcement mode.
/// </summary>
public class FiscalRegisterIdempotencyContractTests
{
    private static FiscalReceiptRequest Request(string receiptNumber) =>
        FiscalReceiptRequest.Create(
            receiptNumber: receiptNumber,
            issuedAt: new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
            totalAmount: 1000m,
            vatAmount: null,
            currencyCode: "EUR",
            companyLegalName: "Cleansia GmbH",
            companyRegistrationNumber: "HRB-1",
            companyVatNumber: "DE123456789",
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            lineItems: [],
            paymentMethod: "Cash",
            countryCode: "DE");

    [Fact]
    public async Task Idempotent_Provider_Does_Not_Double_Register_On_Same_Token()
    {
        var provider = new RecordingIdempotentProvider();
        var request = Request("RCP-2026-0042");

        var first = await provider.RegisterReceiptAsync(request, CancellationToken.None);
        var second = await provider.RegisterReceiptAsync(request, CancellationToken.None);

        Assert.True(provider.RegisterIsIdempotent);
        Assert.Equal(1, provider.AuthorityEntriesBurned);
        Assert.True(first.IsRegistered);
        Assert.True(second.IsRegistered);
        Assert.Equal(first.FiscalCode, second.FiscalCode);
    }

    [Fact]
    public async Task Idempotent_Provider_Burns_New_Entry_For_Different_Token()
    {
        var provider = new RecordingIdempotentProvider();

        await provider.RegisterReceiptAsync(Request("RCP-2026-0042"), CancellationToken.None);
        await provider.RegisterReceiptAsync(Request("RCP-2026-0043"), CancellationToken.None);

        Assert.Equal(2, provider.AuthorityEntriesBurned);
    }

    [Fact]
    public void GoLiveGate_Rejects_NonIdempotent_Provider_In_Blocking_Mode()
    {
        var provider = new NonIdempotentProvider();

        Assert.Throws<FiscalGoLiveGateException>(() =>
            FiscalGoLiveGate.EnsureRegisterIdempotent(provider, FiscalEnforcementMode.BlockingOnline));
        Assert.Throws<FiscalGoLiveGateException>(() =>
            FiscalGoLiveGate.EnsureRegisterIdempotent(provider, FiscalEnforcementMode.BlockingWithOfflineCache));
    }

    [Fact]
    public void GoLiveGate_Admits_Idempotent_Provider_In_Blocking_Mode()
    {
        var provider = new RecordingIdempotentProvider();

        FiscalGoLiveGate.EnsureRegisterIdempotent(provider, FiscalEnforcementMode.BlockingOnline);
        FiscalGoLiveGate.EnsureRegisterIdempotent(provider, FiscalEnforcementMode.BlockingWithOfflineCache);
    }

    [Fact]
    public void GoLiveGate_Ignores_NonBlocking_Modes()
    {
        var provider = new NonIdempotentProvider();

        FiscalGoLiveGate.EnsureRegisterIdempotent(provider, FiscalEnforcementMode.None);
        FiscalGoLiveGate.EnsureRegisterIdempotent(provider, FiscalEnforcementMode.AsyncBackground);
    }

    [Fact]
    public void Shipped_Providers_Declare_Their_RegisterIdempotency()
    {
        var noOp = new NoOpFiscalService();
        var czech = new CzechEet2FiscalService(
            Options.Create(new CzechEet2Options()),
            new HttpClient(),
            NullLogger<CzechEet2FiscalService>.Instance);

        Assert.True(noOp.RegisterIsIdempotent);
        Assert.True(czech.RegisterIsIdempotent);
    }

    private sealed class RecordingIdempotentProvider : IFiscalService
    {
        private readonly Dictionary<string, FiscalResult> _byKey = new(StringComparer.Ordinal);

        public string ProviderKey => "test-idempotent";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => true;
        public int AuthorityEntriesBurned { get; private set; }

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            if (_byKey.TryGetValue(request.IdempotencyKey, out var prior))
            {
                return Task.FromResult(prior);
            }

            AuthorityEntriesBurned++;
            var result = FiscalResult.Success($"SIG-{request.IdempotencyKey}", request.IssuedAt.ToString("o"));
            _byKey[request.IdempotencyKey] = result;
            return Task.FromResult(result);
        }
    }

    private sealed class NonIdempotentProvider : IFiscalService
    {
        public string ProviderKey => "test-non-idempotent";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => false;

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(FiscalResult.Success($"SIG-{Guid.NewGuid():N}", request.IssuedAt.ToString("o")));
    }
}
