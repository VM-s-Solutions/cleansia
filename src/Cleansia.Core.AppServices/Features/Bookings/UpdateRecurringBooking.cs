using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

public class UpdateRecurringBooking
{
    public record Command(
        string TemplateId,
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
        DateTime? EndsOn = null) : ICommand<RecurringBookingTemplateDto>;

    public class Validator : AbstractValidator<Command>
    {
        private readonly IRecurringBookingTemplateRepository _templateRepository;
        private readonly IUserMembershipRepository _userMembershipRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IRecurringBookingTemplateRepository templateRepository,
            IUserMembershipRepository userMembershipRepository,
            IUserSessionProvider userSessionProvider)
        {
            _templateRepository = templateRepository;
            _userMembershipRepository = userMembershipRepository;
            _userSessionProvider = userSessionProvider;

            // The entitlement link is the LAST link of THIS chain, never a second RuleFor: the
            // class-level default is Continue, so a parallel chain would answer "you need Plus" for a
            // template that does not exist or belongs to someone else, leaking entitlement state onto a
            // path the ownership rule must resolve as not-found.
            RuleFor(x => x.TemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_templateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotFound)
                .MustAsync(BeOwnedByCallerAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotOwnedByUser)
                .MustAsync(CallerHasActiveMembershipAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateMembershipRequired);

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

            When(x => x.EndsOn.HasValue, () =>
            {
                RuleFor(x => x)
                    .Must(c => c.EndsOn!.Value > c.StartsOn)
                    .WithMessage(BusinessErrorMessage.RecurringTemplateEndsOnBeforeStart);
            });
        }

        private async Task<bool> BeOwnedByCallerAsync(string id, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var template = await _templateRepository.GetByIdAsync(id, cancellationToken);
            return template != null && template.UserId == userId;
        }

        /// <summary>
        /// <c>UpdateSchedule</c> rewrites every schedule field and clears the materialisation watermark,
        /// so an update is authoring a schedule — the same paid Cleansia Plus capability
        /// <c>CreateRecurringBooking</c> gates. Without this, one paid month buys a permanently
        /// re-specifiable scheduling engine: subscribe, create, cancel, then update forever.
        /// <para>The owner ruling that a lapsed membership does not stop a schedule (Q-PLUS-04)
        /// PRESERVES an existing schedule; it does not license writing a new one. Pause, resume and
        /// delete stay ungated so a lapsed subscriber can always stop what is still generating.</para>
        /// <para>The predicate is the shared one — <c>Status == Active &amp;&amp; CurrentPeriodEnd &gt;
        /// UtcNow</c> — so a trialing member (Stripe's <c>trialing</c> collapses to Active) keeps this
        /// perk; only the metered express waiver is withheld during a trial.</para>
        /// </summary>
        private async Task<bool> CallerHasActiveMembershipAsync(string id, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            return await _userMembershipRepository
                .GetActiveForUserNoTrackingAsync(userId, cancellationToken) is not null;
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, RecurringBookingTemplateDto>
    {
        public async Task<BusinessResult<RecurringBookingTemplateDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Existence + ownership of the template are enforced by Validator.
            // The saved-address-belongs-to-user check stays here for now —
            // it's a different entity and would need its own MustAsync rule;
            // tracked as a small follow-up cleanup.
            var userId = userSessionProvider.GetUserId()!;
            var existing = (await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken))!;

            var addresses = await savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.Id == command.SavedAddressId);
            if (address?.Address == null)
            {
                return BusinessResult.Failure<RecurringBookingTemplateDto>(new Error(
                    nameof(command.SavedAddressId),
                    BusinessErrorMessage.RecurringTemplateSavedAddressNotFound));
            }

            // Mutate in place so the template's Id survives an update. Clients
            // caching the template by id (mobile list, web facade) stay valid.
            existing.UpdateSchedule(
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

            var line = $"{address.Address.Street}, {address.Address.City} {address.Address.ZipCode}";

            return BusinessResult.Success(new RecurringBookingTemplateDto(
                Id: existing.Id,
                Frequency: (int)existing.Frequency,
                DayOfWeek: (int)existing.DayOfWeek,
                TimeOfDay: existing.TimeOfDay.ToString("HH:mm"),
                Rooms: existing.Rooms,
                Bathrooms: existing.Bathrooms,
                SavedAddressId: existing.SavedAddressId,
                AddressLine: line,
                SelectedServiceIds: existing.SelectedServiceIds.ToList(),
                SelectedPackageIds: existing.SelectedPackageIds.ToList(),
                PaymentType: (int)existing.PaymentType,
                StartsOn: existing.StartsOn,
                EndsOn: existing.EndsOn,
                LastMaterializedFor: existing.LastMaterializedFor,
                IsActive: existing.IsActive,
                PreferredEmployeeId: existing.PreferredEmployeeId));
        }
    }
}
