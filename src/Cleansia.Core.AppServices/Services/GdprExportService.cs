using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public class GdprExportService(
    IUserRepository userRepository,
    IOrderRepository orderRepository,
    IEmployeeDocumentRepository employeeDocumentRepository,
    IEmployeeInvoiceRepository employeeInvoiceRepository,
    IUserConsentRepository userConsentRepository) : IGdprExportService
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
                emp.EntityType, emp.RegistrationNumber, emp.VatNumber, emp.LegalEntityName,
                emp.IBAN, emp.PassportId, emp.NationalityId,
                emp.EmergencyContactName, emp.EmergencyContactPhone,
                emp.PreferredCurrencyCode, emp.AverageRating, emp.ContractStatus, emp.CreatedOn);

        var orders = await orderRepository.GetFiltered(o => o.UserId == userId)
            .AsNoTracking()
            .Select(o => new GdprExportOrderDto(
                o.Id, o.DisplayOrderNumber, o.CustomerName, o.CustomerEmail,
                // Projection, not a filter: a pre-backfill NULL column must still export the
                // order's true status, so it falls back to the authoritative history subquery.
                o.CurrentStatus ?? o.OrderStatusHistory.OrderByDescending(s => s.CreatedOn).ThenByDescending(s => s.Sequence).First().Status,
                o.TotalPrice, o.CleaningDateTime, o.CreatedOn))
            .ToListAsync(cancellationToken);

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
            c.Id, c.ConsentType, c.IsGranted, c.GrantedAt, c.WithdrawnAt)).ToList();

        var metadata = new GdprExportMetadataDto(
            DateTimeOffset.UtcNow, exportedBy, "JSON");

        return new GdprExportDto(
            profile, address, employee, orders,
            documents, invoices, consentDtos, metadata);
    }
}
