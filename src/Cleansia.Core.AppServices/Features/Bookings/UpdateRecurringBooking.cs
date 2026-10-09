using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

[AuditAction("customer.recurring.update", Audience = AuditAudience.Customer, ResourceType = "RecurringBookingTemplate",
    ResourceIdProperty = nameof(UpdateRecurringBooking.Command.TemplateId))]
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
        DateTime? EndsOn = null,
        // Editable, not create-only. Without this the preferred cleaner could be chosen once and
        // never changed or cleared, which is a schedule the customer cannot correct.
        string? PreferredEmployeeId = null,
        DirtinessLevel DirtinessLevel = DirtinessLevel.Normal) : ICommand<RecurringBookingTemplateDto>;

    public class Validator : AbstractValidator<Command>
    {
        private const string OwnedTemplateKey = nameof(UpdateRecurringBooking) + ".OwnedTemplate";
        private readonly IRecurringBookingTemplateRepository _templateRepository;
        private readonly IUserMembershipRepository _userMembershipRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly IOrderRepository _orderRepository;
        private readonly ISavedAddressRepository _savedAddressRepository;
        private readonly ICurrencyResolutionService _currencyResolutionService;
        private readonly ICountryRepository _countryRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly IReceivableRepository _receivableRepository;

        public Validator(
            IRecurringBookingTemplateRepository templateRepository,
            IUserMembershipRepository userMembershipRepository,
            IUserSessionProvider userSessionProvider,
            IOrderRepository orderRepository,
            ISavedAddressRepository savedAddressRepository,
            ICurrencyResolutionService currencyResolutionService,
            ICountryRepository countryRepository,
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            IReceivableRepository receivableRepository)
        {
            _templateRepository = templateRepository;
            _userMembershipRepository = userMembershipRepository;
            _userSessionProvider = userSessionProvider;
            _orderRepository = orderRepository;
            _savedAddressRepository = savedAddressRepository;
            _currencyResolutionService = currencyResolutionService;
            _countryRepository = countryRepository;
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;
            _receivableRepository = receivableRepository;

            // The entitlement link is the LAST link of THIS chain, never a second RuleFor: the
            // class-level default is Continue, so a parallel chain would answer "you need Plus" for a
            // template that does not exist or belongs to someone else, leaking entitlement state onto a
            // path the ownership rule must resolve as not-found.
            RuleFor(x => x.TemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (_, id, context, ct) => await LoadOwnedTemplateAsync(id, context, ct) is not null)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotFound)
                .MustAsync(BeOwnedByCallerAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotOwnedByUser)
                .MustAsync(CallerHasActiveMembershipAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateMembershipRequired);

            // The same gate CreateRecurringBooking applies (ADR-0036 D9). Without it on THIS path
            // the create-time check is a formality: a customer could create a template with no
            // preference and then edit any employee id onto it, withholding every occurrence from
            // the board for a cleaner they simply named.
            When(x => !string.IsNullOrEmpty(x.PreferredEmployeeId), () =>
            {
                RuleFor(x => x)
                    .MustAsync(PreferredEmployeeIsEligibleAsync)
                    .WithMessage(BusinessErrorMessage.PreferredEmployeeNotEligible)
                    .WithName(nameof(Command.PreferredEmployeeId));
            });

            RuleFor(x => x.Frequency)
                .Must(f => Enum.IsDefined(typeof(RecurrenceFrequency), f))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.DayOfWeek)
                .InclusiveBetween(0, 6)
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.DirtinessLevel)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.TimeOfDay)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(t => TimeOnly.TryParse(t, out _))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue)
                .Must(t => BookingPolicy.IsBookableTimeOfDay(TimeOnly.Parse(t)))
                .WithMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);

            RuleFor(x => x.Rooms).GreaterThanOrEqualTo(0).WithMessage(BusinessErrorMessage.InvalidEnumValue)
                .LessThanOrEqualTo(BookingPolicy.MaxRooms).WithMessage(BusinessErrorMessage.OrderSizeExceedsMaximum);
            RuleFor(x => x.Bathrooms).GreaterThanOrEqualTo(0).WithMessage(BusinessErrorMessage.InvalidEnumValue)
                .LessThanOrEqualTo(BookingPolicy.MaxBathrooms).WithMessage(BusinessErrorMessage.OrderSizeExceedsMaximum);

            RuleFor(x => x.SavedAddressId).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(SavedAddressCountryIsServicedAsync).WithMessage(BusinessErrorMessage.CountryNotServiced);

            // Judged on the command alone: the update replaces the selection and the payment type
            // together, so a legacy cash template that needs two cleaners is correctable only by an
            // edit that moves it to card or to a one-cleaner selection.
            RuleFor(x => x.PaymentType)
                .Cascade(CascadeMode.Stop)
                .Must(p => Enum.IsDefined(typeof(PaymentType), p))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue)
                .MustAsync(CashIsAvailableForSelectionAsync)
                .WithMessage(BusinessErrorMessage.OrderCashNotAvailable)
                .When(x => Enum.IsDefined(x.DirtinessLevel), ApplyConditionTo.CurrentValidator)
                .MustAsync(CashLeavesRoomForAnotherOpenBookingAsync)
                .WithMessage(BusinessErrorMessage.OrderCashOpenBookingsLimitReached);

            RuleFor(x => x)
                .MustAsync(OwesNothingAsync)
                .WithMessage(BusinessErrorMessage.OrderUnpaidReceivable);

            RuleFor(x => x)
                .Must(c => c.SelectedServiceIds.Count > 0 || c.SelectedPackageIds.Count > 0)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNoServicesOrPackages);

            // Only the ids the edit ADDS must be active, with CreateRecurringBooking's codes; one the stored
            // template already holds passes even if retired since. → /product/business-rules#deactivated-catalogue
            RuleFor(x => x.SelectedServiceIds)
                .MustAsync((command, ids, context, ct) => AddsOnlyActiveAsync(
                    command.TemplateId, ids, t => t.SelectedServiceIds, _serviceRepository.ExistActiveWithIdsAsync, context, ct))
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .MustAsync((command, ids, context, ct) => AddsOnlyActiveAsync(
                    command.TemplateId, ids, t => t.SelectedPackageIds, _packageRepository.ExistActiveWithIdsAsync, context, ct))
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            RuleFor(x => x.StartsOn)
                .Must(d => !BookingPolicy.IsBeyondBookingHorizon(d, DateTime.UtcNow))
                .WithMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);

            When(x => x.EndsOn.HasValue, () =>
            {
                RuleFor(x => x)
                    .Must(c => c.EndsOn!.Value > c.StartsOn)
                    .WithMessage(BusinessErrorMessage.RecurringTemplateEndsOnBeforeStart);
            });
        }

        /// <summary>
        /// The same two terms as <c>CreateRecurringBooking</c>. The currency is the one the template's
        /// saved address resolves to AFTER this update -- the update rewrites the address from the
        /// command, and every occurrence from now on is priced by that address's country.
        /// </summary>
        private async Task<bool> PreferredEmployeeIsEligibleAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();

            return !string.IsNullOrEmpty(userId)
                && await _orderRepository.UserHasCompletedOrderWithEmployeeAsync(
                    userId, command.PreferredEmployeeId!, cancellationToken)
                && await PreferredEmployeeIsPaidInTheAddressCurrencyAsync(
                    userId, command.SavedAddressId, command.PreferredEmployeeId!, cancellationToken);
        }

        private async Task<bool> PreferredEmployeeIsPaidInTheAddressCurrencyAsync(
            string userId, string savedAddressId, string employeeId, CancellationToken cancellationToken)
        {
            var addresses = await _savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.Id == savedAddressId);
            if (address?.Address is null)
            {
                return true;
            }

            var orderCurrency = await _currencyResolutionService.ResolveCurrencyForCountryAsync(
                address.Address.CountryId, cancellationToken);
            var cleanerCurrency = await _currencyResolutionService.ResolveCurrencyForServingEmployeeAsync(
                userId, employeeId, cancellationToken);
            return cleanerCurrency?.Id == orderCurrency.Id;
        }

        /// <summary>A template the caller does not own holds nothing for them, so every id is asked.</summary>
        private async Task<bool> AddsOnlyActiveAsync(
            string templateId,
            IReadOnlyList<string> ids,
            Func<RecurringBookingTemplate, IReadOnlyCollection<string>> held,
            Func<IEnumerable<string>, CancellationToken, Task<bool>> existActiveWithIdsAsync,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            var template = await LoadOwnedTemplateAsync(templateId, context, cancellationToken);
            var added = template is null ? ids : ids.Except(held(template)).ToList();
            return await existActiveWithIdsAsync(added, cancellationToken);
        }

        private async Task<RecurringBookingTemplate?> LoadOwnedTemplateAsync(
            string id, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId() ?? string.Empty;
            if (context.RootContextData.TryGetValue(OwnedTemplateKey, out var stored)
                && stored is ValueTuple<string, string, RecurringBookingTemplate?> cached
                && cached.Item1 == userId && cached.Item2 == id)
            {
                return cached.Item3;
            }

            var template = await _templateRepository.GetByIdForOwnerAsync(id, userId, cancellationToken);
            // Cache null too, for this validation only; the handler still reads its current tracked row.
            context.RootContextData[OwnedTemplateKey] = (userId, id, template);
            return template;
        }

        private async Task<bool> CashIsAvailableForSelectionAsync(
            Command command, int paymentType, CancellationToken cancellationToken)
            => paymentType != (int)PaymentType.Cash
               || (await RecurringCashEligibility.LoadAsync(
                       _serviceRepository, _packageRepository,
                       command.SelectedServiceIds, command.SelectedPackageIds, cancellationToken))
                   .Allows(command.SelectedServiceIds, command.SelectedPackageIds,
                       command.Rooms, command.Bathrooms, command.DirtinessLevel);

        private async Task<bool> OwesNothingAsync(Command command, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            return string.IsNullOrEmpty(userId)
                || await CustomerCashStanding.OwesNothingAsync(_receivableRepository, userId, cancellationToken);
        }

        private async Task<bool> CashLeavesRoomForAnotherOpenBookingAsync(
            Command command, int paymentType, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            return paymentType != (int)PaymentType.Cash
                || string.IsNullOrEmpty(userId)
                || await CustomerCashStanding.HasRoomForAnotherOpenCashBookingAsync(
                    _orderRepository, userId, cancellationToken);
        }

        private async Task<bool> BeOwnedByCallerAsync(
            Command _, string id, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var template = await LoadOwnedTemplateAsync(id, context, cancellationToken);
            return template != null && template.UserId == userId;
        }

        private async Task<bool> SavedAddressCountryIsServicedAsync(string savedAddressId, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var address = (await _savedAddressRepository.GetByUserAsync(userId, cancellationToken))
                .FirstOrDefault(a => a.Id == savedAddressId)?.Address;
            return address is null || await _countryRepository.IsServicedAsync(address.CountryId, cancellationToken);
        }

        /// <summary>
        /// Gated on an active membership because an update REWRITES every schedule field — it is
        /// authoring a schedule, the same paid capability creation gates. <b>Without this, one paid month
        /// buys a permanently re-specifiable scheduling engine: subscribe, create, cancel, update
        /// forever.</b> Pause, resume and delete stay ungated so a lapsed subscriber can always stop what
        /// is still generating. → /flows/loyalty-and-memberships
        /// </summary>
        private async Task<bool> CallerHasActiveMembershipAsync(string id, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            return await _userMembershipRepository
                .GetEntitledForUserNoTrackingAsync(userId, cancellationToken) is not null;
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IUserSessionProvider userSessionProvider,
        IOperatorTenantResolver operatorTenantResolver,
        ICountryConfigurationRepository countryConfigurationRepository,
        IAuditContext auditContext) : ICommandHandler<Command, RecurringBookingTemplateDto>
    {
        public async Task<BusinessResult<RecurringBookingTemplateDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Existence + ownership of the template are enforced by Validator.
            // The saved-address-belongs-to-user check stays here for now —
            // it's a different entity and would need its own MustAsync rule;
            // tracked as a small follow-up cleanup.
            var userId = userSessionProvider.GetUserId()!;
            var existing = (await templateRepository.GetByIdForOwnerAsync(command.TemplateId, userId, cancellationToken))!;

            var addresses = await savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.Id == command.SavedAddressId);
            if (address?.Address == null)
            {
                return BusinessResult.Failure<RecurringBookingTemplateDto>(new Error(
                    nameof(command.SavedAddressId),
                    BusinessErrorMessage.RecurringTemplateSavedAddressNotFound));
            }

            var before = RecurringTemplateFacts.Of(existing);

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
                endsOn: command.EndsOn,
                preferredEmployeeId: command.PreferredEmployeeId,
                dirtinessLevel: command.DirtinessLevel);
            existing.TenantId = (await operatorTenantResolver.ResolveAsync(address.Address.CountryId, cancellationToken)).OperatorTenantId;

            auditContext.RecordEvidence("RecurringBookingTemplate", existing.Id,
                new RecurringTemplateEvidence(before, RecurringTemplateFacts.Of(existing)));

            var line = $"{address.Address.Street}, {address.Address.City} {address.Address.ZipCode}";
            var marketZone = await TimeZoneResolution.ForMarketAsync(
                countryConfigurationRepository, address.Address.CountryId, cancellationToken);

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
                PreferredEmployeeId: existing.PreferredEmployeeId,
                TimeZoneId: marketZone.Id,
                DirtinessLevel: existing.DirtinessLevel));
        }
    }
}
