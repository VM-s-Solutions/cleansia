namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// The live facts of the ambient company's books that must be zero before it can be sealed
/// (ADR-0064 D3), plus what the lifecycle page shows beside them. <see cref="LatestCardPaidCleaningDateTime"/>
/// is null when the company never took a card payment; the chargeback horizon is counted from it by
/// the caller, which holds the company's setting.
/// </summary>
public sealed record CompanySettlementFacts(
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
    DateTime? LatestCardPaidCleaningDateTime);
