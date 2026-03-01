using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class ExportUserData
{
    public record Query : IQuery<GdprExportDto>;

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IOrderRepository orderRepository,
        IEmployeeDocumentRepository employeeDocumentRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IUserConsentRepository userConsentRepository,
        IGdprRequestRepository gdprRequestRepository)
        : IQueryHandler<Query, GdprExportDto>
    {
        public async Task<BusinessResult<GdprExportDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var email = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(email!, cancellationToken);

            if (user is null)
                return BusinessResult.Failure<GdprExportDto>(new Error(
                    BusinessErrorMessage.NotExistingUserWithEmail, "User not found"));

            var export = await BuildExportAsync(user.Id, email!, cancellationToken);

            var auditEntry = Core.Domain.Users.GdprRequest.Create(user.Id, "Export");
            auditEntry.MarkCompleted(email);
            gdprRequestRepository.Add(auditEntry);

            return BusinessResult.Success(export);
        }

        private async Task<GdprExportDto> BuildExportAsync(string userId, string exportedBy, CancellationToken ct)
        {
            var user = await userRepository.GetQueryable()
                .Include(u => u.Employee).ThenInclude(e => e!.Address)
                .AsNoTracking()
                .FirstAsync(u => u.Id == userId, ct);

            var profile = new GdprExportProfileDto(
                user.Id, user.FirstName, user.LastName, user.Email,
                user.PhoneNumber, user.BirthDate, user.PreferredLanguageCode, user.CreatedOn);

            GdprExportAddressDto? address = null;
            if (user.Employee?.Address is { } addr)
                address = new GdprExportAddressDto(addr.Street, addr.City, addr.ZipCode, addr.State, addr.CountryId);

            GdprExportEmployeeDto? employee = null;
            if (user.Employee is { } emp)
                employee = new GdprExportEmployeeDto(
                    emp.Id, emp.TaxId, emp.IBAN, emp.PassportId, emp.NationalityId,
                    emp.EmergencyContactName, emp.EmergencyContactPhone,
                    emp.PreferredCurrencyCode, emp.AverageRating, emp.ContractStatus, emp.CreatedOn);

            var orders = await orderRepository.GetFiltered(o => o.UserId == userId)
                .AsNoTracking()
                .Select(o => new GdprExportOrderDto(
                    o.Id, o.DisplayOrderNumber, o.CustomerName, o.CustomerEmail,
                    o.OrderStatusHistory.OrderByDescending(s => s.CreatedOn).First().Status,
                    o.TotalPrice, o.CleaningDateTime, o.CreatedOn))
                .ToListAsync(ct);

            var documents = new List<GdprExportDocumentDto>();
            if (user.Employee is not null)
            {
                var docs = await employeeDocumentRepository.GetByEmployeeIdAsync(user.Employee.Id, true, ct);
                documents = docs.Select(d => new GdprExportDocumentDto(
                    d.Id, d.FileName, d.DocumentType.ToString(), d.CreatedOn)).ToList();
            }

            var invoices = new List<GdprExportInvoiceDto>();
            if (user.Employee is not null)
            {
                invoices = await employeeInvoiceRepository.GetByEmployeeId(user.Employee.Id)
                    .AsNoTracking()
                    .Select(i => new GdprExportInvoiceDto(
                        i.Id, i.InvoiceNumber, i.TotalAmount, i.Status, i.CreatedOn))
                    .ToListAsync(ct);
            }

            var consents = await userConsentRepository.GetByUserIdAsync(userId, ct);
            var consentDtos = consents.Select(c => new GdprExportConsentDto(
                c.Id, c.ConsentType, c.IsGranted, c.GrantedAt, c.WithdrawnAt)).ToList();

            var metadata = new GdprExportMetadataDto(
                DateTimeOffset.UtcNow, exportedBy, "JSON");

            return new GdprExportDto(
                profile, address, employee, orders,
                documents, invoices, consentDtos, metadata);
        }
    }
}
