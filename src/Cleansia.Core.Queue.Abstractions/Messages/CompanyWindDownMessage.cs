namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// One run of the company wind-down sweep (ADR-0064 D2). Recorded by <c>WindDownCompany</c> and by
/// <c>DeactivateCompany</c> when a date is already set; the consumer reads everything else off the
/// company's own row, so a redelivery and a later request converge on the same state.
/// </summary>
public record CompanyWindDownMessage(string TenantId);
