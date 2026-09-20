using Cleansia.Core.Domain.Legal;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;

namespace Cleansia.IntegrationTests;

/// <summary>
/// The fixed-id contract-for-work documents for the arrange steps that book, take or read under one,
/// so a take has a text to echo without running the real seeder. The in-force one is platform-wide
/// (no market), so it resolves for whatever country a fixture invents: every arrange that books
/// through the real <c>OrderFactory</c> needs it, since the factory refuses a booking with no
/// contract text in force for the address's market. Added by the arrange step rather than after
/// every reset, because the seeder's own tests count exactly what the seed lands.
/// </summary>
public static class TestLegalDocuments
{
    public const string WorkContractId = WorkContractTestData.DocumentId;
    public const string WorkContractTextEnId = WorkContractTestData.TextIdEn;
    public const string WorkContractTextCsId = WorkContractTestData.TextIdCs;
    public const string OtherWorkContractId = WorkContractTestData.OtherDocumentId;
    public const string OtherWorkContractTextId = WorkContractTestData.OtherTextId;

    /// <summary>Adds the in-force document and an older one of the same type; the arrange step's commit lands them.</summary>
    public static (LegalDocument WorkContract, LegalDocument Other) Add(CleansiaDbContext context)
    {
        var contract = WorkContractTestData.Document();
        var other = WorkContractTestData.OtherDocument();
        context.LegalDocuments.AddRange(contract, other);
        return (contract, other);
    }
}
