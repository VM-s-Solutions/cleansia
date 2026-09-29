using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

[AuditAction("customer.recurring.create", Audience = AuditAudience.Customer, ResourceType = "RecurringBookingTemplate")]
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
        string? PreferredEmployeeId = null,
        // The same tick a one-off booking asks for, and on the same terms: required while either legal
        // consent is not an acceptance of the text in force for the saved address's market.
        bool? TermsAccepted = null) : ICommand<RecurringBookingTemplateDto>;

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly ISavedAddressRepository _savedAddressRepository;
        private readonly ICurrencyResolutionService _currencyResolutionService;
        private readonly ICountryRepository _countryRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly IUserConsentRepository _userConsentRepository;
        private readonly ILegalDocumentResolver _legalDocumentResolver;

        public Validator(
            IOrderRepository orderRepository,
            IUserSessionProvider userSessionProvider,
            ISavedAddressRepository savedAddressRepository,
            ICurrencyResolutionService currencyResolutionService,
            ICountryRepository countryRepository,
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            IUserConsentRepository userConsentRepository,
            ILegalDocumentResolver legalDocumentResolver)
        {
            _orderRepository = orderRepository;
            _userSessionProvider = userSessionProvider;
            _savedAddressRepository = savedAddressRepository;
            _currencyResolutionService = currencyResolutionService;
            _countryRepository = countryRepository;
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;
            _userConsentRepository = userConsentRepository;
            _legalDocumentResolver = legalDocumentResolver;

            RuleFor(x => x.TermsAccepted)
                .MustAsync((command, termsAccepted, cancellationToken) =>
                    AssertedOrAlreadyConsentedAsync(command, termsAccepted, cancellationToken))
                .WithMessage(BusinessErrorMessage.TermsNotAccepted)
                .WithErrorCode(nameof(Command.TermsAccepted));

            RuleFor(x => x.Frequency)
                .Must(f => Enum.IsDefined(typeof(RecurrenceFrequency), f))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.DayOfWeek)
                .InclusiveBetween(0, 6)
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

            RuleFor(x => x.SavedAddressId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(SavedAddressCountryIsServicedAsync)
                .WithMessage(BusinessErrorMessage.CountryNotServiced);

            RuleFor(x => x.PaymentType)
                .Cascade(CascadeMode.Stop)
                .Must(p => Enum.IsDefined(typeof(PaymentType), p))
                .WithMessage(BusinessErrorMessage.InvalidEnumValue)
                .MustAsync(CashIsAvailableForSelectionAsync)
                .WithMessage(BusinessErrorMessage.OrderCashNotAvailable);

            RuleFor(x => x)
                .Must(c => c.SelectedServiceIds.Count > 0 || c.SelectedPackageIds.Count > 0)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNoServicesOrPackages);

            RuleFor(x => x.StartsOn)
                .Cascade(CascadeMode.Stop)
                .Must(d => d.Date >= DateTime.UtcNow.Date)
                .WithMessage(BusinessErrorMessage.RecurringTemplateStartsOnInPast)
                .Must(d => !BookingPolicy.IsBeyondBookingHorizon(d, DateTime.UtcNow))
                .WithMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);

            When(x => x.EndsOn.HasValue, () =>
            {
                RuleFor(x => x)
                    .Must(c => c.EndsOn!.Value > c.StartsOn)
                    .WithMessage(BusinessErrorMessage.RecurringTemplateEndsOnBeforeStart);
            });

            When(x => !string.IsNullOrEmpty(x.PreferredEmployeeId), () =>
            {
                RuleFor(x => x)
                    .MustAsync(PreferredEmployeeIsEligibleAsync)
                    .WithMessage(BusinessErrorMessage.PreferredEmployeeNotEligible)
                    .WithName(nameof(Command.PreferredEmployeeId));
            });
        }

        /// <summary>
        /// The same gate the one-off path applies (<c>CreateOrder</c>, ADR-0036 D9): without it
        /// <c>PreferredEmployeeId</c> is any employee id a client sends, and the hold would let a
        /// customer withhold every occurrence from the board and hand it to a cleaner they name. It runs
        /// ONCE, here — the relationship is monotone and this is the only gate needing the caller's
        /// identity, while everything that can lapse is re-run per occurrence by the hold resolver, where
        /// a "no" costs the perk and never the cleaning.
        ///
        /// <para>The second term is the currency: every occurrence is priced in the currency of the saved
        /// address's country, and a cleaner paid in another could never take one. A saved address the
        /// handler will refuse passes this term untouched so its own not-found answer is the one given.</para>
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
            var address = await FindSavedAddressAsync(userId, savedAddressId, cancellationToken);
            if (address is null)
            {
                return true;
            }

            var orderCurrency = await _currencyResolutionService.ResolveCurrencyForCountryAsync(
                address.CountryId, cancellationToken);
            var cleanerCurrency = await _currencyResolutionService.ResolveCurrencyForServingEmployeeAsync(
                userId, employeeId, cancellationToken);
            return cleanerCurrency?.Id == orderCurrency.Id;
        }

        /// <summary>
        /// The same answer the one-off booking gets from <c>OrderAddressResolver</c>: a saved address in
        /// a country nobody operates — delisted, or a deactivated company's market — books nothing.
        /// A saved address the handler will refuse passes so its own not-found answer is the one given.
        /// </summary>
        private async Task<bool> SavedAddressCountryIsServicedAsync(string savedAddressId, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return true;
            }

            var address = await FindSavedAddressAsync(userId, savedAddressId, cancellationToken);
            return address is null
                || await _countryRepository.IsServicedAsync(address.CountryId, cancellationToken);
        }

        /// <summary>
        /// A schedule is a new booking (owner ruling 2026-09-28): asked again like a one-off order whenever
        /// the customer's acceptance is not of the texts in force for the saved address's market. The
        /// orders it goes on to create run on the version accepted here. A saved address the handler will
        /// refuse passes so its own not-found answer is the one given.
        /// </summary>
        private async Task<bool> AssertedOrAlreadyConsentedAsync(
            Command command, bool? termsAccepted, CancellationToken cancellationToken)
        {
            if (termsAccepted == true)
            {
                return true;
            }

            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var address = await FindSavedAddressAsync(userId, command.SavedAddressId, cancellationToken);
            return address is null || await CustomerLegalConsents.CoverTextsInForceAsync(
                _userConsentRepository, _legalDocumentResolver, userId, address.CountryId, cancellationToken);
        }

        private async Task<bool> CashIsAvailableForSelectionAsync(
            Command command, int paymentType, CancellationToken cancellationToken)
            => paymentType != (int)PaymentType.Cash
               || (await RecurringCashEligibility.LoadAsync(
                       _serviceRepository, _packageRepository,
                       command.SelectedServiceIds, command.SelectedPackageIds, cancellationToken))
                   .Allows(command.SelectedServiceIds, command.SelectedPackageIds,
                       command.Rooms, command.Bathrooms, DirtinessLevel.Normal);

        private async Task<Address?> FindSavedAddressAsync(string userId, string savedAddressId, CancellationToken cancellationToken)
        {
            var addresses = await _savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            return addresses.FirstOrDefault(a => a.Id == savedAddressId)?.Address;
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        ISavedAddressRepository savedAddressRepository,
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider,
        IOperatorTenantResolver operatorTenantResolver,
        ICountryConfigurationRepository countryConfigurationRepository,
        IConsentService consentService,
        IUserConsentRepository userConsentRepository,
        ILegalDocumentResolver legalDocumentResolver,
        IAuditContext auditContext) : ICommandHandler<Command, RecurringBookingTemplateDto>
    {
        public async Task<BusinessResult<RecurringBookingTemplateDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            // Recurring schedules are a paid Cleansia Plus perk, and CanManageRecurringBookings is
            // held by every signed-in customer — without this the perk is free to anyone who calls
            // the endpoint directly. The client-side gates are UX, not the control.
            var membership = await userMembershipRepository
                .GetEntitledForUserNoTrackingAsync(userId, cancellationToken);
            if (membership is null)
            {
                return BusinessResult.Failure<RecurringBookingTemplateDto>(new Error(
                    nameof(userId),
                    BusinessErrorMessage.RecurringTemplateMembershipRequired));
            }

            var addresses = await savedAddressRepository.GetByUserAsync(userId, cancellationToken);
            var address = addresses.FirstOrDefault(a => a.Id == command.SavedAddressId);
            if (address?.Address == null)
            {
                return BusinessResult.Failure<RecurringBookingTemplateDto>(new Error(
                    nameof(command.SavedAddressId),
                    BusinessErrorMessage.RecurringTemplateSavedAddressNotFound));
            }

            var template = RecurringBookingTemplate.Create(
                userId: userId,
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
                preferredEmployeeId: command.PreferredEmployeeId);

            template.TenantId = (await operatorTenantResolver.ResolveAsync(address.Address.CountryId, cancellationToken)).OperatorTenantId;
            templateRepository.Add(template);

            var (termsVersionAccepted, privacyVersionAccepted) = await CustomerLegalConsents.RecordAsync(
                consentService, userConsentRepository, legalDocumentResolver,
                userId, command.TermsAccepted, address.Address.CountryId, cancellationToken);

            auditContext.RecordEvidence("RecurringBookingTemplate", template.Id,
                new RecurringTemplateEvidence(
                    Before: null,
                    After: RecurringTemplateFacts.Of(template),
                    TermsAccepted: command.TermsAccepted,
                    TermsVersionAccepted: termsVersionAccepted,
                    PrivacyVersionAccepted: privacyVersionAccepted));

            var line = $"{address.Address.Street}, {address.Address.City} {address.Address.ZipCode}";
            var marketZone = await TimeZoneResolution.ForMarketAsync(
                countryConfigurationRepository, address.Address.CountryId, cancellationToken);

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
                IsActive: template.IsActive,
                PreferredEmployeeId: template.PreferredEmployeeId,
                TimeZoneId: marketZone.Id));
        }
    }
}
