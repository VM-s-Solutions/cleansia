using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Shared fixture for the CreateOrder validator + handler characterization suite. Centralizes the
/// canonical valid <see cref="CreateOrder.Command"/> and the matching pricing snapshot so each
/// case mutates exactly one input away from the happy path.
/// </summary>
internal static class CreateOrderTestData
{
    public const string ServiceId = "service-1";
    public const string PackageId = "package-1";
    public const string CurrencyId = "czk";

    /// <summary>
    /// The platform default currency with <see cref="CurrencyId"/> as its id, so a command that names
    /// no currency, a pay config created with <see cref="CurrencyId"/>, and a price row keyed on this
    /// instance all agree on ONE id. The pay gate and the price lookup both filter on it now; two
    /// <c>Currency.Create</c> calls are two different currencies and would find each other's rows
    /// missing.
    /// </summary>
    public static Currency DefaultCurrency()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        return currency;
    }
    public const decimal MatchingTotalPrice = 1500m;

    public static AddressDto InlineAddress(string? countryId = "cz") =>
        new(
            Street: "123 Main Street",
            City: "Prague",
            ZipCode: "11000",
            CountryId: countryId,
            State: null);

    /// <summary>
    /// A fully valid command: a future cleaning date above the lead time, a positive total that
    /// matches <see cref="MatchingPricing"/>, exactly the inline address, and one of each catalog id.
    /// </summary>
    public static CreateOrder.Command ValidCommand(
        AddressDto? customerAddress = null,
        string? savedAddressId = null,
        PaymentType paymentType = PaymentType.Cash,
        DateTime? cleaningDate = null,
        decimal? totalPrice = null,
        IEnumerable<string>? serviceIds = null,
        IEnumerable<string>? packageIds = null,
        string? preferredEmployeeId = null,
        string? promoCode = null,
        string? referralCode = null,
        string? specialInstructions = null,
        string? accessInstructions = null) =>
        new(
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            CustomerAddress: savedAddressId == null ? customerAddress ?? InlineAddress() : null,
            SavedAddressId: savedAddressId,
            SelectedPackageIds: packageIds ?? new[] { PackageId },
            SelectedServiceIds: serviceIds ?? new[] { ServiceId },
            Rooms: 2,
            Bathrooms: 1,
            Extras: new Dictionary<string, bool>(),
            CleaningDate: cleaningDate ?? DateTime.UtcNow.AddDays(3),
            PaymentType: paymentType,
            CurrencyId: CurrencyId,
            TotalPrice: totalPrice ?? MatchingTotalPrice,
            Language: Constants.Language.English,
            PromoCode: promoCode,
            ReferralCode: referralCode,
            PreferredEmployeeId: preferredEmployeeId,
            SpecialInstructions: specialInstructions,
            AccessInstructions: accessInstructions);

    public static OrderPricingResult MatchingPricing(decimal totalPrice = MatchingTotalPrice) =>
        new(
            TotalPrice: totalPrice,
            CurrencyId: CurrencyId,
            CurrencyCode: "CZK",
            ServicesSubtotal: 1000m,
            PackagesSubtotal: 500m,
            ExtrasSubtotal: 0m,
            ExpressSurchargeApplied: false,
            ExpressSurchargeAmount: 0m);
}
