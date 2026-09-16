using Cleansia.Core.AppServices.Features.CompanyLifecycle.DTOs;
using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Core.AppServices.Mappers;

public static class CompanyLifecycleMappers
{
    public static CompanyLifecycleDto MapToDto(
        this Tenant tenant,
        bool operatesDefaultMarket,
        CompanySettlementFacts facts,
        DateTime? chargebackHorizonEndsOn,
        string? deactivatedByEmail,
        string? windDownRequestedByEmail,
        string? archiveRequestedByEmail) =>
        new(
            Name: tenant.Name,
            State: tenant.State,
            OperatesDefaultMarket: operatesDefaultMarket,
            DeactivatedOn: tenant.DeactivatedOn,
            DeactivatedByEmail: deactivatedByEmail,
            WindDownFrom: tenant.WindDownFrom,
            WindDownRequestedOn: tenant.WindDownRequestedOn,
            WindDownRequestedByEmail: windDownRequestedByEmail,
            WindDownRunStartedOn: tenant.WindDownRunStartedOn,
            WindDownLastRunOn: tenant.WindDownLastRunOn,
            ArchiveRequestedOn: tenant.ArchiveRequestedOn,
            ArchiveRequestedByEmail: archiveRequestedByEmail,
            ArchivedOn: tenant.ArchivedOn,
            ArchiveManifestSha256: tenant.ArchiveManifestSha256,
            OpenOrders: facts.OpenOrders,
            OpenOrdersOnOrAfterWindDownFrom: facts.OpenOrdersOnOrAfterWindDownFrom,
            ActiveTemplates: facts.ActiveTemplates,
            ActiveMemberships: facts.ActiveMemberships,
            CreditBalances: facts.CreditBalances,
            PendingRefunds: facts.PendingRefunds,
            OrdersAwaitingPay: facts.OrdersAwaitingPay,
            OrdersAwaitingReceipt: facts.OrdersAwaitingReceipt,
            ReceiptsAwaitingFiscalRegistration: facts.ReceiptsAwaitingFiscalRegistration,
            OpenPayPeriods: facts.OpenPayPeriods,
            UnpaidInvoices: facts.UnpaidInvoices,
            UninvoicedPayRows: facts.UninvoicedPayRows,
            OpenDisputes: facts.OpenDisputes,
            ChargebackHorizonEndsOn: chargebackHorizonEndsOn);
}
