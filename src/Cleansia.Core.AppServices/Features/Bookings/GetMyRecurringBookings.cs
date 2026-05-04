using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Returns the calling user's recurring booking templates (active + paused),
/// ordered active-first then by creation date. Drives the Plus "My recurring
/// cleanings" list in the customer apps.
///
/// AddressLine is denormalized from the saved address to keep the customer UI
/// from needing a second round-trip just to render the row.
/// </summary>
public class GetMyRecurringBookings
{
    public record Query(string UserId = "") : IQuery<IReadOnlyList<RecurringBookingTemplateDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository) : IQueryHandler<Query, IReadOnlyList<RecurringBookingTemplateDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<RecurringBookingTemplateDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var templates = await templateRepository.GetByUserAsync(query.UserId, cancellationToken);
            if (templates.Count == 0)
            {
                return BusinessResult.Success<IReadOnlyList<RecurringBookingTemplateDto>>([]);
            }

            // One round-trip for all of the user's saved addresses; we look them
            // up by id to denormalize AddressLine onto each row. The user
            // typically has 1-3 saved addresses so this stays cheap.
            var addresses = await savedAddressRepository.GetByUserAsync(query.UserId, cancellationToken);
            var addressById = addresses
                .Where(a => a.Address != null)
                .ToDictionary(a => a.Id, a => a);

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
                    IsActive: t.IsActive);
            }).ToList();

            return BusinessResult.Success<IReadOnlyList<RecurringBookingTemplateDto>>(dtos);
        }
    }
}
