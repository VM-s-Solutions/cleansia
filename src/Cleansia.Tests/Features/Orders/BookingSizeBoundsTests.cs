using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Repositories;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Cleansia.Tests.Features.Orders;

public class BookingSizeBoundsTests
{
    public static IEnumerable<object[]> Sizes()
    {
        foreach (var endpoint in new[] { "create", "quote", "plus", "recurring-create", "recurring-update" })
        {
            yield return [endpoint, 8, 4, true];
            yield return [endpoint, 0, 0, true];
            yield return [endpoint, 9, 4, false];
            yield return [endpoint, 8, 5, false];
            yield return [endpoint, int.MaxValue, int.MaxValue, false];
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task A_Basket_Cannot_Bypass_The_Size_Limit_Through_Another_Entry_Point(
        string endpoint, int rooms, int bathrooms, bool accepted)
    {
        var result = endpoint switch
        {
            "create" => await ValidateSize(new CreateOrder.Validator(
                    Mock.Of<IPackageRepository>(), Mock.Of<IServiceRepository>(), Mock.Of<IOrderPricingCalculator>(),
                    Mock.Of<IOrderRepository>(), Mock.Of<IUserMembershipRepository>(), Mock.Of<IUserSessionProvider>(),
                    Mock.Of<IEmployeePayConfigRepository>(), Mock.Of<ICurrencyRepository>(), Mock.Of<IOrderAddressResolver>(),
                    Mock.Of<ICurrencyResolutionService>(), Mock.Of<IServicePriceRepository>(), Mock.Of<IPackagePriceRepository>(),
                    Mock.Of<IPromoCodeService>(), Mock.Of<IOperatorTenantResolver>(), Mock.Of<ITenantProvider>(),
                    Mock.Of<IUserConsentRepository>(), Mock.Of<ILanguageRepository>()),
                CreateOrderTestData.ValidCommand() with { Rooms = rooms, Bathrooms = bathrooms }),
            "quote" => await ValidateSize(new QuoteOrder.Validator(
                    Mock.Of<IServiceRepository>(), Mock.Of<IPackageRepository>(), Mock.Of<ICurrencyRepository>(),
                    Mock.Of<ICountryRepository>(), Mock.Of<ICurrencyResolutionService>(),
                    Mock.Of<IServicePriceRepository>(), Mock.Of<IPackagePriceRepository>()),
                new QuoteOrder.Command([], [], rooms, bathrooms, null)),
            "plus" => await ValidateSize(new QuotePlusSavings.Validator(
                    Mock.Of<IServiceRepository>(), Mock.Of<IPackageRepository>(), Mock.Of<ICurrencyRepository>(),
                    Mock.Of<ICountryRepository>(), Mock.Of<ICurrencyResolutionService>(),
                    Mock.Of<IServicePriceRepository>(), Mock.Of<IPackagePriceRepository>()),
                new QuotePlusSavings.Query([], [], rooms, bathrooms, "plus")),
            "recurring-create" => await ValidateSize(new CreateRecurringBooking.Validator(
                    Mock.Of<IOrderRepository>(), Mock.Of<IUserSessionProvider>(), Mock.Of<ISavedAddressRepository>(),
                    Mock.Of<ICurrencyResolutionService>(), Mock.Of<ICountryRepository>(),
                    Bookings.CatalogueDoubles.Services(), Bookings.CatalogueDoubles.Packages()),
                new CreateRecurringBooking.Command(1, 1, "10:00", rooms, bathrooms, "address", [], [], 1, DateTime.UtcNow)),
            "recurring-update" => await ValidateSize(new UpdateRecurringBooking.Validator(
                    Mock.Of<IRecurringBookingTemplateRepository>(), Mock.Of<IUserMembershipRepository>(),
                    Mock.Of<IUserSessionProvider>(), Mock.Of<IOrderRepository>(), Mock.Of<ISavedAddressRepository>(),
                    Mock.Of<ICurrencyResolutionService>(), Mock.Of<ICountryRepository>(),
                    Bookings.CatalogueDoubles.Services(), Bookings.CatalogueDoubles.Packages()),
                new UpdateRecurringBooking.Command("template", 1, 1, "10:00", rooms, bathrooms,
                    "address", [], [], 1, DateTime.UtcNow)),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
        };

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            Assert.All(result.Errors, failure => Assert.Equal("order.size_exceeds_maximum", failure.ErrorMessage));
            Assert.Equal(rooms > 8, result.Errors.Any(f => f.PropertyName == "Rooms"));
            Assert.Equal(bathrooms > 4, result.Errors.Any(f => f.PropertyName == "Bathrooms"));
        }
    }

    private static Task<ValidationResult> ValidateSize<T>(IValidator<T> validator, T request) =>
        validator.ValidateAsync(request, options => options.IncludeProperties("Rooms", "Bathrooms"));
}
