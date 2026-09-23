using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// An <see cref="Address"/> row is deduped on (Street, City, ZipCode, CountryId), so everyone at one street
/// shares it: the erased customer's past order, a neighbour's upcoming booking and the neighbour's saved
/// address all point at ONE row. Blanking that row in place rewrote the neighbour's job and address along
/// with the subject's. Against real Postgres, through the real erasure and the real retention sweep: the
/// shared row is left exactly as it was — not even marked updated — and the subject's order moves to an
/// anonymised copy of its own under its own company. An original nobody else needs is deleted under
/// restrictive foreign keys, so a concurrent new owner refuses the erasure rather than losing data.
/// </summary>
[Collection("PostgresCollection")]
public class SharedAddressAnonymisationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string NeighbourId = "user-neighbour-shared-addr";
    private const string CountryId = "country-cz-shared-addr";
    private const string CurrencyId = "currency-czk-shared-addr";

    private const string SharedAddressId = "address-shared-street";
    private const string SharedStreet = "Sdilena 5";
    private const string SoleAddressId = "address-sole-street";
    private const string SoleStreet = "Samotna 9";
    private const string City = "Praha";
    private const string ZipCode = "11000";
    private const double Latitude = 50.0755;
    private const double Longitude = 14.4378;

    private const string SubjectPastOrderId = "order-sa-subject-past";
    private const string SubjectRecentOrderId = "order-sa-subject-recent";
    private const string NeighbourLiveOrderId = "order-sa-neighbour-live";
    private const string StrangerOldOrderId = "order-sa-stranger-old";

    private static Task WithoutBlobStorage(IServiceCollection services)
    {
        var factory = new Mock<IBlobContainerClientFactory>();
        factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
        services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
        services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Erasing_A_Customer_Leaves_A_Shared_Address_Row_Untouched_And_Moves_Their_Order_To_An_Anonymised_Copy()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var shared = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                context.Orders.AddRange(
                    NewOrder(SubjectPastOrderId, SubjectId, shared, DateTime.UtcNow.AddDays(-30)),
                    NewOrder(NeighbourLiveOrderId, NeighbourId, shared, DateTime.UtcNow.AddDays(3), OrderStatus.Confirmed));
                context.SavedAddresses.AddRange(
                    SavedAddress.Create(SubjectId, SharedAddressId, "Home", isDefault: true),
                    SavedAddress.Create(NeighbourId, SharedAddressId, "Home", isDefault: true));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                await AssertSharedRowUntouchedAsync(context);

                var orders = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress)
                    .ToDictionaryAsync(o => o.Id);

                var neighbours = orders[NeighbourLiveOrderId];
                Assert.Equal(SharedAddressId, neighbours.CustomerAddressId);
                Assert.Equal("Neighbour Customer", neighbours.CustomerName);
                var neighboursSaved = Assert.Single(await context.SavedAddresses.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(NeighbourId, neighboursSaved.UserId);
                Assert.Equal(SharedAddressId, neighboursSaved.AddressId);

                AssertOnItsOwnAnonymisedCopy(orders[SubjectPastOrderId]);
            });
    }

    [Fact]
    public async Task Erasing_A_Customer_Whose_Address_Nobody_Else_Uses_Replaces_It_And_Leaves_No_Copy_Of_The_Street()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var sole = await SeedCatalogueAndPeopleAsync(context, SoleAddressId, SoleStreet);
                context.Orders.Add(NewOrder(SubjectPastOrderId, SubjectId, sole, DateTime.UtcNow.AddDays(-30)));
                context.SavedAddresses.Add(SavedAddress.Create(SubjectId, SoleAddressId, "Home", isDefault: true));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == SubjectPastOrderId);
                Assert.NotEqual(SoleAddressId, order.CustomerAddressId);

                var address = Assert.Single(await context.Addresses.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(order.CustomerAddressId, address.Id);
                Assert.Equal(AnonymizationMarker.Value, address.Street);
                Assert.Equal(AnonymizationMarker.Value, address.City);
                Assert.Equal(AnonymizationMarker.Value, address.ZipCode);
                Assert.Null(address.Latitude);
                Assert.Empty(await context.SavedAddresses.IgnoreQueryFilters().ToListAsync());
            });
    }

    /// <summary>
    /// The sweep never erases anybody, so the customer's own newer order and their saved address keep the
    /// row as surely as the neighbour's booking does — the repeat customer whose first job passes the window
    /// is the everyday case. An original address nobody else needs is deleted.
    /// </summary>
    [Fact]
    public async Task The_Order_Pii_Sweep_Leaves_A_Shared_Address_Row_Untouched_And_Moves_The_Old_Order_To_An_Anonymised_Copy()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var shared = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                var sole = Address.Create(SoleStreet, City, ZipCode, CountryId);
                sole.Id = SoleAddressId;
                context.Orders.AddRange(
                    NewOrder(SubjectPastOrderId, SubjectId, shared, DateTime.UtcNow.AddYears(-3)),
                    NewOrder(SubjectRecentOrderId, SubjectId, shared, DateTime.UtcNow.AddDays(-30)),
                    NewOrder(NeighbourLiveOrderId, NeighbourId, shared, DateTime.UtcNow.AddDays(3), OrderStatus.Confirmed),
                    NewOrder(StrangerOldOrderId, userId: null, sole, DateTime.UtcNow.AddYears(-3)));
                context.SavedAddresses.Add(SavedAddress.Create(SubjectId, SharedAddressId, "Home", isDefault: true));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertSharedRowUntouchedAsync(context);

                var orders = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress)
                    .ToDictionaryAsync(o => o.Id);

                Assert.Equal(SharedAddressId, orders[SubjectRecentOrderId].CustomerAddressId);
                Assert.Equal("Subject Customer", orders[SubjectRecentOrderId].CustomerName);
                Assert.Equal(SharedAddressId, orders[NeighbourLiveOrderId].CustomerAddressId);
                var saved = Assert.Single(await context.SavedAddresses.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SharedAddressId, saved.AddressId);

                Assert.Equal(AnonymizationMarker.Value, orders[SubjectPastOrderId].CustomerName);
                AssertOnItsOwnAnonymisedCopy(orders[SubjectPastOrderId]);

                var strangers = orders[StrangerOldOrderId];
                Assert.NotEqual(SoleAddressId, strangers.CustomerAddressId);
                Assert.Equal(AnonymizationMarker.Value, strangers.CustomerAddress!.Street);

                var addresses = await context.Addresses.IgnoreQueryFilters().ToListAsync();
                Assert.Equal(3, addresses.Count);
                Assert.DoesNotContain(addresses, a => a.Street == SoleStreet);
            });
    }

    [Fact]
    public async Task Erasure_Preserves_An_Address_Referenced_Only_By_Another_Customers_Saved_Address()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var shared = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                context.Orders.Add(NewOrder(SubjectPastOrderId, SubjectId, shared, DateTime.UtcNow.AddDays(-30)));
                context.SavedAddresses.Add(SavedAddress.Create(NeighbourId, SharedAddressId, "Home", isDefault: true));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                await AssertSharedRowUntouchedAsync(context);
                AssertOnItsOwnAnonymisedCopy(await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress).SingleAsync());
                Assert.Equal(SharedAddressId, (await context.SavedAddresses.IgnoreQueryFilters().SingleAsync()).AddressId);
            });
    }

    [Fact]
    public async Task Retention_Preserves_An_Address_Referenced_Only_By_The_Customers_Own_Saved_Address()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var shared = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                context.Orders.Add(NewOrder(SubjectPastOrderId, SubjectId, shared, DateTime.UtcNow.AddYears(-3)));
                context.SavedAddresses.Add(SavedAddress.Create(SubjectId, SharedAddressId, "Home", isDefault: true));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                await AssertSharedRowUntouchedAsync(context);
                AssertOnItsOwnAnonymisedCopy(await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress).SingleAsync());
                Assert.Equal(SharedAddressId, (await context.SavedAddresses.IgnoreQueryFilters().SingleAsync()).AddressId);
            });
    }

    [Fact]
    public async Task Erasure_Only_Excludes_Saved_Addresses_It_Actually_Removes_From_The_Source_Reference_Check()
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var address = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                context.Orders.Add(NewOrder(SubjectPastOrderId, SubjectId, address, DateTime.UtcNow.AddDays(-30)));
                var inactive = SavedAddress.Create(SubjectId, address.Id, "Former home", isDefault: false);
                inactive.Deactivated(SubjectId, DateTimeOffset.UtcNow.AddDays(-1));
                context.SavedAddresses.Add(inactive);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command()),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                await AssertSharedRowUntouchedAsync(context);
                AssertOnItsOwnAnonymisedCopy(await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress).SingleAsync());
                var saved = await context.SavedAddresses.IgnoreQueryFilters().SingleAsync();
                Assert.False(saved.IsActive);
                Assert.Equal(SharedAddressId, saved.AddressId);
            });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Cleaner_Erasure_Anonymises_Their_Address_Without_Rewriting_A_Customers_Booking(
        bool sharedWithCustomer, bool withOwnReferences)
    {
        await TestMethod(
            setup: WithoutBlobStorage,
            arrange: async context =>
            {
                var address = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                var subject = context.Users.Local.Single(u => u.Id == SubjectId);
                var employee = Employee.CreateWithUser(subject);
                employee.UpdateAddress(address);
                context.Employees.Add(employee);
                if (sharedWithCustomer)
                {
                    context.Orders.Add(NewOrder(NeighbourLiveOrderId, NeighbourId, address,
                        DateTime.UtcNow.AddDays(3), OrderStatus.Confirmed));
                }
                if (withOwnReferences)
                {
                    context.Orders.Add(NewOrder(SubjectPastOrderId, SubjectId, address, DateTime.UtcNow.AddDays(-30)));
                    context.SavedAddresses.Add(SavedAddress.Create(SubjectId, SharedAddressId, "Home", isDefault: true));
                }
                await context.CommitAsync(CancellationToken.None);
            },
            act: EraseCleanerAsync,
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var employee = await context.Employees.IgnoreQueryFilters().Include(e => e.Address).SingleAsync();
                Assert.Equal(AnonymizationMarker.Value, employee.Address!.Street);
                Assert.Null(employee.Address.Latitude);
                Assert.Equal(employee.TenantId, employee.Address.TenantId);
                Assert.NotEqual(SharedAddressId, employee.AddressId);
                if (sharedWithCustomer)
                {
                    await AssertSharedRowUntouchedAsync(context);
                    Assert.Equal(SharedAddressId, (await context.Orders.IgnoreQueryFilters().SingleAsync()).CustomerAddressId);
                }
                else
                {
                    var addresses = await context.Addresses.IgnoreQueryFilters().ToListAsync();
                    Assert.Equal(withOwnReferences ? 2 : 1, addresses.Count);
                    Assert.All(addresses, address => Assert.Equal(AnonymizationMarker.Value, address.Street));
                    Assert.DoesNotContain(addresses, address => address.Id == SharedAddressId);
                    Assert.Empty(await context.SavedAddresses.IgnoreQueryFilters().ToListAsync());
                }
            });
    }

    private static async Task<BusinessResult> EraseCleanerAsync(IServiceProvider provider)
    {
        var result = await provider.GetRequiredService<IGdprDeletionService>().DeleteUserAccountAsync(
            SubjectId, GdprAuditReasons.AdminDeletion, _ => ("integration-admin", null),
            deferEmployeeErasure: false, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await provider.GetRequiredService<CleansiaDbContext>().CommitAsync(CancellationToken.None);
        return result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_New_Reference_After_The_Census_Refuses_Erasure_Instead_Of_Blanking_Or_Deleting_The_Neighbours_Data(
        bool neighbourBooksOrder)
    {
        await TestMethod(
            setup: async services =>
            {
                await WithoutBlobStorage(services);
                services.Replace(ServiceDescriptor.Scoped<IAddressRepository>(provider =>
                {
                    var repository = new AddressRepository(provider.GetRequiredService<CleansiaDbContext>());
                    var seam = new Mock<IAddressRepository>(MockBehavior.Strict);
                    seam.Setup(r => r.Dispose());
                    seam.Setup(r => r.Add(It.IsAny<Address>())).Callback<Address>(repository.Add);
                    seam.Setup(r => r.RemoveRange(It.IsAny<IEnumerable<Address>>()))
                        .Callback<IEnumerable<Address>>(repository.RemoveRange);
                    seam.Setup(r => r.GetReferencedElsewhereAsync(
                            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>(),
                            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                        .Returns(async (IReadOnlyCollection<string> addressIds, IReadOnlyCollection<string> orderIds,
                            IReadOnlyCollection<string> savedAddressIds, string? employeeId, CancellationToken ct) =>
                        {
                            var references = await repository.GetReferencedElsewhereAsync(
                                addressIds, orderIds, savedAddressIds, employeeId, ct);
                            Assert.DoesNotContain(SharedAddressId, references);

                            await using var otherScope = provider.CreateAsyncScope();
                            var otherContext = otherScope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                            var address = await otherContext.Addresses.SingleAsync(a => a.Id == SharedAddressId, ct);
                            if (neighbourBooksOrder)
                            {
                                otherContext.Orders.Add(NewOrder(NeighbourLiveOrderId, NeighbourId, address,
                                    DateTime.UtcNow.AddDays(3), OrderStatus.Confirmed));
                            }
                            else
                            {
                                otherContext.SavedAddresses.Add(
                                    SavedAddress.Create(NeighbourId, address.Id, "Home", isDefault: true));
                            }
                            await otherContext.CommitAsync(ct);
                            return references;
                        });
                    return seam.Object;
                }));
            },
            arrange: async context =>
            {
                var address = await SeedCatalogueAndPeopleAsync(context, SharedAddressId, SharedStreet);
                context.Orders.Add(NewOrder(SubjectPastOrderId, SubjectId, address, DateTime.UtcNow.AddDays(-30)));
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await Assert.ThrowsAsync<DbUpdateException>(async () =>
                await provider.GetRequiredService<IMediator>().Send(new DeleteUserAccount.Command())),
            assert: async (CleansiaDbContext context, DbUpdateException exception) =>
            {
                Assert.Equal(PostgresErrorCodes.RestrictViolation,
                    Assert.IsType<PostgresException>(exception.InnerException).SqlState);
                await AssertSharedRowUntouchedAsync(context);
                var subjectOrder = await context.Orders.IgnoreQueryFilters()
                    .SingleAsync(o => o.Id == SubjectPastOrderId);
                Assert.Equal(SharedAddressId, subjectOrder.CustomerAddressId);
                Assert.Equal("Subject Customer", subjectOrder.CustomerName);
                Assert.Single(await context.Addresses.IgnoreQueryFilters().ToListAsync());
                if (neighbourBooksOrder)
                {
                    var neighbourOrder = await context.Orders.IgnoreQueryFilters()
                        .SingleAsync(o => o.Id == NeighbourLiveOrderId);
                    Assert.Equal(SharedAddressId, neighbourOrder.CustomerAddressId);
                    Assert.Equal("Neighbour Customer", neighbourOrder.CustomerName);
                }
                else
                {
                    var savedAddress = await context.SavedAddresses.IgnoreQueryFilters().SingleAsync();
                    Assert.Equal(NeighbourId, savedAddress.UserId);
                    Assert.Equal(SharedAddressId, savedAddress.AddressId);
                }
            },
            transactional: false);
    }

    private static async Task AssertSharedRowUntouchedAsync(CleansiaDbContext context)
    {
        var shared = await context.Addresses.IgnoreQueryFilters().SingleAsync(a => a.Id == SharedAddressId);
        Assert.Equal(SharedStreet, shared.Street);
        Assert.Equal(City, shared.City);
        Assert.Equal(ZipCode, shared.ZipCode);
        Assert.Null(shared.State);
        Assert.Equal(Latitude, shared.Latitude);
        Assert.Equal(Longitude, shared.Longitude);
        Assert.Null(shared.UpdatedOn);
        Assert.Null(shared.UpdatedBy);
    }

    private static void AssertOnItsOwnAnonymisedCopy(Order order)
    {
        Assert.NotEqual(SharedAddressId, order.CustomerAddressId);
        var copy = order.CustomerAddress!;
        Assert.Equal(AnonymizationMarker.Value, copy.Street);
        Assert.Equal(AnonymizationMarker.Value, copy.City);
        Assert.Equal(AnonymizationMarker.Value, copy.ZipCode);
        Assert.Null(copy.Latitude);
        Assert.Null(copy.Longitude);
        Assert.Equal(CountryId, copy.CountryId);
        Assert.Equal(order.TenantId, copy.TenantId);
    }

    private static async Task<Address> SeedCatalogueAndPeopleAsync(CleansiaDbContext context, string addressId, string street)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
        }

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var subject = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        var neighbour = User.CreateWithPassword("neighbour.shared-addr@cleansia.test", "Seed-Password-123", "Neighbour", "Customer");
        neighbour.Id = NeighbourId;
        neighbour.ConfirmEmail();
        context.Users.AddRange(subject, neighbour);

        var address = Address.Create(street, City, ZipCode, CountryId, latitude: Latitude, longitude: Longitude);
        address.Id = addressId;
        return address;
    }

    private static Order NewOrder(
        string id, string? userId, Address address, DateTime cleaningDateTime, OrderStatus status = OrderStatus.Completed)
    {
        var (customerName, customerEmail) = userId switch
        {
            SubjectId => ("Subject Customer", TestConstants.TestUserSession.TestUserEmail),
            NeighbourId => ("Neighbour Customer", "neighbour.shared-addr@cleansia.test"),
            _ => ("Stranger Guest", "stranger.shared-addr@cleansia.test"),
        };
        var order = Order.Create(
            customerName: customerName,
            customerEmail: customerEmail,
            customerPhone: "+420777111333",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        return order;
    }
}
