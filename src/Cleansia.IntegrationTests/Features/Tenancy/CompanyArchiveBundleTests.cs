using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0064 D3 on Postgres with two operating companies (TC-LC-ARCH-4): the archive consumer builds
/// exactly the decision's layout for frozen B into an in-memory blob store — the books as JSON Lines
/// with the person's members absent, the tenantless children nested or beside their parents, the
/// receipt and invoice PDFs copied byte for byte with the source blobs left in place, no file of the
/// person's estate, the manifest written last with every file's row count and hash — and then stamps
/// the row with the manifest's hash. A second delivery writes nothing. A build that dies after the
/// first file is rebuilt from scratch on redelivery into the same folder, hash for hash.
/// </summary>
[Collection("PostgresCollection")]
public sealed class CompanyArchiveBundleTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string A = TestTenants.Default;
    private const string B = TestTenants.Second;
    private const string AdminBId = "01ARCH-ADMIN-B-00000000000";
    private const string SvkId = "country-svk-bundle";
    private const string CzeId = "country-cze-bundle";
    private const string EurId = "currency-eur-bundle";
    private const string CzkId = "currency-czk-bundle";
    private static readonly DateTimeOffset FrozenOn = new(2026, 9, 16, 8, 30, 0, TimeSpan.Zero);
    private static readonly string Folder = CompanyArchiveService.FolderOf(B, FrozenOn);
    private static readonly byte[] ReceiptPdf = "%PDF-1.4 receipt of B"u8.ToArray();
    private static readonly byte[] InvoicePdf = "%PDF-1.4 payout invoice of B"u8.ToArray();

    private static readonly string[] ExpectedLayout =
    [
        "audit/admin-action-audits.jsonl",
        "audit/employee-action-audits.jsonl",
        "books/company-info.jsonl",
        "books/credit-accounts.jsonl",
        "books/credit-transactions.jsonl",
        "books/disputes.jsonl",
        "books/employee-invoices.jsonl",
        "books/employees.jsonl",
        "books/fiscal-counters.jsonl",
        "books/order-employee-pays.jsonl",
        "books/order-receipts.jsonl",
        "books/order-status-history.jsonl",
        "books/orders.jsonl",
        "books/pay-periods.jsonl",
        "books/payout-reference-counters.jsonl",
        "books/promo-code-redemptions.jsonl",
        "books/promo-codes.jsonl",
        "books/refunds.jsonl",
        "books/tenant-configurations.jsonl",
        "manifest.json",
        "payout-invoices/INV-2026-000001.pdf",
        "receipts/RCP-2026-0001.pdf",
    ];

    private readonly InMemoryBlobStorage _blobs = new();

    private sealed record Seeded(string CustomerBId, string EmployeeBId, string ReceiptedOrderId, string AnonymisedOrderId, string DisputeId);

    private Task SetupAsync(IServiceCollection services)
    {
        services.AddScoped<ICompanyArchiveService, CompanyArchiveService>();
        services.Replace(ServiceDescriptor.Singleton<IBlobContainerClientFactory>(_blobs));
        // One clock for every build, so two builds of the same frozen input are comparable byte for
        // byte: the manifest records the build instant.
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedClock(FrozenOn.AddHours(1))));
        return Task.CompletedTask;
    }

    private static async Task<CompanyArchiveRunSummary> RunAsync(IServiceProvider provider, string tenantId, DateTimeOffset requestedOn)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICompanyArchiveService>().RunAsync(tenantId, requestedOn, CancellationToken.None);
    }

    private IReadOnlyList<string> ArchivedNames() =>
        _blobs.Container(AppConstants.BlobContainers.CompanyArchives).Names
            .Where(n => n.StartsWith($"{Folder}/", StringComparison.Ordinal))
            .Select(n => n[(Folder.Length + 1)..])
            .Order(StringComparer.Ordinal)
            .ToList();

    private string Archived(string relative) =>
        Encoding.UTF8.GetString(_blobs.Container(AppConstants.BlobContainers.CompanyArchives).Bytes($"{Folder}/{relative}"));

    private static List<JsonElement> Lines(string jsonl) =>
        jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task The_Bundle_Holds_The_Books_Without_The_Person_The_PDFs_And_A_Manifest_Whose_Hash_Lands_On_The_Row_And_A_Second_Delivery_Is_A_No_Op()
    {
        Seeded seeded = default!;
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                seeded = await SeedAsync(ctx);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var first = await RunAsync(provider, B, FrozenOn);
                var snapshot = _blobs.Snapshot();
                var second = await RunAsync(provider, B, FrozenOn);
                return (First: first, Second: second, AfterFirst: snapshot);
            },
            assert: async (ctx, runs) =>
            {
                Assert.True(runs.First.Ran);
                Assert.Equal(Folder, runs.First.Folder);
                Assert.Equal(ExpectedLayout.Length, runs.First.Files);
                Assert.Equal(ExpectedLayout, ArchivedNames());

                var orders = Lines(Archived("books/orders.jsonl"));
                Assert.Equal(2, orders.Count);
                var receipted = Assert.Single(orders, o => o.GetProperty("id").GetString() == seeded.ReceiptedOrderId);
                Assert.Equal("RCP-2026-0001", receipted.GetProperty("receiptNumber").GetString());
                Assert.Equal(SvkId, receipted.GetProperty("countryId").GetString());
                Assert.Equal("Bratislava", receipted.GetProperty("city").GetString());
                Assert.Equal("Completed", receipted.GetProperty("currentStatus").GetString());
                Assert.Single(receipted.GetProperty("extras").EnumerateArray());
                var anonymised = Assert.Single(orders, o => o.GetProperty("id").GetString() == seeded.AnonymisedOrderId);
                Assert.Equal(JsonValueKind.Null, anonymised.GetProperty("receiptNumber").ValueKind);
                foreach (var row in orders)
                {
                    foreach (var forbidden in new[] { "customerName", "customerEmail", "customerPhone", "userId", "accessInstructions", "specialInstructions", "notes", "street", "customerAddressId" })
                    {
                        Assert.False(row.TryGetProperty(forbidden, out _), $"orders.jsonl carries {forbidden}");
                    }
                }

                var history = Lines(Archived("books/order-status-history.jsonl"));
                Assert.Equal(4, history.Count);
                Assert.All(history, h => Assert.Contains(h.GetProperty("orderId").GetString(), new[] { seeded.ReceiptedOrderId, seeded.AnonymisedOrderId }));

                var dispute = Assert.Single(Lines(Archived("books/disputes.jsonl")));
                Assert.Equal(seeded.DisputeId, dispute.GetProperty("id").GetString());
                Assert.Equal("Resolved", dispute.GetProperty("status").GetString());
                Assert.Single(dispute.GetProperty("lines").EnumerateArray());
                foreach (var forbidden in new[] { "description", "resolutionNotes", "messages", "evidence" })
                {
                    Assert.False(dispute.TryGetProperty(forbidden, out _), $"disputes.jsonl carries {forbidden}");
                }

                Assert.Equal(2, Lines(Archived("books/credit-transactions.jsonl")).Count);
                Assert.Single(Lines(Archived("books/credit-accounts.jsonl")));

                var employee = Assert.Single(Lines(Archived("books/employees.jsonl")));
                Assert.Equal(
                    new[] { "contractStatus", "id", "legalEntityName", "registrationNumber", "workCountryId" },
                    employee.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
                Assert.Equal(seeded.EmployeeBId, employee.GetProperty("id").GetString());

                var adminAudit = Assert.Single(Lines(Archived("audit/admin-action-audits.jsonl")));
                Assert.Equal(AdminBId, adminAudit.GetProperty("actorId").GetString());
                Assert.False(adminAudit.TryGetProperty("actorEmail", out _));
                Assert.Single(Lines(Archived("audit/employee-action-audits.jsonl")));

                var invoice = Assert.Single(Lines(Archived("books/employee-invoices.jsonl")));
                Assert.Equal("INV-2026-000001", invoice.GetProperty("invoiceNumber").GetString());
                Assert.False(invoice.TryGetProperty("adminNotes", out _));

                Assert.Single(Lines(Archived("books/pay-periods.jsonl")));
                Assert.Single(Lines(Archived("books/order-employee-pays.jsonl")));
                Assert.Single(Lines(Archived("books/order-receipts.jsonl")));
                Assert.Single(Lines(Archived("books/company-info.jsonl")));
                Assert.Single(Lines(Archived("books/tenant-configurations.jsonl")));
                Assert.Empty(Lines(Archived("books/refunds.jsonl")));
                Assert.Empty(Lines(Archived("books/promo-codes.jsonl")));

                // The PDFs are copies: byte-identical, and the sources are still where they were.
                var archives = _blobs.Container(AppConstants.BlobContainers.CompanyArchives);
                Assert.Equal(ReceiptPdf, archives.Bytes($"{Folder}/receipts/RCP-2026-0001.pdf"));
                Assert.Equal(InvoicePdf, archives.Bytes($"{Folder}/payout-invoices/INV-2026-000001.pdf"));
                Assert.Equal(ReceiptPdf, _blobs.Container(AppConstants.BlobContainers.GeneratedReceipts).Bytes("b/RCP-2026-0001.pdf"));
                Assert.Equal(InvoicePdf, _blobs.Container(AppConstants.BlobContainers.GeneratedInvoices).Bytes("2026-09/emp-b/INV-2026-000001.pdf"));

                // The manifest was written last, lists every other file with its rows and hash, and
                // its own hash is the row's.
                var manifestBytes = archives.Bytes($"{Folder}/manifest.json");
                Assert.Equal(archives.Names.Where(n => n.StartsWith($"{Folder}/", StringComparison.Ordinal)).Max(n => archives.WriteOrder(n)), archives.WriteOrder($"{Folder}/manifest.json"));
                var manifest = JsonDocument.Parse(manifestBytes).RootElement;
                Assert.Equal(B, manifest.GetProperty("tenantId").GetString());
                Assert.Equal("Cleansia SK s.r.o.", manifest.GetProperty("name").GetString());
                Assert.Equal(CompanyArchiveService.AdrId, manifest.GetProperty("adr").GetString());
                Assert.EndsWith("_Initial", manifest.GetProperty("schemaVersion").GetString(), StringComparison.Ordinal);
                Assert.Equal(FrozenOn, manifest.GetProperty("frozenOn").GetDateTimeOffset());
                Assert.Single(manifest.GetProperty("companies").EnumerateArray());
                var files = manifest.GetProperty("files").EnumerateArray().ToList();
                Assert.Equal(ExpectedLayout.Where(n => n != "manifest.json").Select(n => $"{Folder}/{n}").Order(StringComparer.Ordinal),
                    files.Select(f => f.GetProperty("path").GetString()!).Order(StringComparer.Ordinal));
                foreach (var file in files)
                {
                    var path = file.GetProperty("path").GetString()!;
                    Assert.Equal(Sha256(archives.Bytes(path)), file.GetProperty("sha256").GetString());
                    if (path.EndsWith(".jsonl", StringComparison.Ordinal))
                    {
                        Assert.Equal(Lines(Encoding.UTF8.GetString(archives.Bytes(path))).Count, file.GetProperty("rows").GetInt64());
                    }
                    else
                    {
                        Assert.Equal(JsonValueKind.Null, file.GetProperty("rows").ValueKind);
                    }
                }

                var tenant = await ctx.Tenants.AsNoTracking().SingleAsync(t => t.Id == B);
                Assert.Equal(Sha256(manifestBytes), tenant.ArchiveManifestSha256);
                Assert.Equal(runs.First.ManifestSha256, tenant.ArchiveManifestSha256);
                Assert.NotNull(tenant.ArchivedOn);
                Assert.Equal(FrozenOn, tenant.ArchiveRequestedOn);

                // The second delivery found an archived company and wrote nothing.
                Assert.False(runs.Second.Ran);
                Assert.Equal("the company is already archived", runs.Second.SkippedBecause);
                Assert.Equal(runs.AfterFirst, _blobs.Snapshot());

                // Nothing of A, and nothing of the person's estate, is in the bundle.
                Assert.DoesNotContain(ArchivedNames(), n => n.Contains("user", StringComparison.OrdinalIgnoreCase) || n.Contains("consent", StringComparison.OrdinalIgnoreCase) || n.Contains("payout-details", StringComparison.OrdinalIgnoreCase) || n.Contains("membership", StringComparison.OrdinalIgnoreCase) || n.Contains("customer-action", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(archives.Names, n => !n.StartsWith($"{B}/", StringComparison.Ordinal));
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Build_That_Dies_After_The_First_File_Is_Rebuilt_From_Scratch_Into_The_Same_Folder_Hash_For_Hash()
    {
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                await SeedAsync(ctx);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                _blobs.FailNextWriteOf($"{Folder}/books/order-status-history.jsonl");
                var died = await Record.ExceptionAsync(() => RunAsync(provider, B, FrozenOn));
                var afterDeath = ArchivedNames();
                var tenantAfterDeath = await TenantAsync(provider);

                var rebuilt = await RunAsync(provider, B, FrozenOn);
                var rebuiltSnapshot = _blobs.Snapshot();

                // The same frozen input built into a fresh store yields the same bundle, hash for hash.
                _blobs.ResetContainer(AppConstants.BlobContainers.CompanyArchives);
                await ResetArchiveStampsAsync(provider);
                var clean = await RunAsync(provider, B, FrozenOn);
                var cleanSnapshot = _blobs.Snapshot();

                return (Died: died, AfterDeath: afterDeath, TenantAfterDeath: tenantAfterDeath, Rebuilt: rebuilt, RebuiltSnapshot: rebuiltSnapshot, Clean: clean, CleanSnapshot: cleanSnapshot);
            },
            assert: (_, run) =>
            {
                Assert.IsType<IOException>(run.Died);
                Assert.Equal(["books/orders.jsonl"], run.AfterDeath);
                Assert.Null(run.TenantAfterDeath.ArchivedOn);
                Assert.Null(run.TenantAfterDeath.ArchiveManifestSha256);
                Assert.Equal(FrozenOn, run.TenantAfterDeath.ArchiveRequestedOn);

                Assert.True(run.Rebuilt.Ran);
                Assert.True(run.Clean.Ran);
                Assert.Equal(run.Clean.ManifestSha256, run.Rebuilt.ManifestSha256);
                Assert.Equal(run.CleanSnapshot, run.RebuiltSnapshot);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Message_Naming_A_Request_The_Row_No_Longer_Carries_Or_An_Unfrozen_Company_Is_Discarded()
    {
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                await SeedAsync(ctx);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var stale = await RunAsync(provider, B, FrozenOn.AddMinutes(-5));
                var unfrozen = await RunAsync(provider, A, FrozenOn);
                var missing = await RunAsync(provider, "cleansia-nobody", FrozenOn);
                return (Stale: stale, Unfrozen: unfrozen, Missing: missing);
            },
            assert: (_, runs) =>
            {
                Assert.False(runs.Stale.Ran);
                Assert.False(runs.Unfrozen.Ran);
                Assert.False(runs.Missing.Ran);
                Assert.Empty(_blobs.Container(AppConstants.BlobContainers.CompanyArchives).Names);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static async Task<Cleansia.Core.Domain.Tenancy.Tenant> TenantAsync(IServiceProvider provider)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().Tenants.AsNoTracking().SingleAsync(t => t.Id == B);
    }

    /// <summary>Back to frozen-and-unarchived, the state a failed first build leaves, so a clean build can be compared.</summary>
    private static async Task ResetArchiveStampsAsync(IServiceProvider provider)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        await ctx.Database.ExecuteSqlAsync($"""UPDATE "Tenants" SET "ArchivedOn" = NULL, "ArchiveManifestSha256" = NULL WHERE "Id" = {B}""");
    }

    private async Task<Seeded> SeedAsync(CleansiaDbContext ctx)
    {
        var english = Language.Create("en", "English");
        ctx.Languages.Add(english);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = EurId;
        eur.IsActive = true;
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        ctx.Currencies.AddRange(eur, czk);
        var slovakia = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        slovakia.Id = SvkId;
        var czechia = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        czechia.Id = CzeId;
        ctx.Countries.AddRange(slovakia, czechia);
        ctx.CountryConfigurations.AddRange(
            CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m, timeZoneId: "Europe/Bratislava").AssignOperator(B),
            CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague").AssignOperator(A).SetAsDefaultMarket(true));
        var category = Cleansia.Core.Domain.Services.ServiceCategory.Create("bundle", "Bundle", "Category under test");
        ctx.Add(category);
        var service = Cleansia.Core.Domain.Services.Service.Create(category.Id, "Bundle clean", "Under test", 60);
        ctx.Add(service);
        var extra = Extra.Create("windows", "Windows", "Window cleaning");
        ctx.Add(extra);

        var registry = await ctx.Tenants.SingleAsync(t => t.Id == B);
        registry.RequestWindDown(new DateOnly(2026, 8, 1), AdminBId, FrozenOn.AddDays(-60));
        registry.Deactivate(AdminBId, FrozenOn.AddDays(-45));
        registry.RequestArchive(AdminBId, FrozenOn);

        var companyInfoB = CompanyInfo.Create("Cleansia SK s.r.o.", "Cleansia SK", "12345678", "Hlavna 1", "Bratislava", "81101", SvkId);
        companyInfoB.TenantId = B;
        var companyInfoA = CompanyInfo.Create("Cleansia CZ s.r.o.", "Cleansia CZ", "87654321", "Hlavni 1", "Praha", "11000", CzeId);
        companyInfoA.TenantId = A;
        ctx.AddRange(companyInfoB, companyInfoA);
        var setting = TenantConfiguration.Create("lifecycle.chargeback_horizon_days", "30", category: "lifecycle");
        setting.TenantId = B;
        ctx.TenantConfigurations.Add(setting);

        var customerB = Stamped(NewUser("bundle-customer-b@cleansia.test", UserProfile.Customer), B);
        var cleanerUserB = Stamped(NewUser("bundle-cleaner-b@cleansia.test", UserProfile.Employee), B);
        var adminB = Stamped(NewUser("bundle-admin-b@cleansia.test", UserProfile.Administrator), B);
        adminB.Id = AdminBId;
        var customerA = Stamped(NewUser("bundle-customer-a@cleansia.test", UserProfile.Customer), A);
        ctx.Users.AddRange(customerB, cleanerUserB, adminB, customerA);
        ctx.UserConsents.Add(Stamped(UserConsent.Grant(customerB.Id, ConsentType.TermsOfService, null, null, "v1"), B));
        var cleanerB = Employee.CreateWithUser(cleanerUserB).Approve(AdminBId);
        cleanerB.Id = "emp-b";
        cleanerB.TenantId = B;
        ctx.Add(cleanerB);
        ctx.EmployeePayoutDetails.Add(Stamped(EmployeePayoutDetails.Create(
            cleanerB.Id, PayoutScheme.SepaIban, SvkId, PayoutDetailsStatus.Provided, iban: "SK3112000000198742637541", holderName: "Bundle Cleaner"), B));

        var receiptedOrder = NewOrder("bundle-b-receipted", SvkId, EurId, customerB.Id, DateTime.UtcNow.AddDays(-40), B);
        receiptedOrder.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, receiptedOrder));
        receiptedOrder.AddSelectedExtras([OrderExtra.Create(receiptedOrder, extra, 12m)]);
        var anonymisedOrder = NewOrder("bundle-b-anonymised", SvkId, EurId, customerB.Id, DateTime.UtcNow.AddYears(-3), B);
        anonymisedOrder.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, anonymisedOrder));
        anonymisedOrder.AnonymizeCustomerData();
        var orderA = NewOrder("bundle-a-order", CzeId, CzkId, customerA.Id, DateTime.UtcNow.AddDays(-40), A);
        orderA.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, orderA));
        ctx.Orders.AddRange(receiptedOrder, anonymisedOrder, orderA);

        var receipt = Stamped(OrderReceipt.Create(receiptedOrder.Id, "RCP-2026-0001", "RCP-2026-0001.pdf", "b/RCP-2026-0001.pdf", english.Id), B);
        ctx.OrderReceipts.Add(receipt);
        _blobs.Container(AppConstants.BlobContainers.GeneratedReceipts).Put("b/RCP-2026-0001.pdf", ReceiptPdf);

        var dispute = new Dispute(receiptedOrder.Id, customerB.Id, DisputeReason.QualityIssue, "the windows were streaky and the kitchen floor was skipped", customerB.Id);
        dispute.TenantId = B;
        dispute.AddLines([(service.Id, null)], customerB.Id);
        dispute.AddMessage("the windows were streaky", customerB.Id, isStaff: false);
        dispute.AddMessage("we are sorry, a credit is on its way", AdminBId, isStaff: true);
        dispute.Resolve(AdminBId, refundAmount: null, "credit issued");
        ctx.Disputes.Add(dispute);

        var credit = Stamped(CreditAccount.Create(customerB.Id, EurId, "seed"), B);
        credit.Issue(20m, CreditTransactionReason.DisputeSettlement, "bundle:dispute", AdminBId, disputeId: dispute.Id);
        credit.RecordExpiry(credit.Drain(AdminBId, FrozenOn.AddDays(-45)), "bundle:expired", AdminBId, note: "company wind-down");
        ctx.CreditAccounts.Add(credit);

        var period = Stamped(PayPeriod.Create(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15)).Close(AdminBId, "last period").MarkAsPaid(), B);
        ctx.PayPeriods.Add(period);
        var invoice = Stamped(EmployeeInvoice.Create(cleanerB.Id, period.Id, 1, 60m, EurId, "2026000001", "INV-2026-000001"), B);
        invoice.SetPdfBlobUrl(_blobs.Container(AppConstants.BlobContainers.GeneratedInvoices).Put("2026-09/emp-b/INV-2026-000001.pdf", InvoicePdf).ToString());
        invoice.Approve(AdminBId, adminNotes: "checked by hand").MarkAsPaid();
        ctx.EmployeeInvoices.Add(invoice);
        var pay = Stamped(OrderEmployeePay.Create(receiptedOrder.Id, cleanerB.Id, period.Id, EurId, basePay: 60m, totalPay: 60m), B);
        pay.AssignToInvoice(invoice.Id);
        ctx.OrderEmployeePays.Add(pay);

        ctx.AdminActionAudits.Add(new AdminActionAudit
        {
            ActorId = AdminBId, ActorEmail = "admin-b@cleansia.test", ActorProfile = UserProfile.Administrator, Action = "company.deactivate",
            ResourceType = "Tenant", ResourceId = B, Success = true, TenantId = B,
        });
        ctx.EmployeeActionAudits.Add(Stamped(EmployeeActionAudit.Create(cleanerB.Id, receiptedOrder.Id, EmployeeAuditAction.OrderDropped), B));
        ctx.CustomerActionAudits.Add(Stamped(CustomerActionAudit.Create(
            userId: customerB.Id, clientAudience: "cleansia.customer", ipAddress: null, deviceLabel: null, deviceId: null,
            action: "customer.order.create", resourceType: "Order", resourceId: receiptedOrder.Id, success: true, errorCode: null,
            payloadJson: null, correlationId: null), B));

        StampUnstampedAdded(ctx, B);
        return new Seeded(customerB.Id, cleanerB.Id, receiptedOrder.Id, anonymisedOrder.Id, dispute.Id);
    }

    private static T Stamped<T>(T entity, string tenantId) where T : Core.Domain.Common.ITenantEntity
    {
        entity.TenantId = tenantId;
        return entity;
    }

    private static User NewUser(string email, UserProfile profile)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Bundle", "Person", profile);
        user.ConfirmEmail();
        return user;
    }

    private static Order NewOrder(string id, string countryId, string currencyId, string userId, DateTime cleaningAt, string tenantId)
    {
        var address = Address.Create("Hlavna 1", "Bratislava", "81101", countryId);
        address.TenantId = tenantId;
        var order = Order.Create(
            customerName: "Bundle Person",
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+421900000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningAt,
            paymentType: PaymentType.Cash,
            totalPrice: 100m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.TenantId = tenantId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// A blob account in memory: one container per name, copies by URI across containers, a
    /// streaming writer that lands on dispose, a per-write fault to inject, and a snapshot to
    /// compare two builds.
    /// </summary>
    private sealed class InMemoryBlobStorage : IBlobContainerClientFactory
    {
        private const string Host = "https://blobs.test";
        private readonly Dictionary<string, InMemoryContainer> _containers = new(StringComparer.Ordinal);
        private string? _failNextWriteOf;
        private int _writes;

        public IBlobContainerClient GetBlobContainerClient(string containerName) => Container(containerName);

        public InMemoryContainer Container(string name)
        {
            if (!_containers.TryGetValue(name, out var container))
            {
                container = new InMemoryContainer(this, name);
                _containers[name] = container;
            }

            return container;
        }

        public void FailNextWriteOf(string blobName) => _failNextWriteOf = blobName;

        public void ResetContainer(string name) => _containers.Remove(name);

        public IReadOnlyDictionary<string, string> Snapshot() =>
            _containers.SelectMany(c => c.Value.Entries.Select(e => ($"{c.Key}/{e.Key}", Sha256(e.Value.Bytes))))
                .ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal);

        internal (InMemoryContainer Container, string Blob) Resolve(Uri uri)
        {
            var path = uri.AbsolutePath.TrimStart('/');
            var slash = path.IndexOf('/');
            return (Container(path[..slash]), path[(slash + 1)..]);
        }

        internal void BeforeWrite(string blobName)
        {
            if (_failNextWriteOf == blobName)
            {
                _failNextWriteOf = null;
                throw new IOException($"injected fault writing {blobName}");
            }
        }

        internal int NextWriteOrder() => ++_writes;

        internal sealed class InMemoryContainer(InMemoryBlobStorage storage, string name) : IBlobContainerClient
        {
            internal readonly Dictionary<string, (byte[] Bytes, int Order)> Entries = new(StringComparer.Ordinal);

            public IEnumerable<string> Names => Entries.Keys;

            public byte[] Bytes(string blobName) => Entries[blobName].Bytes;

            public int WriteOrder(string blobName) => Entries[blobName].Order;

            public Uri Put(string blobName, byte[] bytes)
            {
                Entries[blobName] = (bytes, storage.NextWriteOrder());
                return GetBlobUri(blobName);
            }

            public Task<IEnumerable<string>> GetFilesAsync(string path, CancellationToken cancellationToken) =>
                Task.FromResult(Names.Where(n => n.StartsWith(path, StringComparison.Ordinal)));

            public Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken) => Task.FromResult(Entries.ContainsKey(blobName));

            public Task<BlobFile> DownloadAsync(string blobName, CancellationToken cancellationToken) =>
                Task.FromResult(new BlobFile(new MemoryStream(Bytes(blobName), writable: false), "application/octet-stream"));

            public async Task UploadAsync(string blobName, Stream stream, Metadata? metadata = null, CancellationToken cancellationToken = default)
            {
                storage.BeforeWrite(blobName);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                Put(blobName, buffer.ToArray());
            }

            public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
            {
                Entries.Remove(blobName);
                return Task.CompletedTask;
            }

            public Task<Stream> CreateFileForWritingAsync(string blobName, CancellationToken cancellationToken)
            {
                storage.BeforeWrite(blobName);
                return Task.FromResult<Stream>(new LandingStream(bytes => Put(blobName, bytes)));
            }

            public Uri GetBlobUri(string blobName) => new($"{Host}/{name}/{blobName}");

            public Uri GenerateSasUri(string blobName, TimeSpan expiry) => GetBlobUri(blobName);

            public Uri GenerateSasUri(string blobName, TimeSpan expiry, ServedContentType servedAs) => GetBlobUri(blobName);

            public Task CopyAsync(Uri sourceUri, string targetBlob, CancellationToken cancellationToken)
            {
                var (source, blob) = storage.Resolve(sourceUri);
                Put(targetBlob, source.Bytes(blob).ToArray());
                return Task.CompletedTask;
            }

            public Task CopyAsync(string sourceBlob, string targetBlob, CancellationToken cancellationToken) =>
                CopyAsync(GetBlobUri(sourceBlob), targetBlob, cancellationToken);
        }

        private sealed class LandingStream(Action<byte[]> land) : MemoryStream
        {
            private bool _landed;

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_landed)
                {
                    _landed = true;
                    land(ToArray());
                }

                base.Dispose(disposing);
            }
        }
    }
}
