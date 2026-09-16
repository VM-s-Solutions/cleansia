using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle.DTOs;

/// <summary>
/// The admin's own company: where it stands, who moved it there and when, and every live fact the
/// archive waits on (ADR-0064 D3–D4). Actors are named by e-mail, never by id; no tenant id and no
/// blob path cross the wire (S4).
/// </summary>
public record CompanyLifecycleDto(
    string Name,
    CompanyLifecycleState State,
    bool OperatesDefaultMarket,
    DateTimeOffset? DeactivatedOn,
    string? DeactivatedByEmail,
    DateOnly? WindDownFrom,
    DateTimeOffset? WindDownRequestedOn,
    string? WindDownRequestedByEmail,
    DateTimeOffset? WindDownRunStartedOn,
    DateTimeOffset? WindDownLastRunOn,
    DateTimeOffset? ArchiveRequestedOn,
    string? ArchiveRequestedByEmail,
    DateTimeOffset? ArchivedOn,
    string? ArchiveManifestSha256,
    int OpenOrders,
    int OpenOrdersOnOrAfterWindDownFrom,
    int ActiveTemplates,
    int ActiveMemberships,
    int CreditBalances,
    int PendingRefunds,
    int OrdersAwaitingPay,
    int OrdersAwaitingReceipt,
    int ReceiptsAwaitingFiscalRegistration,
    int OpenPayPeriods,
    int UnpaidInvoices,
    int UninvoicedPayRows,
    int OpenDisputes,
    DateTime? ChargebackHorizonEndsOn);
