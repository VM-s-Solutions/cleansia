namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

/// <summary>
/// A contract for work rendered for one job: the exact text row the client must echo on the take
/// (<see cref="LegalDocumentTextId"/>), the facts the acceptance binds, and — on a read of an accepted
/// contract — the acceptance itself. The preview carries a null <see cref="Acceptance"/>; the read
/// carries the STORED facts, never the live order.
/// </summary>
public record WorkContractDto(
    string LegalDocumentTextId,
    string LegalDocumentId,
    string Version,
    DateOnly EffectiveFrom,
    string Language,
    string Title,
    string ContentHtml,
    WorkContractFacts Facts,
    WorkContractAcceptanceDetails? Acceptance
);

/// <summary>
/// The acceptance behind a read. <see cref="AcceptedLanguage"/> is the language of the text row that was
/// accepted, so a page rendering another language can say so.
/// </summary>
public record WorkContractAcceptanceDetails(
    DateTimeOffset AcceptedOn,
    string DocumentVersion,
    string AcceptedLanguage,
    string OrderEmployeeId,
    string EmployeeId
);

/// <summary>
/// One acceptance on the order detail, one per current seat that has a row. Paired with the crew entry
/// by <see cref="OrderEmployeeId"/> == <c>AssignedEmployeeDto.Id</c>, which is where the name comes from
/// — already audience-masked there, so this carries none.
/// </summary>
public record WorkContractAcceptanceDto(
    string Id,
    string OrderEmployeeId,
    string EmployeeId,
    DateTimeOffset AcceptedOn,
    string DocumentVersion,
    string Language
);
