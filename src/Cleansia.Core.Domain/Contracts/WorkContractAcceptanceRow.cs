namespace Cleansia.Core.Domain.Contracts;

/// <summary>
/// One acceptance row with the two facts its readers always join for: the language of the text row it
/// names and the order's display number. The order detail, the subject export and the customer's export
/// read this shape; the entity itself is loaded only by the read keyed on the acceptance id and the
/// erasure walk.
/// </summary>
public sealed record WorkContractAcceptanceRow(
    string Id,
    string OrderId,
    string OrderNumber,
    string OrderEmployeeId,
    string EmployeeId,
    string LegalDocumentTextId,
    string DocumentVersion,
    string Language,
    DateTimeOffset AcceptedOn,
    string ClientAudience,
    string? IpAddress,
    string? DeviceLabel,
    string? DeviceId,
    string FactsJson);
