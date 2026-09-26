using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Cleansia.Core.Domain.Common;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0061 D11, end to end over the real hosts: a second operating company (<c>cleansia-sk</c>,
/// serving Slovakia) with a customer, a cleaner, an order, a receipt, a pay default, a promo code, a
/// company record and an active membership — and a CZ admin who lists none of it and 404s on every
/// row by id, while listing its own company's seeded config rows (the seed re-homing proven by a read,
/// not only by the scan). Customers read their own orders across operators; unrelated customers need
/// the order's secret. An anonymous
/// registration naming Slovakia lands in <c>cleansia-sk</c> and cannot be repeated in CZ; login by that
/// email resolves the SK account; every JWT the hosts mint carries <c>tenant_id</c>.
/// </summary>
public sealed class SecondTenantIsolationHostTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string SlovakiaId = "SK-hosttests";
    private const string EurId = "EUR-hosttests";
    private const string Password = "12345678Test!";
    private static readonly byte[] ReceiptBytes = [37, 80, 68, 70, 45, 49];
    private readonly Mock<IBlobContainerClient> _receiptBlob = new(MockBehavior.Strict);

    protected override void ConfigureCustomerHostServices(IServiceCollection services)
    {
        _receiptBlob.Setup(b => b.DownloadAsync("receipts/r.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobFile(new MemoryStream(ReceiptBytes), "application/pdf"));
        var factory = new Mock<IBlobContainerClientFactory>(MockBehavior.Strict);
        factory.Setup(f => f.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts)).Returns(_receiptBlob.Object);
        services.Replace(ServiceDescriptor.Singleton(factory.Object));
    }

    private sealed record Arranged(
        string CzAdminId, string CzAdminEmail,
        string CzCustomerId, string CzCustomerEmail,
        string SkCustomerId, string SkCustomerEmail,
        string SkEmployeeId, string SkOrderId, string SkOrderNumber, string SkGuestOrderToken,
        string SkPayConfigId, string SkPromoCodeId, string SkCompanyInfoId,
        string CzPayConfigId, string CzPromoCodeId, string CzCompanyInfoId);

    private async Task<Arranged> ArrangeTwoOperatorsAsync(bool receiptOwnedByCzCustomer = false)
    {
        Arranged arranged = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var eur = Currency.Create("EUR", "€", "Euro");
            eur.Id = EurId;
            eur.IsActive = true;
            ctx.Currencies.Add(eur);
            var slovakia = Country.Create("Slovakia", "SK", "SK", isServiced: true);
            slovakia.Id = SlovakiaId;
            ctx.Countries.Add(slovakia);
            ctx.CountryConfigurations.Add(
                CountryConfiguration.Create(SlovakiaId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.B));

            var category = ServiceCategory.Create("iso", "Isolation", "Category under test");
            ctx.Add(category);
            var service = Service.Create(category.Id, "Isolation Service", "Under test", 60);
            ctx.Add(service);

            // The first company's own config rows — the seed shape, written explicitly under A.
            var czAdmin = DomainSeed.Admin("cz-admin@hosttests.local", tenantId: HostTestTenants.A);
            var czCustomer = DomainSeed.Customer("cz-customer@hosttests.local", tenantId: HostTestTenants.A);
            ctx.Users.AddRange(czAdmin, czCustomer);
            var czCompany = Stamp(CompanyInfo.Create("Cleansia CZ s.r.o.", "CLEANSIA", "12345678", "Vaclavske 1", "Prague", "11000", DomainSeed.CountryId), HostTestTenants.A);
            var czPay = Stamp(EmployeePayConfig.CreateForService(service.Id, 250m, DomainSeed.CurrencyId), HostTestTenants.A);
            var czPromo = Stamp(PromoCode.CreatePercent("CZWELCOME", 0.10m), HostTestTenants.A);
            ctx.Add(czCompany);
            ctx.Add(czPay);
            ctx.Add(czPromo);

            // The second company, whole: customer, cleaner, order + receipt, pay default, promo, company, membership.
            var skCustomer = DomainSeed.Customer("sk-customer@hosttests.local", tenantId: HostTestTenants.B);
            var skCleanerUser = DomainSeed.EmployeeUser("sk-cleaner@hosttests.local", tenantId: HostTestTenants.B);
            ctx.Users.AddRange(skCustomer, skCleanerUser);
            var skCleaner = DomainSeed.ApprovedEmployee(skCleanerUser, tenantId: HostTestTenants.B);
            ctx.Employees.Add(skCleaner);
            var receiptOwner = receiptOwnedByCzCustomer ? czCustomer : skCustomer;
            var skOrder = DomainSeed.NewOrder(receiptOwner.Id, receiptOwner.Email, tenantId: HostTestTenants.B);
            ctx.Orders.Add(skOrder);
            // A guest booking of the second company, reachable only by the token its confirmation
            // e-mail carried — the shape the anonymous lookup answers to.
            var skGuestOrder = DomainSeed.NewOrder(null!, "sk-guest@hosttests.local", tenantId: HostTestTenants.B);
            ctx.Orders.Add(skGuestOrder);
            var skGuestToken = Stamp(
                GuestOrderAccessToken.Issue(skGuestOrder.Id, GuestOrderAccessToken.ExpiryFor(skGuestOrder.CleaningDateTime)),
                HostTestTenants.B);
            ctx.GuestOrderAccessTokens.Add(skGuestToken);
            var language = ctx.Languages.Local.FirstOrDefault(l => l.Code == DomainSeed.LanguageCode)
                ?? await ctx.Languages.SingleAsync(l => l.Code == DomainSeed.LanguageCode);
            var skReceipt = Stamp(OrderReceipt.Create(skOrder.Id, "R-2026-000001", "receipt.pdf", "receipts/r.pdf", language.Id), HostTestTenants.B);
            ctx.OrderReceipts.Add(skReceipt);
            var skCompany = Stamp(CompanyInfo.Create("Cleansia SK s.r.o.", "CLEANSIA SK", "87654321", "Hlavna 1", "Bratislava", "81101", SlovakiaId), HostTestTenants.B);
            var skPay = Stamp(EmployeePayConfig.CreateForService(service.Id, 10m, EurId), HostTestTenants.B);
            var skPromo = Stamp(PromoCode.CreatePercent("SKWELCOME", 0.10m), HostTestTenants.B);
            ctx.Add(skCompany);
            ctx.Add(skPay);
            ctx.Add(skPromo);
            var plan = DomainSeed.MembershipPlan("ISO-MONTHLY");
            ctx.MembershipPlans.Add(plan);
            ctx.MembershipPlanPrices.Add(DomainSeed.MembershipPlanPrice(plan.Id, "ISO-MONTHLY"));
            ctx.UserMemberships.Add(DomainSeed.ActiveMembership(skCustomer.Id, plan.Id, tenantId: HostTestTenants.B));

            arranged = new Arranged(
                czAdmin.Id, czAdmin.Email, czCustomer.Id, czCustomer.Email,
                skCustomer.Id, skCustomer.Email, skCleaner.Id,
                skOrder.Id, skOrder.DisplayOrderNumber, skGuestToken.RawToken!,
                skPay.Id, skPromo.Id, skCompany.Id,
                czPay.Id, czPromo.Id, czCompany.Id);
        });
        return arranged;
    }

    private static T Stamp<T>(T entity, string tenantId) where T : Cleansia.Core.Domain.Common.ITenantEntity
    {
        entity.TenantId = tenantId;
        return entity;
    }

    private string CzAdminToken(Arranged a) =>
        TestJwtFactory.Mint(AdminAudience, a.CzAdminId, a.CzAdminEmail, UserProfile.Administrator, tenantId: HostTestTenants.A);

    private static async Task<bool> HasMembershipAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/Membership/GetMine");
        HttpAssert.IsOk(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("hasMembership").GetBoolean();
    }

    private static async Task<HashSet<string>> IdsOfAsync(HttpResponseMessage response)
    {
        HttpAssert.IsOk(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_Cz_Admin_Lists_Its_Own_Companys_Rows_And_None_Of_The_Second_Companys()
    {
        var a = await ArrangeTwoOperatorsAsync();
        var admin = AdminClient(CzAdminToken(a));

        var companies = await IdsOfAsync(await admin.GetAsync("/api/AdminCompany/get-paged"));
        Assert.Contains(a.CzCompanyInfoId, companies);
        Assert.DoesNotContain(a.SkCompanyInfoId, companies);

        var payConfigs = await IdsOfAsync(await admin.GetAsync("/api/AdminPayConfig/get-paged"));
        Assert.Contains(a.CzPayConfigId, payConfigs);
        Assert.DoesNotContain(a.SkPayConfigId, payConfigs);

        var promoCodes = await IdsOfAsync(await admin.GetAsync("/api/AdminPromoCode/get-paged"));
        Assert.Contains(a.CzPromoCodeId, promoCodes);
        Assert.DoesNotContain(a.SkPromoCodeId, promoCodes);

        var orders = await IdsOfAsync(await admin.GetAsync("/api/AdminOrder/get-paged"));
        Assert.DoesNotContain(a.SkOrderId, orders);

        var employees = await IdsOfAsync(await admin.GetAsync("/api/AdminEmployee/get-paged"));
        Assert.DoesNotContain(a.SkEmployeeId, employees);

        // Customers are listed on the partner host; the same admin, the same claim.
        var partnerAdminToken = TestJwtFactory.Mint(PartnerAudience, a.CzAdminId, a.CzAdminEmail, UserProfile.Administrator, tenantId: HostTestTenants.A);
        var users = await IdsOfAsync(await PartnerClient(partnerAdminToken).GetAsync("/api/User/GetPaged"));
        Assert.Contains(a.CzCustomerId, users);
        Assert.DoesNotContain(a.SkCustomerId, users);
    }

    [Fact]
    public async Task Every_By_Id_Read_Of_A_Second_Company_Row_Is_Not_Found_For_The_Cz_Admin()
    {
        var a = await ArrangeTwoOperatorsAsync();
        var admin = AdminClient(CzAdminToken(a));

        await HttpAssert.RejectedAsync(await admin.GetAsync($"/api/AdminOrder/details/{a.SkOrderId}"), BusinessErrorMessage.OrderNotFound);
        await HttpAssert.RejectedAsync(await admin.GetAsync($"/api/AdminEmployee/details/{a.SkEmployeeId}"), BusinessErrorMessage.EmployeeNotFound);
        await HttpAssert.RejectedAsync(await admin.GetAsync($"/api/AdminPayConfig/details/{a.SkPayConfigId}"), BusinessErrorMessage.PayConfigNotFound);
        await HttpAssert.RejectedAsync(await admin.GetAsync($"/api/AdminPromoCode/details/{a.SkPromoCodeId}"), BusinessErrorMessage.PromoNotFound);
        await HttpAssert.RejectedAsync(await admin.GetAsync($"/api/AdminCompany/details/{a.SkCompanyInfoId}"), BusinessErrorMessage.CompanyInfoNotFound);

        var partnerAdminToken = TestJwtFactory.Mint(PartnerAudience, a.CzAdminId, a.CzAdminEmail, UserProfile.Administrator, tenantId: HostTestTenants.A);
        await HttpAssert.RejectedAsync(
            await PartnerClient(partnerAdminToken).GetAsync($"/api/User/GetById?UserId={a.SkCustomerId}"),
            BusinessErrorMessage.NotExistingUserWithId);

        var skClaim = TestJwtFactory.Mint(CustomerAudience, a.SkCustomerId, a.SkCustomerEmail, UserProfile.Customer, tenantId: HostTestTenants.B);
        Assert.True(await HasMembershipAsync(CustomerClient(skClaim)));
        // Membership remains in the account company's scope even though order ownership spans operators.
        var czClaimOnSkOwner = TestJwtFactory.Mint(CustomerAudience, a.SkCustomerId, a.SkCustomerEmail, UserProfile.Customer, tenantId: HostTestTenants.A);
        Assert.False(await HasMembershipAsync(CustomerClient(czClaimOnSkOwner)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Customer_Can_Download_Their_Receipt_Across_Operators_And_A_Different_Customer_Cannot(bool receiptOwnedByCzCustomer)
    {
        var a = await ArrangeTwoOperatorsAsync(receiptOwnedByCzCustomer);
        var ownerId = receiptOwnedByCzCustomer ? a.CzCustomerId : a.SkCustomerId;
        var ownerEmail = receiptOwnedByCzCustomer ? a.CzCustomerEmail : a.SkCustomerEmail;
        var token = TestJwtFactory.Mint(CustomerAudience, ownerId, ownerEmail, UserProfile.Customer, tenantId: HostTestTenants.A);
        var receipt = await CustomerClient(token).GetAsync($"/api/Order/DownloadReceipt?OrderId={a.SkOrderId}");
        HttpAssert.IsOk(receipt);
        Assert.Equal("application/pdf", receipt.Content.Headers.ContentType!.MediaType);
        Assert.Equal(ReceiptBytes, await receipt.Content.ReadAsByteArrayAsync());
        _receiptBlob.Verify(b => b.DownloadAsync("receipts/r.pdf", It.IsAny<CancellationToken>()), Times.Once);

        var outsiderId = receiptOwnedByCzCustomer ? a.SkCustomerId : a.CzCustomerId;
        var outsiderEmail = receiptOwnedByCzCustomer ? a.SkCustomerEmail : a.CzCustomerEmail;
        var outsiderTenant = receiptOwnedByCzCustomer ? HostTestTenants.B : HostTestTenants.A;
        var outsider = TestJwtFactory.Mint(CustomerAudience, outsiderId, outsiderEmail, UserProfile.Customer, tenantId: outsiderTenant);
        await HttpAssert.RejectedAsync(
            await CustomerClient(outsider).GetAsync($"/api/Order/DownloadReceipt?OrderId={a.SkOrderId}"),
            BusinessErrorMessage.OrderNotFound);
        _receiptBlob.Verify(b => b.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var persisted = await QueryAsync(ctx => ctx.OrderReceipts.IgnoreQueryFilters().SingleAsync(r => r.OrderId == a.SkOrderId));
        Assert.Equal(HostTestTenants.B, persisted.TenantId);
    }

    [Fact]
    public async Task The_Cz_Customer_Reaches_The_Sk_Order_Only_By_Its_Secret()
    {
        var a = await ArrangeTwoOperatorsAsync();
        var anonymous = CustomerClientAnonymous();

        var bySecret = await anonymous.GetAsync(
            $"/api/Order/Lookup?token={Uri.EscapeDataString(a.SkGuestOrderToken)}");
        HttpAssert.IsOk(bySecret);

        var byAnotherToken = await anonymous.GetAsync(
            $"/api/Order/Lookup?token={Uri.EscapeDataString(SecurityTokens.Generate(SecurityTokens.DurableTokenByteLength))}");
        await HttpAssert.RejectedAsync(byAnotherToken, BusinessErrorMessage.OrderNotFound);

        var czCustomer = TestJwtFactory.Mint(CustomerAudience, a.CzCustomerId, a.CzCustomerEmail, UserProfile.Customer, tenantId: HostTestTenants.A);
        await HttpAssert.RejectedAsync(
            await CustomerClient(czCustomer).GetAsync($"/api/Order/GetById?OrderId={a.SkOrderId}"),
            BusinessErrorMessage.OrderNotFound);
    }

    [Fact]
    public async Task An_Anonymous_Registration_Naming_Slovakia_Lands_In_The_Second_Company_And_Cannot_Be_Repeated_In_Cz()
    {
        await ArrangeTwoOperatorsAsync();
        const string email = "new-slovak@hosttests.local";
        var anonymous = CustomerClientAnonymous();

        var inSlovakia = await anonymous.PostAsJsonAsync("/api/Auth/Register", new
        {
            email, password = Password, firstName = "New", lastName = "Slovak", language = DomainSeed.LanguageCode, countryId = SlovakiaId, termsAccepted = true,
        });
        HttpAssert.IsOk(inSlovakia);

        var user = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == email));
        Assert.Equal(HostTestTenants.B, user.TenantId);

        var inCzechia = await anonymous.PostAsJsonAsync("/api/Auth/Register", new
        {
            email, password = Password, firstName = "New", lastName = "Slovak", language = DomainSeed.LanguageCode, termsAccepted = true,
        });
        await HttpAssert.RejectedAsync(inCzechia, BusinessErrorMessage.ExistingUserWithEmail);
        Assert.Equal(1, await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task Login_Resolves_The_Account_By_Email_And_Every_Minted_Jwt_Carries_Its_Company()
    {
        var a = await ArrangeTwoOperatorsAsync();

        foreach (var (email, expectedTenant) in new[]
                 {
                     (a.SkCustomerEmail, HostTestTenants.B),
                     (a.CzCustomerEmail, HostTestTenants.A),
                 })
        {
            // The customer web host answers with an HttpOnly cookie rather than a body token.
            var response = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", new { email, password = Password, rememberMe = true });
            HttpAssert.IsOk(response);
            var accessCookie = response.Headers.GetValues("Set-Cookie")
                .Single(c => c.StartsWith("customer_token=", StringComparison.Ordinal));
            var accessToken = Uri.UnescapeDataString(accessCookie["customer_token=".Length..].Split(';')[0]);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            Assert.Equal(expectedTenant, Assert.Single(jwt.Claims, c => c.Type == "tenant_id").Value);
        }

        var tokens = await QueryAsync(ctx => ctx.RefreshTokens.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.False(string.IsNullOrEmpty(t.TenantId)));
    }
}
