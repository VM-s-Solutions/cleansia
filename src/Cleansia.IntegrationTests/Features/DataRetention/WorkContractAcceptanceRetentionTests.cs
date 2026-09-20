using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// ADR-0068 D5 (Verification #6) through the REAL sweep on real Postgres with two operating companies:
/// company A holds <c>retention.work_contract_metadata.years = 1</c>, company B is at the default, both
/// carry acceptance rows older than a year. One run of <c>RunAllRetentionTasksAsync</c> blanks the IP,
/// device label and device id on A's old rows and on nothing else; every row is still there with its
/// seat, text, version, instant and facts. The floor of one is refused at write through the real
/// command under <c>tenant_setting.invalid_value</c>.
/// </summary>
[Collection("PostgresCollection")]
public sealed class WorkContractAcceptanceRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-wcret";
    private const string CurrencyId = "currency-czk-wcret";
    private const string OrderAId = "order-wcret-a";
    private const string OrderBId = "order-wcret-b";
    private const string OldSeatA = "01SEATWCRETAOLD00000000001";
    private const string YoungSeatA = "01SEATWCRETAYOUNG000000001";
    private const string OldSeatB = "01SEATWCRETBOLD00000000001";
    private const string Ip = "198.51.100.7";
    private const string Facts = "{\"orderNumber\":\"ORD-WCRET\"}";

    private static readonly DateTimeOffset TwoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2);

    private static Task AsAdministrator(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-wcret", "admin-wcret@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_Companys_Own_Window_Blanks_Its_Old_Rows_Trio_And_No_Other_Companys_And_Deletes_Nothing()
    {
        await TestMethod(
            setup: services =>
            {
                services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
                services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
                return Task.CompletedTask;
            },
            arrange: Seed,
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var rows = await context.WorkContractAcceptances.IgnoreQueryFilters().ToListAsync();
                Assert.Equal(3, rows.Count);

                var oldA = rows.Single(r => r.OrderEmployeeId == OldSeatA);
                Assert.Null(oldA.IpAddress);
                Assert.Null(oldA.DeviceLabel);
                Assert.Null(oldA.DeviceId);
                Assert.Equal(OrderAId, oldA.OrderId);
                Assert.Equal(TestLegalDocuments.WorkContractTextEnId, oldA.LegalDocumentTextId);
                Assert.Equal(WorkContractTestData.Version, oldA.DocumentVersion);
                Assert.Equal("ORD-WCRET", JsonDocument.Parse(oldA.FactsJson).RootElement.GetProperty("orderNumber").GetString());
                Assert.Equal(TestTenants.Default, oldA.TenantId);

                Assert.Equal(Ip, rows.Single(r => r.OrderEmployeeId == YoungSeatA).IpAddress);
                var oldB = rows.Single(r => r.OrderEmployeeId == OldSeatB);
                Assert.Equal(Ip, oldB.IpAddress);
                Assert.Equal("Firefox", oldB.DeviceLabel);
                Assert.Equal(TestTenants.Second, oldB.TenantId);

                var setting = Assert.Single(await context.TenantConfigurations.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(TestTenants.Default, setting.TenantId);
                Assert.Equal("1", setting.Value);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Window_Of_Zero_Is_Refused_At_Write()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new SetTenantSetting.Command(RetentionDefaults.WorkContractMetadataRetentionYearsKey, "0")),
            assert: async (CleansiaDbContext context, BusinessResult<SetTenantSetting.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == Core.AppServices.Common.BusinessErrorMessage.TenantSettingInvalidValue);
                Assert.Equal("1", Assert.Single(await context.TenantConfigurations.IgnoreQueryFilters().ToListAsync()).Value);
            },
            transactional: false);
    }

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var (document, _) = TestLegalDocuments.Add(context);
        var text = document.TextFor("en")!;

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var window = TenantConfiguration.Create(RetentionDefaults.WorkContractMetadataRetentionYearsKey, "1");
        window.TenantId = TestTenants.Default;
        window.Created("admin-cz", DateTimeOffset.UtcNow);
        context.TenantConfigurations.Add(window);

        var orderA = NewOrder(OrderAId, document);
        orderA.TenantId = TestTenants.Default;
        orderA.CustomerAddress!.TenantId = TestTenants.Default;
        var orderB = NewOrder(OrderBId, document);
        orderB.TenantId = TestTenants.Second;
        orderB.CustomerAddress!.TenantId = TestTenants.Second;
        foreach (var track in orderB.OrderStatusHistory)
        {
            track.TenantId = TestTenants.Second;
        }

        context.Orders.AddRange(orderA, orderB);

        context.WorkContractAcceptances.AddRange(
            Row(OrderAId, OldSeatA, text, TwoYearsAgo, TestTenants.Default),
            Row(OrderAId, YoungSeatA, text, DateTimeOffset.UtcNow.AddMonths(-6), TestTenants.Default),
            Row(OrderBId, OldSeatB, text, TwoYearsAgo, TestTenants.Second));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static WorkContractAcceptance Row(string orderId, string seatId, LegalDocumentText text, DateTimeOffset acceptedOn, string tenantId)
    {
        var row = WorkContractAcceptance.Create(
            orderId, seatId, "employee-wcret", text, WorkContractTestData.Version,
            "cleansia.partner", Ip, "Firefox", "device-claim-wcret", Facts);
        row.TenantId = tenantId;
        typeof(WorkContractAcceptance).GetProperty(nameof(WorkContractAcceptance.AcceptedOn))!.SetValue(row, acceptedOn);
        return row;
    }

    private static Order NewOrder(string id, LegalDocument document)
    {
        var order = Order.Create(
            customerName: "Ret Ention",
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: TwoYearsAgo.AddDays(-1).UtcDateTime,
            paymentType: PaymentType.Cash,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.SetWorkContractDocument(document);
        return order;
    }
}
