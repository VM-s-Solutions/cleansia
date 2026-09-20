using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.TestUtilities;

/// <summary>
/// One in-force contract-for-work document with an English and a Czech text, and the mocks a test
/// needs to book, preview, take or read under it. Fixed ids at ULID width, so the same fixture serves
/// the mocked suites and a Postgres arrange step; effective well before the embedded seed's version, so
/// an arrange step that also runs the real seeder keeps the seeded document in force and lands both
/// without a collision on the identity index.
/// </summary>
public static class WorkContractTestData
{
    public const string DocumentId = "01WCDOC0000000000000000001";
    public const string TextIdEn = "01WCTXT00000000000000000EN";
    public const string TextIdCs = "01WCTXT00000000000000000CS";
    public const string Version = "2026-01-01";

    /// <summary>A second, unrelated document — the "text of another document" every mismatch case needs.</summary>
    public const string OtherDocumentId = "01WCDOC0000000000000000002";
    public const string OtherTextId = "01WCTXT0000000000000000OTH";

    public static readonly DateOnly EffectiveFrom = new(2026, 1, 1);

    public static LegalDocument Document()
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.WorkContract, null, EffectiveFrom);
        document.Id = DocumentId;
        document.AddText("en", "Contract for Work", "## Price\n\nThe price is stated in {{currency}}.").Id = TextIdEn;
        document.AddText("cs", "Smlouva o dílo", "## Cena\n\nCena je uvedena v {{currency}}.").Id = TextIdCs;
        return document;
    }

    public static LegalDocument OtherDocument()
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.WorkContract, null, EffectiveFrom.AddMonths(-7));
        document.Id = OtherDocumentId;
        document.AddText("en", "Contract for Work", "## Old\n\nAn older wording.").Id = OtherTextId;
        return document;
    }

    /// <summary>The order was booked under <see cref="Document"/>, as the factory stamps it.</summary>
    public static Order BookedUnderContract(Order order) => order.SetWorkContractDocument(Document());

    /// <summary>A repository that knows both documents by their texts and by id.</summary>
    public static Mock<ILegalDocumentRepository> LegalDocumentRepository()
    {
        var document = Document();
        var other = OtherDocument();
        var repository = new Mock<ILegalDocumentRepository>();
        repository
            .Setup(r => r.GetByTextIdWithTextsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string textId, CancellationToken _) =>
                document.Texts.Any(t => t.Id == textId) ? document
                : other.Texts.Any(t => t.Id == textId) ? other
                : null);
        repository
            .Setup(r => r.GetWithTextsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
                id == document.Id ? document : id == other.Id ? other : null);
        return repository;
    }

    /// <summary>An acceptance repository whose seat gate answers the same for every seat.</summary>
    public static Mock<IWorkContractAcceptanceRepository> AcceptanceRepository(bool everySeatAccepted = true)
    {
        var repository = new Mock<IWorkContractAcceptanceRepository>();
        repository
            .Setup(r => r.AnyForSeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(everySeatAccepted);
        repository
            .Setup(r => r.GetForSeatsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return repository;
    }
}
