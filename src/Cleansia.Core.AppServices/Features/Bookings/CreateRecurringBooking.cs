using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Creates a new <see cref="RecurringBookingTemplate"/> for the calling user.
/// The materializer background job picks it up on its next run and starts
/// spawning concrete Order rows for each occurrence within the 7-day horizon.
///
/// Wire shape mirrors the regular CreateOrder where possible (rooms, bathrooms,
/// service/package ids, payment type) so the customer UI can reuse most of its
/// booking-form state when promoting a one-off to a recurring schedule.
///
/// Frequency / DayOfWeek / TimeOfDay determine the recurrence; StartsOn anchors
/// the first occurrence; EndsOn is optional (null = indefinite).
/// </summary>
public class CreateRecurringBooking
{
    public record Command(
        int Frequency,
        int DayOfWeek,
        string TimeOfDay,
        int Rooms,
        int Bathrooms,
        string SavedAddressId,
        IReadOnlyList<string> SelectedServiceIds,
        IReadOnlyList<string> SelectedPackageIds,
        int PaymentType,
        DateTime StartsOn,
        DateTime? EndsOn = null,
        string UserId = "") : ICommand<RecurringBookingTemplateDto>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Frequency)
                .Must(f => Enum.IsDefined(typeof(RecurrenceFrequency), f))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.DayOfWeek)
                .InclusiveBetween(0, 6)
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.TimeOfDay)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(t => TimeOnly.TryParse(t, out _))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.Rooms).GreaterThanOrEqualTo(0).WithMessage(BusinessErrorMessage.InvalidEnumValue);
            RuleFor(x => x.Bathrooms).GreaterThanOrEqualTo(0).WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.SavedAddressId).NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.PaymentType)
                .Must(p => Enum.IsDefined(typeof(PaymentType), p))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x)
                .Must(c => c.SelectedServiceIds.Count > 0 || c.SelectedPackageIds.Count > 0)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNoServicesOrPackages);

            RuleFor(x => x.StartsOn)
                .Must(d => d.Date >= DateTime.UtcNow.Date)
                .WithMessage(BusinessErrorMessage.RecurringTemplateStartsOnInPast);

            When(x => x.EndsOn.HasValue, () =>
            {
                RuleFor(x => x)
                    .Must(c => c.EndsOn!.Value > c.StartsOn)
                    .WithMessage(BusinessErrorMessage.RecurringTemplateEndsOnBeforeStart);
            });
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository) : ICommandHandler<Command, RecurringBookingTemplateDto>
    {
        public async Task<BusinessResult<RecurringBookingTemplateDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Ownership check: the picked SavedAddressId must belong to the
            // calling user. Done here (not in Validator) because it depends
            // on the JWT-enriched UserId.
            var addresses = await savedAddressRepository.GetByUserAsync(command.UserId, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.Id == command.SavedAddressId);
            if (address?.Address == null)
            {
                return BusinessResult.Failure<RecurringBookingTemplateDto>(new Error(
                    nameof(command.SavedAddressId),
                    BusinessErrorMessage.RecurringTemplateSavedAddressNotFound));
            }

            var template = RecurringBookingTemplate.Create(
                userId: command.UserId,
                frequency: (RecurrenceFrequency)command.Frequency,
                dayOfWeek: (System.DayOfWeek)command.DayOfWeek,
                timeOfDay: TimeOnly.Parse(command.TimeOfDay),
                rooms: command.Rooms,
                bathrooms: command.Bathrooms,
                savedAddressId: command.SavedAddressId,
                selectedServiceIds: command.SelectedServiceIds,
                selectedPackageIds: command.SelectedPackageIds,
                paymentType: (PaymentType)command.PaymentType,
                startsOn: command.StartsOn,
                endsOn: command.EndsOn);

            templateRepository.Add(template);

            var line = $"{address.Address.Street}, {address.Address.City} {address.Address.ZipCode}";

            return BusinessResult.Success(new RecurringBookingTemplateDto(
                Id: template.Id,
                Frequency: (int)template.Frequency,
                DayOfWeek: (int)template.DayOfWeek,
                TimeOfDay: template.TimeOfDay.ToString("HH:mm"),
                Rooms: template.Rooms,
                Bathrooms: template.Bathrooms,
                SavedAddressId: template.SavedAddressId,
                AddressLine: line,
                SelectedServiceIds: template.SelectedServiceIds.ToList(),
                SelectedPackageIds: template.SelectedPackageIds.ToList(),
                PaymentType: (int)template.PaymentType,
                StartsOn: template.StartsOn,
                EndsOn: template.EndsOn,
                LastMaterializedFor: template.LastMaterializedFor,
                IsActive: template.IsActive));
        }
    }
}
