using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminExportUserData
{
    public record Query(string UserId) : IQuery<GdprExportDto>;

    internal class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(q => q.UserId)
                .NotEmpty()
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId);
        }
    }

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
            var adminEmail = userSessionProvider.GetUserEmail() ?? "admin";

            var user = await userRepository.GetQueryable()
                .Include(u => u.Employee).ThenInclude(e => e!.Address)
                .AsNoTracking()
                .FirstAsync(u => u.Id == request.UserId, cancellationToken);

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

            var orders = await orderRepository.GetFiltered(o => o.UserId == request.UserId)
                .AsNoTracking()
                .Select(o => new GdprExportOrderDto(
                    o.Id, o.DisplayOrderNumber, o.CustomerName, o.CustomerEmail,
                    o.OrderStatusHistory.OrderByDescending(s => s.CreatedOn).First().Status,
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
                invoices = await employeeInvoiceRepository.GetByEmployeeId(user.Employee.Id)
                    .AsNoTracking()
                    .Select(i => new GdprExportInvoiceDto(
                        i.Id, i.InvoiceNumber, i.TotalAmount, i.Status, i.CreatedOn))
                    .ToListAsync(cancellationToken);
            }

            var consents = await userConsentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            var consentDtos = consents.Select(c => new GdprExportConsentDto(
                c.Id, c.ConsentType, c.IsGranted, c.GrantedAt, c.WithdrawnAt)).ToList();

            var metadata = new GdprExportMetadataDto(
                DateTimeOffset.UtcNow, $"admin:{adminEmail}", "JSON");

            var auditEntry = Core.Domain.Users.GdprRequest.Create(request.UserId, "Export");
            auditEntry.MarkCompleted(adminEmail);
            gdprRequestRepository.Add(auditEntry);

            return BusinessResult.Success(new GdprExportDto(
                profile, address, employee, orders,
                documents, invoices, consentDtos, metadata));
        }
    }
}
