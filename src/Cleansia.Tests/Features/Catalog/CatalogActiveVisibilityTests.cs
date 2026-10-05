using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.Tests.Features.Orders;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// Visibility contract over a REAL <see cref="CleansiaDbContext"/> (SQLite in-memory, real
/// repositories): a deactivated service/package disappears from the customer-facing overview
/// (the booking wizard catalog) while the row itself survives, and the admin list can target it
/// via the IsActive filter (S10 — no global IsActive filter, admins see all by default). A deactivated
/// service inside an ACTIVE package stays listed in that package, because an order with it books it.
/// A customer cannot select a deactivated entry by id either, while a schedule that already holds one
/// keeps booking it and an edit of that schedule may keep it.
/// </summary>
public sealed class CatalogActiveVisibilityTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CatalogActiveVisibilityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private static CurrencyResolutionService Markets(CleansiaDbContext ctx) =>
        new(new EmployeeRepository(ctx), new CountryConfigurationRepository(ctx), new CurrencyRepository(ctx));

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new DefaultTenantProvider());
    }

    private async Task<(string ActiveServiceId, string RetiredServiceId, string ActivePackageId, string RetiredPackageId)> SeedAsync()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);

        var category = ServiceCategory.Create("cat-1", "Category", "seeded");
        var activeService = Service.Create(category.Id, "Active Service", "seeded");
        var retiredService = Service.Create(category.Id, "Retired Service", "seeded");
        retiredService.Deactivated("admin-1", DateTimeOffset.UtcNow);

        var activePackage = Package.Create("Active Package", "seeded");
        var retiredPackage = Package.Create("Retired Package", "seeded");
        retiredPackage.Deactivated("admin-1", DateTimeOffset.UtcNow);

        ctx.ServiceCategories.Add(category);
        ctx.Services.AddRange(activeService, retiredService);
        ctx.Packages.AddRange(activePackage, retiredPackage);

        // Bookable is IsActive AND quotable, so the entries this suite expects to SEE need a
        // platform-wide pay config; without one they would be withheld for the other reason and the
        // deactivation assertions would pass vacuously.
        var currency = Currency.Create("CZK", "Kc", "Czech Koruna");
        currency.SetAsDefault(true);
        ctx.Currencies.Add(currency);

        // ...and a PRICE in that currency, for the same reason as the pay config beside it. Bookable is
        // IsActive AND quotable AND priced; an unpriced fixture would withhold every entry and the
        // deactivation assertions would pass without the deactivation doing any of the work. Default,
        // because the customer overview prices in the platform default currency.
        ctx.ServicePrices.AddRange(
            ServicePrice.Create(activeService.Id, currency.Id, 500m, 100m),
            ServicePrice.Create(retiredService.Id, currency.Id, 500m, 100m));
        ctx.PackagePrices.AddRange(
            PackagePrice.Create(activePackage.Id, currency.Id, 1000m),
            PackagePrice.Create(retiredPackage.Id, currency.Id, 1000m));
        ctx.EmployeePayConfigs.AddRange(
            EmployeePayConfig.CreateForService(activeService.Id, 500m, currency.Id),
            EmployeePayConfig.CreateForService(retiredService.Id, 500m, currency.Id),
            EmployeePayConfig.CreateForPackage(activePackage.Id, 250m, currency.Id),
            EmployeePayConfig.CreateForPackage(retiredPackage.Id, 250m, currency.Id));

        await ctx.CommitAsync(CancellationToken.None);

        return (activeService.Id, retiredService.Id, activePackage.Id, retiredPackage.Id);
    }

    [Fact]
    public async Task DeactivatedService_IsHiddenFromCustomerOverview_ButRowSurvives()
    {
        var (activeServiceId, retiredServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var overview = (await new GetServiceOverview.Handler(
                new ServiceRepository(ctx),
                new ServicePriceRepository(ctx),
                Markets(ctx),
                new EmployeePayConfigRepository(ctx),
                new CountryRepository(ctx))
            .Handle(new GetServiceOverview.Request(), CancellationToken.None)).ToList();

        Assert.Contains(overview, s => s.Id == activeServiceId);
        Assert.DoesNotContain(overview, s => s.Id == retiredServiceId);
        Assert.NotNull(await ctx.Services.FindAsync(retiredServiceId));
    }

    [Fact]
    public async Task DeactivatedPackage_IsHiddenFromCustomerOverview_ButRowSurvives()
    {
        var (_, _, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var overview = (await new GetPackageOverview.Handler(
                new PackageRepository(ctx),
                new PackagePriceRepository(ctx),
                Markets(ctx),
                new EmployeePayConfigRepository(ctx),
                new CountryRepository(ctx))
            .Handle(new GetPackageOverview.Request(), CancellationToken.None)).ToList();

        Assert.Contains(overview, p => p.Id == activePackageId);
        Assert.DoesNotContain(overview, p => p.Id == retiredPackageId);
        Assert.NotNull(await ctx.Packages.FindAsync(retiredPackageId));
    }

    /// <summary>
    /// A package's included services on the overview are exactly the ones an order with that package
    /// books — a deactivated service among them. Deactivating a service hides it from the service list
    /// but leaves it inside every package that includes it, and no order step asks: the factory writes
    /// a line for every included service, the job is timed by all of them, and the order detail the
    /// customer and the cleaner read lists them all. Filtering the overview alone would show a package
    /// without a service the cleaner is then sent to do. Equality, not containment, so the two sides
    /// may change only together.
    /// </summary>
    [Fact]
    public async Task DeactivatedServiceInsideAnActivePackage_IsListedByTheOverview_ExactlyAsAnOrderBooksIt()
    {
        await SeedAsync();

        string bundleId;
        string stillOfferedId;
        Dictionary<string, int> minutes;
        await using (var seed = NewContext())
        {
            var currency = await seed.Currencies.SingleAsync();
            var categoryId = (await seed.ServiceCategories.SingleAsync()).Id;
            var windows = Service.Create(categoryId, "Windows", "seeded", estimatedTime: 60);
            var oven = Service.Create(categoryId, "Oven", "seeded", estimatedTime: 45);
            oven.Deactivated("admin-1", DateTimeOffset.UtcNow);
            var bundle = Package.Create("Bundle", "seeded").AddService(windows).AddService(oven);

            seed.Services.AddRange(windows, oven);
            seed.Packages.Add(bundle);
            seed.PackagePrices.Add(PackagePrice.Create(bundle.Id, currency.Id, 1000m));
            seed.EmployeePayConfigs.Add(EmployeePayConfig.CreateForPackage(bundle.Id, 250m, currency.Id));
            await seed.CommitAsync(CancellationToken.None);

            bundleId = bundle.Id;
            stillOfferedId = windows.Id;
            minutes = new() { [windows.Id] = 60, [oven.Id] = 45 };
        }

        List<string> listed;
        await using (var ctx = NewContext())
        {
            var overview = await new GetPackageOverview.Handler(
                    new PackageRepository(ctx),
                    new PackagePriceRepository(ctx),
                    Markets(ctx),
                    new EmployeePayConfigRepository(ctx),
                    new CountryRepository(ctx))
                .Handle(new GetPackageOverview.Request(), CancellationToken.None);
            listed = overview.Single(p => p.Id == bundleId).IncludedServices
                .Select(s => s.ServiceId).Order().ToList();
        }

        await using (var ctx = NewContext())
        {
            var order = await OrderFactoryOver(ctx).CreateAsync(
                new CreateOrderInput(
                    UserId: null,
                    CustomerName: "Test Customer",
                    CustomerEmail: "customer@example.com",
                    CustomerPhone: "+420123456789",
                    Address: AddressMockFactory.Generate(),
                    Rooms: 2,
                    Bathrooms: 1,
                    SelectedExtraSlugs: [],
                    CleaningDate: DateTime.UtcNow.AddDays(3),
                    PaymentType: PaymentType.Card,
                    Currency: await ctx.Currencies.SingleAsync(),
                    SelectedServiceIds: [],
                    SelectedPackageIds: [bundleId],
                    RawSubtotal: 1000m,
                    NowUtc: DateTime.UtcNow,
                    ReservedExpressWaiver: null,
                    OperatorTenantId: null),
                CancellationToken.None);
            var booked = order.SelectedPackages.Single();

            Assert.Contains(stillOfferedId, listed);
            Assert.Equal(listed, booked.IncludedServiceLines.Select(l => l.ServiceId).Order().ToList());
            Assert.Equal(
                listed,
                booked.Package.MapToDetails("CZK", booked.LineTotal).IncludedServiceItems
                    .Select(i => i.Id).Order().ToList());
            Assert.Equal(listed.Sum(id => minutes[id]), order.EstimatedTime);
        }
    }

    /// <summary>
    /// The row a deactivation leaves behind still EXISTS, which is why the selection gates ask for an
    /// active one: existence alone let a stale client select what no catalogue shows any more.
    /// </summary>
    [Fact]
    public async Task ExistActiveWithIds_RefusesARetiredEntry_ThatExistWithIdsStillFinds()
    {
        var (activeServiceId, retiredServiceId, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var services = new ServiceRepository(ctx);
        var packages = new PackageRepository(ctx);

        Assert.True(await services.ExistWithIdsAsync([retiredServiceId], CancellationToken.None));
        Assert.False(await services.ExistActiveWithIdsAsync([retiredServiceId], CancellationToken.None));
        Assert.False(await services.ExistActiveWithIdsAsync([activeServiceId, retiredServiceId], CancellationToken.None));
        Assert.True(await services.ExistActiveWithIdsAsync([activeServiceId, activeServiceId], CancellationToken.None));
        Assert.False(await services.ExistActiveWithIdsAsync(["never-existed"], CancellationToken.None));
        Assert.True(await services.ExistActiveWithIdsAsync([], CancellationToken.None));

        Assert.True(await packages.ExistWithIdsAsync([retiredPackageId], CancellationToken.None));
        Assert.False(await packages.ExistActiveWithIdsAsync([retiredPackageId], CancellationToken.None));
        Assert.True(await packages.ExistActiveWithIdsAsync([activePackageId], CancellationToken.None));
    }

    /// <summary>
    /// A schedule is a new booking: it may select only what CreateOrder accepts, with the same codes. It
    /// used to take any id at all, a deactivated one included, and book it every week.
    /// </summary>
    [Fact]
    public async Task A_Schedule_Cannot_Select_A_Retired_Service_Or_Package_By_Id()
    {
        var (activeServiceId, retiredServiceId, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var validator = ScheduleValidatorOver(ctx);

        var retired = await validator.ValidateAsync(Schedule([retiredServiceId], [retiredPackageId]));
        var active = await validator.ValidateAsync(Schedule([activeServiceId], [activePackageId]));

        Assert.Equal(
            new[]
            {
                (nameof(CreateRecurringBooking.Command.SelectedPackageIds), BusinessErrorMessage.InvalidSelectedPackage),
                (nameof(CreateRecurringBooking.Command.SelectedServiceIds), BusinessErrorMessage.InvalidSelectedServices),
            },
            retired.Errors.Select(e => (e.PropertyName, e.ErrorMessage)).Order());
        Assert.True(active.IsValid, string.Join("; ", active.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// An edit is asked only what it ADDS, with the codes a new schedule gets: it used to take any id at
    /// all, a retired one or one that never existed included.
    /// </summary>
    [Fact]
    public async Task A_Schedule_Edit_Cannot_Add_A_Retired_Or_Unknown_Service_Or_Package()
    {
        var (activeServiceId, retiredServiceId, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var validator = ScheduleEditValidatorOver(ctx, HeldTemplate([activeServiceId], [activePackageId]));

        var retired = await validator.ValidateAsync(
            ScheduleEdit([activeServiceId, retiredServiceId], [activePackageId, retiredPackageId]));
        var unknown = await validator.ValidateAsync(ScheduleEdit(["never-existed"], ["never-existed"]));

        var refusals = new[]
        {
            (nameof(UpdateRecurringBooking.Command.SelectedPackageIds), BusinessErrorMessage.InvalidSelectedPackage),
            (nameof(UpdateRecurringBooking.Command.SelectedServiceIds), BusinessErrorMessage.InvalidSelectedServices),
        };
        Assert.Equal(refusals, retired.Errors.Select(e => (e.PropertyName, e.ErrorMessage)).Order());
        Assert.Equal(refusals, unknown.Errors.Select(e => (e.PropertyName, e.ErrorMessage)).Order());
    }

    /// <summary>
    /// The other side of the same rule: an entry retired after the schedule took it stays selectable on
    /// that schedule, so editing its time does not cost the customer what it keeps booking.
    /// </summary>
    [Fact]
    public async Task A_Schedule_Edit_Keeps_A_Retired_Entry_The_Schedule_Holds_And_May_Add_An_Active_One()
    {
        var (activeServiceId, retiredServiceId, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var validator = ScheduleEditValidatorOver(ctx, HeldTemplate([retiredServiceId], [retiredPackageId]));

        var result = await validator.ValidateAsync(
            ScheduleEdit([retiredServiceId, activeServiceId], [retiredPackageId, activePackageId]));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// The line's other side: the materialiser hands a template's ids to the factory without asking the
    /// catalogue again, so a schedule created before the deactivation keeps booking the entry.
    /// </summary>
    [Fact]
    public async Task The_Factory_Still_Books_A_Retired_Service_A_Schedule_Already_Holds()
    {
        var (_, retiredServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var order = await OrderFactoryOver(ctx).CreateAsync(
            new CreateOrderInput(
                UserId: null,
                CustomerName: "Test Customer",
                CustomerEmail: "customer@example.com",
                CustomerPhone: "+420123456789",
                Address: AddressMockFactory.Generate(),
                Rooms: 2,
                Bathrooms: 1,
                SelectedExtraSlugs: [],
                CleaningDate: DateTime.UtcNow.AddDays(3),
                PaymentType: PaymentType.Card,
                Currency: await ctx.Currencies.SingleAsync(),
                SelectedServiceIds: [retiredServiceId],
                SelectedPackageIds: [],
                RawSubtotal: 800m,
                NowUtc: DateTime.UtcNow,
                ReservedExpressWaiver: null,
                OperatorTenantId: null),
            CancellationToken.None);

        Assert.Equal(retiredServiceId, Assert.Single(order.SelectedServices).ServiceId);
    }

    [Fact]
    public async Task AdminServiceFilter_IsActiveFalse_ListsOnlyRetired_NullListsAll()
    {
        var (activeServiceId, retiredServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var repository = new ServiceRepository(ctx);

        var retired = await repository
            .GetFiltered(ServiceSpecification.Create(isActive: false).SatisfiedBy())
            .ToListAsync();
        Assert.Single(retired);
        Assert.Equal(retiredServiceId, retired[0].Id);

        var all = await repository
            .GetFiltered(ServiceSpecification.Create().SatisfiedBy())
            .ToListAsync();
        Assert.Contains(all, s => s.Id == activeServiceId);
        Assert.Contains(all, s => s.Id == retiredServiceId);
    }

    [Fact]
    public async Task AdminPackageFilter_IsActiveFalse_ListsOnlyRetired_NullListsAll()
    {
        var (_, _, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var repository = new PackageRepository(ctx);

        var retired = await repository
            .GetFiltered(PackageSpecification.Create(isActive: false).SatisfiedBy())
            .ToListAsync();
        Assert.Single(retired);
        Assert.Equal(retiredPackageId, retired[0].Id);

        var all = await repository
            .GetFiltered(PackageSpecification.Create().SatisfiedBy())
            .ToListAsync();
        Assert.Contains(all, p => p.Id == activePackageId);
        Assert.Contains(all, p => p.Id == retiredPackageId);
    }

    /// <summary>
    /// The real factory reading the real catalogue. Everything else is a double: the order is never
    /// saved, and nothing else the factory reads changes which services it books.
    /// </summary>
    private static OrderFactory OrderFactoryOver(CleansiaDbContext ctx) =>
        new(
            Mock.Of<IOrderRepository>(),
            new ServiceRepository(ctx),
            new PackageRepository(ctx),
            new ExtraRepository(ctx),
            new ServicePriceRepository(ctx),
            new PackagePriceRepository(ctx),
            new ExtraPriceRepository(ctx),
            new EmployeePayConfigRepository(ctx),
            Mock.Of<ICompanyInfoRepository>(),
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<IVatCalculator>(),
            Mock.Of<ILoyaltyService>(),
            Mock.Of<IUserMembershipRepository>(),
            NoPreferredCleanerHold.Resolver,
            WorkContractResolvers.Resolver().Object,
            Mock.Of<INotificationProducer>(),
            Mock.Of<IAdminNotifier>(),
            NullLogger<OrderFactory>.Instance);

    /// <summary>
    /// The real catalogue behind a schedule's validator. No session and a card payment keep every other
    /// rule off the saved address, the consents and the cash standing, which are doubles.
    /// </summary>
    private static CreateRecurringBooking.Validator ScheduleValidatorOver(CleansiaDbContext ctx) =>
        new(
            Mock.Of<IOrderRepository>(),
            Mock.Of<IUserSessionProvider>(),
            Mock.Of<ISavedAddressRepository>(),
            Mock.Of<ICurrencyResolutionService>(),
            Mock.Of<ICountryRepository>(),
            new ServiceRepository(ctx),
            new PackageRepository(ctx),
            Mock.Of<IUserConsentRepository>(),
            Mock.Of<ILegalDocumentResolver>(),
            Mock.Of<IReceivableRepository>());

    private static CreateRecurringBooking.Command Schedule(
        IReadOnlyList<string> serviceIds, IReadOnlyList<string> packageIds) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "saved-1",
            SelectedServiceIds: serviceIds,
            SelectedPackageIds: packageIds,
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3),
            TermsAccepted: true,
            EarlyPerformanceRequested: true);

    private const string ScheduleOwnerId = "user-schedule-edit";

    /// <summary>
    /// The real catalogue behind a schedule edit's validator, over a template the session user owns and an
    /// entitled membership. No saved address and a card payment keep every other rule passing.
    /// </summary>
    private static UpdateRecurringBooking.Validator ScheduleEditValidatorOver(
        CleansiaDbContext ctx, RecurringBookingTemplate held)
    {
        var templates = new Mock<IRecurringBookingTemplateRepository>();
        templates.Setup(r => r.GetByIdForOwnerAsync(held.Id, ScheduleOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(held);
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(ScheduleOwnerId);
        var memberships = new Mock<IUserMembershipRepository>();
        memberships.Setup(r => r.GetEntitledForUserNoTrackingAsync(ScheduleOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                ScheduleOwnerId, "plan-plus", "currency-czk", "sub_1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));
        var savedAddresses = new Mock<ISavedAddressRepository>();
        savedAddresses.Setup(r => r.GetByUserAsync(ScheduleOwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new(
            templates.Object,
            memberships.Object,
            session.Object,
            Mock.Of<IOrderRepository>(),
            savedAddresses.Object,
            Mock.Of<ICurrencyResolutionService>(),
            Mock.Of<ICountryRepository>(),
            new ServiceRepository(ctx),
            new PackageRepository(ctx),
            Mock.Of<IReceivableRepository>());
    }

    private static RecurringBookingTemplate HeldTemplate(
        IReadOnlyList<string> serviceIds, IReadOnlyList<string> packageIds)
    {
        var template = RecurringBookingTemplate.Create(
            userId: ScheduleOwnerId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(9, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "saved-1",
            selectedServiceIds: serviceIds,
            selectedPackageIds: packageIds,
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(1));
        template.Id = "template-schedule-edit";
        return template;
    }

    private static UpdateRecurringBooking.Command ScheduleEdit(
        IReadOnlyList<string> serviceIds, IReadOnlyList<string> packageIds) =>
        new(
            TemplateId: "template-schedule-edit",
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "10:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "saved-1",
            SelectedServiceIds: serviceIds,
            SelectedPackageIds: packageIds,
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3));

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
