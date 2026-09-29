using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public class GdprExportService(
    IUserRepository userRepository,
    IOrderRepository orderRepository,
    IDisputeRepository disputeRepository,
    IEmployeeDocumentRepository employeeDocumentRepository,
    IEmployeeInvoiceRepository employeeInvoiceRepository,
    IEmployeePayoutDetailsRepository employeePayoutDetailsRepository,
    IUserConsentRepository userConsentRepository,
    ICustomerActionAuditRepository customerActionAuditRepository,
    IWorkContractAcceptanceRepository workContractAcceptanceRepository,
    ILegalDocumentRepository legalDocumentRepository,
    ICleanerLegalDocumentAcceptanceRepository cleanerLegalDocumentAcceptanceRepository) : IGdprExportService
{
    public async Task<GdprExportDto> BuildAsync(
        string userId,
        string exportedBy,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetQueryable()
            .Include(u => u.Employee).ThenInclude(e => e!.Address)
            .AsNoTracking()
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var profile = new GdprExportProfileDto(
            user.Id, user.FirstName, user.LastName, user.Email,
            user.PhoneNumber, user.BirthDate, user.PreferredLanguageCode, user.CreatedOn);

        GdprExportAddressDto? address = null;
        if (user.Employee?.Address is { } addr)
            address = new GdprExportAddressDto(addr.Street, addr.City, addr.ZipCode, addr.State, addr.CountryId);

        GdprExportEmployeeDto? employee = null;
        if (user.Employee is { } emp)
            employee = new GdprExportEmployeeDto(
                emp.Id,
                emp.EntityType, emp.RegistrationNumber, emp.LegalEntityName,
                emp.IBAN, emp.PassportId, emp.NationalityId,
                emp.EmergencyContactName, emp.EmergencyContactPhone,
                emp.AverageRating, emp.ContractStatus, emp.CreatedOn);

        GdprExportPayoutDetailsDto? payoutDetails = null;
        if (user.Employee is not null)
        {
            var payout = await employeePayoutDetailsRepository.GetByEmployeeIdAsync(user.Employee.Id, cancellationToken);
            if (payout is not null)
                payoutDetails = new GdprExportPayoutDetailsDto(
                    payout.Scheme, payout.Status, payout.BankCountryId, payout.CurrencyId,
                    payout.AccountPrefix, payout.AccountNumber, payout.BankCode, payout.Iban,
                    payout.Swift, payout.BankName, payout.HolderName,
                    payout.ConfirmedAt, payout.LastRevealedAt, payout.RevealCount);
        }

        // Past the tenant filter for the same reason the erasure walk reads them so: a guest booking under
        // the subject's e-mail is stamped with the market's operator, not the subject's, and the predicate
        // is the pin (ADR-0051).
        var orders = await orderRepository.GetQueryableIgnoringTenant()
            .Where(SubjectOrders.Of(user.Id, user.Email))
            .AsNoTracking()
            .Select(o => new GdprExportOrderDto(
                o.Id, o.DisplayOrderNumber, o.CustomerName, o.CustomerEmail,
                o.CurrentStatus,
                o.TotalPrice, o.CleaningDateTime, o.CreatedOn))
            .ToListAsync(cancellationToken);

        // Filed on the account, or on one of the orders above: the second term keeps the section in step
        // with the orders section, the first is what still finds the disputes after an erasure has taken
        // the account off its orders. The bypass is for the order term alone: a dispute the account filed
        // is stamped with the subject's own operator (a chargeback re-pins to the order's, which an
        // account order shares), so the residual it guards is a dispute on a guest booking stamped with
        // another market's operator — none is written today. The pin is the caller's own id and the ids
        // the orders read yielded (ADR-0051).
        var orderIds = orders.Select(o => o.Id).ToList();
        var disputes = await disputeRepository.GetQueryableIgnoringTenant()
            .Where(d => d.UserId == user.Id || orderIds.Contains(d.OrderId))
            .Include(d => d.Messages)
            .Include(d => d.Evidence)
            .Include(d => d.Order).ThenInclude(o => o.Currency)
            .OrderBy(d => d.CreatedOn)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var disputeDtos = disputes.Select(MapDispute).ToList();

        var documents = new List<GdprExportDocumentDto>();
        if (user.Employee is not null)
        {
            var docs = await employeeDocumentRepository.GetByEmployeeIdAsync(user.Employee.Id, true, cancellationToken);
            documents = docs.Select(d => new GdprExportDocumentDto(
                d.Id, d.FileName, d.DocumentType.ToString(), d.CreatedOn)).ToList();
        }

        var invoices = new List<GdprExportInvoiceDto>();
        if (user.Employee is not null)
        {
            var fullInvoices = await employeeInvoiceRepository.GetByEmployeeIdAsync(user.Employee.Id, cancellationToken);
            invoices = fullInvoices
                .Select(i => new GdprExportInvoiceDto(
                    i.Id, i.InvoiceNumber, i.TotalAmount, i.Status, i.CreatedOn))
                .ToList();
        }

        var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(userId, cancellationToken);
        var consentDtos = consents.Select(c => new GdprExportConsentDto(
            c.Id, c.ConsentType, c.IsGranted, c.GrantedAt, c.WithdrawnAt,
            c.IpAddress, c.UserAgent, c.DocumentVersion, c.LegalDocumentId)).ToList();

        var customerActions = await customerActionAuditRepository.GetQueryable()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.OccurredOn)
            .AsNoTracking()
            .Select(a => new GdprExportCustomerActionDto(
                a.Action, a.OccurredOn, a.ResourceType, a.ResourceId, a.Success, a.ErrorCode,
                a.PayloadJson, a.IpAddress, a.DeviceLabel))
            .ToListAsync(cancellationToken);

        // The cleaner's own record and they are entitled to it, trio included — read null after an erasure.
        var workContractAcceptances = new List<GdprExportWorkContractAcceptanceDto>();
        if (user.Employee is not null)
        {
            var rows = await workContractAcceptanceRepository.GetByEmployeeIdNoTrackingAsync(user.Employee.Id, cancellationToken);
            workContractAcceptances = rows
                .Select(a => new GdprExportWorkContractAcceptanceDto(
                    a.OrderId, a.OrderNumber, a.OrderEmployeeId, a.LegalDocumentTextId, a.DocumentVersion, a.Language,
                    a.AcceptedOn, a.ClientAudience, a.IpAddress, a.DeviceLabel, a.DeviceId, a.FactsJson))
                .ToList();
        }

        var legalDocumentAcceptances = new List<GdprExportCleanerLegalDocumentAcceptanceDto>();
        if (user.Employee is not null)
        {
            var rows = await cleanerLegalDocumentAcceptanceRepository.GetByEmployeeIdNoTrackingAsync(user.Employee.Id, cancellationToken);
            var textIds = rows.Select(a => a.LegalDocumentTextId).Distinct().ToList();
            var texts = await legalDocumentRepository.GetQueryable()
                .AsNoTracking()
                .SelectMany(d => d.Texts, (d, t) => new { t.Id, d.Type, t.Language })
                .Where(t => textIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken);
            legalDocumentAcceptances = rows
                .Select(a => new GdprExportCleanerLegalDocumentAcceptanceDto(
                    texts[a.LegalDocumentTextId].Type, a.LegalDocumentTextId, a.DocumentVersion,
                    texts[a.LegalDocumentTextId].Language, a.AcceptedOn, a.ClientAudience,
                    a.IpAddress, a.DeviceLabel, a.DeviceId))
                .ToList();
        }

        var metadata = new GdprExportMetadataDto(
            DateTimeOffset.UtcNow, exportedBy, "JSON");

        return new GdprExportDto(
            profile, address, employee, payoutDetails, orders, disputeDtos,
            documents, invoices, consentDtos, customerActions, metadata, workContractAcceptances,
            legalDocumentAcceptances);
    }

    private static GdprExportDisputeDto MapDispute(Dispute dispute) =>
        new(
            dispute.Id,
            dispute.OrderId,
            dispute.Order.DisplayOrderNumber,
            dispute.Reason.ToString(),
            dispute.Description,
            dispute.Status.ToString(),
            dispute.ResolutionNotes,
            dispute.RefundAmount,
            dispute.Order.Currency?.Code ?? dispute.Order.CurrencyId,
            dispute.CreatedOn,
            dispute.ResolvedOn,
            dispute.Messages
                .OrderBy(m => m.CreatedOn).ThenBy(m => m.Id)
                .Select(m => new GdprExportDisputeMessageDto(m.IsStaffMessage ? "Staff" : nameof(UserProfile.Customer), m.CreatedOn, m.Message))
                .ToList(),
            dispute.Evidence.OrderBy(e => e.UploadedOn).ThenBy(e => e.Id).Select(e => e.FileName).ToList());
}
