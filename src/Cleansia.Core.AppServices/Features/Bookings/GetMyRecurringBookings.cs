using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Bookings;

public class GetMyRecurringBookings
{
    public record Query : IQuery<IReadOnlyList<RecurringBookingTemplateDto>>;

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IUserSessionProvider userSessionProvider,
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository) : IQueryHandler<Query, IReadOnlyList<RecurringBookingTemplateDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<RecurringBookingTemplateDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var templates = await templateRepository.GetByUserAsync(userId, cancellationToken);
            if (templates.Count == 0)
            {
                return BusinessResult.Success<IReadOnlyList<RecurringBookingTemplateDto>>([]);
            }

            var addresses = await savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            var addressById = addresses
                .Where(a => a.Address != null)
                .ToDictionary(a => a.Id, a => a);

            var cashTemplates = templates.Where(t => t.PaymentType == PaymentType.Cash).ToList();
            var cashEligibility = cashTemplates.Count == 0
                ? null
                : await RecurringCashEligibility.LoadAsync(
                    serviceRepository,
                    packageRepository,
                    cashTemplates.SelectMany(t => t.SelectedServiceIds),
                    cashTemplates.SelectMany(t => t.SelectedPackageIds),
                    cancellationToken);

            var dtos = templates.Select(t =>
            {
                var addr = addressById.TryGetValue(t.SavedAddressId, out var sa) ? sa : null;
                var line = addr?.Address == null
                    ? null
                    : $"{addr.Address.Street}, {addr.Address.City} {addr.Address.ZipCode}";

                return new RecurringBookingTemplateDto(
                    Id: t.Id,
                    Frequency: (int)t.Frequency,
                    DayOfWeek: (int)t.DayOfWeek,
                    TimeOfDay: t.TimeOfDay.ToString("HH:mm"),
                    Rooms: t.Rooms,
                    Bathrooms: t.Bathrooms,
                    SavedAddressId: t.SavedAddressId,
                    AddressLine: line,
                    SelectedServiceIds: t.SelectedServiceIds.ToList(),
                    SelectedPackageIds: t.SelectedPackageIds.ToList(),
                    PaymentType: (int)t.PaymentType,
                    StartsOn: t.StartsOn,
                    EndsOn: t.EndsOn,
                    LastMaterializedFor: t.LastMaterializedFor,
                    IsActive: t.IsActive,
                    PreferredEmployeeId: t.PreferredEmployeeId,
                    RequiresPaymentMethodChange: t.PaymentType == PaymentType.Cash
                        && !cashEligibility!.Allows(t.SelectedServiceIds, t.SelectedPackageIds));
            }).ToList();

            return BusinessResult.Success<IReadOnlyList<RecurringBookingTemplateDto>>(dtos);
        }
    }
}
