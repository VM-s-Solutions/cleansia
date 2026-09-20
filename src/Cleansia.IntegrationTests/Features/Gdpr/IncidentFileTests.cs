using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.Pdf.IncidentFile;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.IntegrationTests.Features.Legal;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// The customer incident file (Q-AUD-L6 ruling: a PDF) through the real pipeline on real Postgres. A
/// subject with a completed, refunded, disputed order, a versioned consent and rows on all three audit
/// tables; a bystander with an order of their own and a refused probe at the subject's order. Unscoped,
/// the file carries the subject's own customer rows and the admin and cleaner rows on their orders and
/// disputes, with a currency id read as its code; scoped to the order it carries the subject's rows that
/// name the order or its dispute — never the stranger's probe, whose id and request context are not the
/// subject's to export — and not the admin act on the account; the bystander's order is refused as not
/// found and the refusal is recorded; and an erased subject still gets the file — identity marked erased,
/// the trail present with its request context blanked, the order reached through the subject's own
/// successful act on it now that the order no longer names them.
/// </summary>
[Collection("PostgresCollection")]
public class IncidentFileTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "CZ-incident";
    private const string CurrencyId = "CZK-incident";
    private const string SubjectId = "user-incident-subject";
    private const string SubjectEmail = "incident-subject@cleansia.test";
    private const string BystanderId = "user-incident-bystander";
    private const string CleanerUserId = "user-incident-cleaner";
    private const string CleanerId = "employee-incident-1";
    private const string OrderId = "order-incident-1";
    private const string StrangerOrderId = "order-incident-stranger";
    private const string DisputeId = "dispute-incident-1";
    private const string AdminId = "admin-incident";
    private const string AdminEmail = "admin-incident@cleansia.test";
    private const string SubjectIp = "203.0.113.9";
    private const string SubjectDevice = "iPhone 15 / iOS 17.4";
    private const string SeatId = "01SEATINCIDENT000000000001";
    private const string CleanerIp = "198.51.100.23";
    private const string CleanerDeviceId = "device-claim-incident-1";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";

    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    private static Task AsAdministrator(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, AdminEmail, [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Unscoped_The_File_Carries_The_Subjects_Record_Resolves_The_Currency_And_Is_Recorded_As_An_Admin_Act()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: Seed,
            act: async provider =>
            {
                // Built before the command: the command's own admin row on the account joins the trail of
                // every later build, and this one asserts the seed.
                var data = await provider.GetRequiredService<IIncidentFileService>().BuildAsync(SubjectId, null, AdminEmail, CancellationToken.None);
                var result = await provider.GetRequiredService<IMediator>().Send(new ExportCustomerIncidentFile.Command(SubjectId, null));
                return (result, data);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<ExportCustomerIncidentFile.Response> Result, IncidentFilePdfData Data) outcome) =>
            {
                var (result, data) = outcome;
                Assert.True(result.IsSuccess);
                Assert.Equal("%PDF", Encoding.ASCII.GetString(result.Value.PdfBytes, 0, 4));
                Assert.Equal($"incident-{SubjectId}-{DateTimeOffset.UtcNow:yyyyMMdd}.pdf", result.Value.FileName);

                Assert.False(data.Subject.Erased);
                Assert.Equal(SubjectEmail, data.Subject.Email);
                Assert.Equal("Cleansia CZ s.r.o.", data.Subject.OperatorName);
                Assert.Equal("Czechia (CZ)", data.Subject.Market);

                var orderNumber = await OrderNumberAsync(context);
                var order = Assert.Single(data.Orders);
                Assert.Equal(orderNumber, order.Number);
                Assert.Equal("CZK", order.Currency);
                Assert.Equal("Completed", order.Status);
                Assert.Equal("Card / Paid", $"{order.PaymentType} / {order.PaymentStatus}");
                Assert.Equal(["New", "Completed"], order.StatusHistory.Select(s => s.Status));
                Assert.Contains("Testovaci 12", order.Address);
                var refund = Assert.Single(order.Refunds);
                Assert.Equal(300m, refund.Amount);
                Assert.Equal("DisputeResolution", refund.Reason);
                Assert.Equal("Succeeded", refund.Status);
                var cleaner = Assert.Single(order.AssignedCleaners);
                Assert.Equal(CleanerId, cleaner.EmployeeId);
                Assert.Equal("Clean", cleaner.FirstName);

                var dispute = Assert.Single(data.Disputes);
                Assert.Equal(orderNumber, dispute.OrderNumber);
                Assert.Equal("QualityIssue", dispute.Reason);
                Assert.Equal(Description, dispute.Description);
                var message = Assert.Single(dispute.Messages);
                Assert.Equal("Customer", message.AuthorRole);
                Assert.Equal("Photos attached, the tiles are still grey.", message.Text);
                Assert.Equal(["kitchen-floor.jpg"], dispute.EvidenceFileNames);

                var terms = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService);
                var consent = Assert.Single(data.Consents);
                Assert.Equal("TermsOfService", consent.Type);
                Assert.Equal(terms.Version, consent.DocumentVersion);
                Assert.Equal(terms.EffectiveFrom, consent.EffectiveFrom);
                Assert.True(consent.IsGranted);
                Assert.Equal(SubjectIp, consent.IpAddress);

                var contract = Assert.Single(data.Contracts);
                Assert.Equal(orderNumber, contract.OrderNumber);
                Assert.Equal(SeatId, contract.OrderEmployeeId);
                Assert.Equal(CleanerId, contract.EmployeeId);
                Assert.Equal("Clean", contract.CleanerFirstName);
                Assert.Equal(T0.AddMinutes(30), contract.AcceptedOn);
                Assert.Equal((await LegalSeed.PlatformWideAsync(context, LegalDocumentType.WorkContract)).Version, contract.DocumentVersion);
                Assert.Equal("en", contract.Language);
                Assert.Equal(JwtAudiences.Mobile, contract.ClientAudience);
                Assert.Equal(CleanerIp, contract.IpAddress);
                Assert.Equal("Pixel 8 / Android 15", contract.DeviceLabel);
                Assert.Equal(CleanerDeviceId, contract.DeviceId);
                Assert.Equal("ORD-INC-SNAPSHOT", contract.Facts.Single(f => f.Key == "orderNumber").Value);
                Assert.Equal("Praha · 110", contract.Facts.Single(f => f.Key == "locationApproximate").Value);

                Assert.False(data.TrailTruncated);
                Assert.Equal(7, data.Trail.Count);
                var customerRows = data.Trail.Where(e => e.Source == IncidentFileService.CustomerSource).ToList();
                Assert.Equal(["customer.dispute.create", "customer.order.create"], customerRows.OrderByDescending(e => e.OccurredOn).Select(e => e.Action));
                Assert.All(customerRows, e => Assert.Equal(SubjectId, e.ActorId));
                var booking = customerRows.Single(e => e.Action == "customer.order.create");
                Assert.Equal(SubjectIp, booking.IpAddress);
                Assert.Equal(SubjectDevice, booking.DeviceLabel);
                Assert.Equal("CZK", booking.Evidence.Single(f => f.Key == "currencyId").Value);
                Assert.Equal("1500", booking.Evidence.Single(f => f.Key == "totalPrice").Value);

                var adminRows = data.Trail.Where(e => e.Source == IncidentFileService.AdminSource).ToList();
                Assert.Equal(["dispute.resolve", "gdpr.user.export", "order.refund.partial"], adminRows.Select(e => e.Action).OrderBy(a => a));
                var refundRow = adminRows.Single(e => e.Action == "order.refund.partial");
                Assert.Equal("Administrator", refundRow.ActorRole);
                Assert.Equal("dispute upheld", refundRow.Evidence.Single(f => f.Key == "reason").Value);
                Assert.Equal("CZK", refundRow.Evidence.Single(f => f.Key == "after.currencyId").Value);

                var cleanerRows = data.Trail.Where(e => e.Source == IncidentFileService.CleanerSource).ToList();
                Assert.Equal(["employee.order.contract_accepted", "employee.order.dropped"], cleanerRows.Select(e => e.Action).OrderBy(a => a));
                Assert.All(cleanerRows, e => Assert.Equal(CleanerId, e.ActorId));

                var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(data));
                Assert.Contains($"## Order {orderNumber}\n", text);
                Assert.Contains($"## Contract for work on order {orderNumber}, seat {SeatId}\n", text);
                Assert.Contains($"Cleaner: Clean ({CleanerId})\n", text);
                Assert.Contains($"Request: {CleanerIp} / Pixel 8 / Android 15 / {CleanerDeviceId}\n", text);
                Assert.Contains("orderNumber | ORD-INC-SNAPSHOT\n", text);
                Assert.Contains("Action: customer.order.create\n", text);
                Assert.Contains("Action: employee.order.dropped\n", text);
                Assert.Contains("Action: employee.order.contract_accepted\n", text);
                Assert.Contains("currencyId | CZK\n", text);
                Assert.Contains("Operator: Cleansia CZ s.r.o.\n", text);
                Assert.Contains("Market: Czechia (CZ)\n", text);
                Assert.DoesNotContain(TestTenants.Default, text);

                var audit = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().Where(a => a.Action == "gdpr.user.incident_file").ToListAsync());
                Assert.True(audit.Success);
                Assert.Equal("User", audit.ResourceType);
                Assert.Equal(SubjectId, audit.ResourceId);
                Assert.Equal(AdminId, audit.ActorId);
                var snapshot = JsonDocument.Parse(audit.AfterJson!).RootElement;
                Assert.Equal(SubjectId, snapshot.GetProperty("subjectUserId").GetString());
                Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("orderId").ValueKind);
                Assert.Equal(1, snapshot.GetProperty("orderCount").GetInt32());
                Assert.Equal(1, snapshot.GetProperty("disputeCount").GetInt32());
                Assert.Equal(1, snapshot.GetProperty("consentCount").GetInt32());
                Assert.Equal(7, snapshot.GetProperty("trailEntryCount").GetInt32());
                Assert.Equal(IncidentFileDigest.Sha256Hex(IncidentFileSections.Build(data)), snapshot.GetProperty("dataSha256").GetString());
                Assert.Equal(7, snapshot.EnumerateObject().Count());
                Assert.DoesNotContain(SubjectEmail, audit.AfterJson!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("ORD-", audit.AfterJson!);
                Assert.DoesNotContain(SubjectIp, audit.AfterJson!);
                Assert.DoesNotContain("kitchen", audit.AfterJson!, StringComparison.OrdinalIgnoreCase);

                Assert.Empty(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task Scoped_To_The_Order_The_Trail_Is_The_Subjects_Rows_On_It_And_Its_Dispute_Not_A_Strangers_Probe_Nor_The_Act_On_The_Account()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: Seed,
            act: async provider =>
            {
                var result = await provider.GetRequiredService<IMediator>().Send(new ExportCustomerIncidentFile.Command(SubjectId, OrderId));
                var data = await provider.GetRequiredService<IIncidentFileService>().BuildAsync(SubjectId, OrderId, AdminEmail, CancellationToken.None);
                return (result, data);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<ExportCustomerIncidentFile.Response> Result, IncidentFilePdfData Data) outcome) =>
            {
                var (result, data) = outcome;
                Assert.True(result.IsSuccess);
                Assert.Equal(OrderId, data.OrderIdFilter);
                Assert.Equal(OrderId, Assert.Single(data.Orders).Id);
                Assert.Single(data.Disputes);

                var customerRows = data.Trail.Where(e => e.Source == IncidentFileService.CustomerSource).ToList();
                Assert.Equal(["customer.dispute.create", "customer.order.create"], customerRows.Select(e => e.Action).OrderBy(a => a));
                Assert.All(customerRows, e => Assert.Equal(SubjectId, e.ActorId));
                Assert.Contains(customerRows, e => e.Action == "customer.dispute.create" && e.ResourceType == "Dispute" && e.ResourceId == DisputeId);
                Assert.DoesNotContain(data.Trail, e => e.ActorId == BystanderId);
                Assert.DoesNotContain(BystanderId, IncidentFileDigest.CanonicalText(IncidentFileSections.Build(data)));

                var adminRows = data.Trail.Where(e => e.Source == IncidentFileService.AdminSource).ToList();
                Assert.Equal(["dispute.resolve", "order.refund.partial"], adminRows.Select(e => e.Action).OrderBy(a => a));
                Assert.Equal(2, data.Trail.Count(e => e.Source == IncidentFileService.CleanerSource));
                Assert.Equal(SeatId, Assert.Single(data.Contracts).OrderEmployeeId);

                var audit = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().Where(a => a.Action == "gdpr.user.incident_file").ToListAsync());
                Assert.True(audit.Success);
                Assert.Equal(OrderId, JsonDocument.Parse(audit.AfterJson!).RootElement.GetProperty("orderId").GetString());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Strangers_Order_Is_Refused_As_Not_Found_And_The_Refusal_Is_Recorded()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new ExportCustomerIncidentFile.Command(SubjectId, StrangerOrderId)),
            assert: async (CleansiaDbContext context, BusinessResult<ExportCustomerIncidentFile.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                var error = Assert.Single(validation.Errors);
                Assert.Equal(Core.AppServices.Common.BusinessErrorMessage.OrderNotFound, error.Message);

                var audit = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().Where(a => a.Action == "gdpr.user.incident_file").ToListAsync());
                Assert.False(audit.Success);
                Assert.Equal(Core.AppServices.Common.BusinessErrorMessage.OrderNotFound, audit.ErrorCode);
                Assert.Equal("User", audit.ResourceType);
                Assert.Equal(SubjectId, audit.ResourceId);
                Assert.Null(audit.AfterJson);
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Erased_Subject_Still_Gets_The_File_With_Its_Trail_Its_Proven_Order_And_Its_Identity_Marked_Erased()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: Seed,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var erased = await mediator.Send(new AdminDeleteUserAccount.Command(SubjectId));
                Assert.True(erased.IsSuccess);

                var result = await mediator.Send(new ExportCustomerIncidentFile.Command(SubjectId, OrderId));
                var data = await provider.GetRequiredService<IIncidentFileService>().BuildAsync(SubjectId, null, AdminEmail, CancellationToken.None);
                return (result, data);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<ExportCustomerIncidentFile.Response> Result, IncidentFilePdfData Data) outcome) =>
            {
                var (result, data) = outcome;
                Assert.True(result.IsSuccess);

                Assert.True(data.Subject.Erased);
                Assert.NotNull(data.Subject.ErasedOn);
                Assert.StartsWith("deleted_", data.Subject.Email);

                // The order's own customer link is gone; the subject's successful booking row still names it.
                Assert.Null(await context.Orders.IgnoreQueryFilters().Where(o => o.Id == OrderId).Select(o => o.UserId).SingleAsync());
                var order = Assert.Single(data.Orders);
                Assert.Equal(await OrderNumberAsync(context), order.Number);
                Assert.Single(data.Disputes);
                Assert.Equal(Description, data.Disputes[0].Description);
                // The customer's erasure leaves the cleaner's contract record untouched (ADR-0068 D5).
                var contract = Assert.Single(data.Contracts);
                Assert.Equal(CleanerIp, contract.IpAddress);
                Assert.Equal("Clean", contract.CleanerFirstName);

                var booking = Assert.Single(data.Trail, e => e.Action == "customer.order.create");
                Assert.Equal(SubjectId, booking.ActorId);
                Assert.Null(booking.IpAddress);
                Assert.Null(booking.DeviceLabel);
                Assert.Equal("CZK", booking.Evidence.Single(f => f.Key == "currencyId").Value);

                var consent = Assert.Single(data.Consents);
                Assert.False(consent.IsGranted);
                Assert.NotNull(consent.WithdrawnAt);

                var text = IncidentFileDigest.CanonicalText(IncidentFileSections.Build(data));
                Assert.Contains("Name: erased\n", text);
                Assert.Contains("E-mail: erased\n", text);
                Assert.Contains("Erased: yes, ", text);
                Assert.DoesNotContain(SubjectEmail, text);
                Assert.DoesNotContain("anonymized.local", text);
                Assert.Contains("Action: customer.order.create\n", text);

                Assert.Contains(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync(),
                    a => a.Action == "gdpr.user.incident_file" && a.ResourceId == SubjectId && a.Success);
            },
            transactional: false);
    }

    private static Task<string> OrderNumberAsync(CleansiaDbContext context) =>
        context.Orders.IgnoreQueryFilters().Where(o => o.Id == OrderId).Select(o => o.DisplayOrderNumber).SingleAsync();

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default));

        var subject = User.CreateWithPassword(SubjectEmail, "Seed-Password-123", "Inci", "Dent");
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        var bystander = User.CreateWithPassword("incident-bystander@cleansia.test", "Seed-Password-123", "By", "Stander");
        bystander.Id = BystanderId;
        bystander.ConfirmEmail();
        var cleanerUser = User.CreateWithPassword("incident-cleaner@cleansia.test", "Seed-Password-123", "Clean", "Er", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerId;
        context.Users.AddRange(subject, bystander, cleanerUser);
        context.Employees.Add(cleaner);

        var order = NewOrder(OrderId, SubjectId, SubjectEmail);
        order.CompleteOrder(90);
        var seat = OrderEmployee.Create(order, cleaner);
        seat.Id = SeatId;
        order.AddAssignedEmployee(seat);
        context.Orders.Add(order);
        context.Orders.Add(NewOrder(StrangerOrderId, BystanderId, "incident-bystander@cleansia.test"));

        var refund = Refund.Create(OrderId, "refund-incident-1", 300m, "CZK", RefundReason.DisputeResolution, RefundSource.AppRefund, disputeId: DisputeId);
        refund.MarkSucceeded("re_incident_1", T0.AddDays(2));
        context.Refunds.Add(refund);

        var dispute = new Dispute(OrderId, SubjectId, DisputeReason.QualityIssue, Description, SubjectId);
        dispute.Id = DisputeId;
        dispute.AddMessage("Photos attached, the tiles are still grey.", SubjectId, isStaff: false);
        dispute.AddEvidence("kitchen-floor.jpg", $"{OrderId}/2f9c1a4b7d6e4f0b9c3a5e8d1f2b4c60.jpg", SubjectId);
        context.Disputes.Add(dispute);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);

        await LegalSeed.SeedAsync(context);
        var terms = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService);
        var consent = UserConsent.Grant(SubjectId, ConsentType.TermsOfService, SubjectIp, "Mozilla/5.0", terms.Version, terms.Id);
        consent.Created("seed", T0.AddDays(-30));
        context.UserConsents.Add(consent);

        context.CustomerActionAudits.AddRange(
            CustomerRow("caud-inc-create", SubjectId, "Order", OrderId, "customer.order.create", T0, success: true, errorCode: null,
                payloadJson: $"{{\"currencyId\":\"{CurrencyId}\",\"totalPrice\":1500,\"isGuest\":false}}"),
            CustomerRow("caud-inc-probe", BystanderId, "Order", OrderId, "customer.order.cancel", T0.AddHours(2), success: false, errorCode: "order.not_found", payloadJson: null),
            CustomerRow("caud-inc-dispute", SubjectId, "Dispute", DisputeId, "customer.dispute.create", T0.AddDays(1), success: true, errorCode: null,
                payloadJson: $"{{\"disputeId\":\"{DisputeId}\",\"orderId\":\"{OrderId}\",\"reason\":\"qualityIssue\"}}"),
            CustomerRow("caud-inc-stranger", BystanderId, "Order", StrangerOrderId, "customer.order.create", T0.AddHours(5), success: true, errorCode: null, payloadJson: null));

        context.AdminActionAudits.AddRange(
            AdminRow("aaud-inc-refund", "order.refund.partial", "Order", OrderId, T0.AddHours(3), reason: "dispute upheld",
                afterJson: $"{{\"amount\":300,\"currencyId\":\"{CurrencyId}\"}}"),
            AdminRow("aaud-inc-export", "gdpr.user.export", "User", SubjectId, T0.AddHours(4), reason: null, afterJson: null),
            AdminRow("aaud-inc-resolve", "dispute.resolve", "Dispute", DisputeId, T0.AddDays(1).AddHours(2), reason: null, afterJson: null));

        var drop = EmployeeActionAudit.Create(CleanerId, OrderId, EmployeeAuditAction.OrderDropped);
        drop.Id = "eaud-inc-drop";
        drop.TenantId = TestTenants.Default;
        drop.Created(CleanerUserId, T0.AddHours(6));
        context.EmployeeActionAudits.Add(drop);

        var contract = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.WorkContract);
        var acceptance = WorkContractAcceptance.Create(
            OrderId, SeatId, CleanerId, contract.TextFor("en")!, contract.Version, JwtAudiences.Mobile,
            CleanerIp, "Pixel 8 / Android 15", CleanerDeviceId,
            $"{{\"orderNumber\":\"ORD-INC-SNAPSHOT\",\"totalPrice\":1500,\"currencyCode\":\"CZK\",\"locationApproximate\":\"Praha · 110\"}}");
        acceptance.TenantId = TestTenants.Default;
        acceptance.Created(CleanerUserId, T0.AddMinutes(30));
        typeof(WorkContractAcceptance).GetProperty(nameof(WorkContractAcceptance.AcceptedOn))!.SetValue(acceptance, T0.AddMinutes(30));
        context.WorkContractAcceptances.Add(acceptance);
        var accepted = EmployeeActionAudit.Create(CleanerId, OrderId, EmployeeAuditAction.ContractAccepted);
        accepted.Id = "eaud-inc-accept";
        accepted.TenantId = TestTenants.Default;
        accepted.Created(CleanerUserId, T0.AddMinutes(30));
        context.EmployeeActionAudits.Add(accepted);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.SaveChangesAsync();
    }

    private static Order NewOrder(string orderId, string userId, string email)
    {
        var order = Order.Create(
            customerName: "Inci Dent",
            customerEmail: email,
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: T0.AddDays(1).UtcDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = orderId;
        order.Created("seed", T0);
        var stamp = T0;
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Completed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddDays(1);
        }

        return order;
    }

    private static CustomerActionAudit CustomerRow(
        string id, string? userId, string resourceType, string resourceId, string action, DateTimeOffset occurredOn,
        bool success, string? errorCode, string? payloadJson)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: SubjectIp, deviceLabel: SubjectDevice,
            deviceId: "device-1", action: action, resourceType: resourceType, resourceId: resourceId, success: success,
            errorCode: errorCode, payloadJson: payloadJson, correlationId: null);
        row.Id = id;
        row.TenantId = TestTenants.Default;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }

    private static AdminActionAudit AdminRow(
        string id, string action, string resourceType, string resourceId, DateTimeOffset occurredOn, string? reason, string? afterJson) =>
        new()
        {
            Id = id,
            TenantId = TestTenants.Default,
            ActorId = AdminId,
            ActorEmail = AdminEmail,
            ActorProfile = UserProfile.Administrator,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Success = true,
            OccurredOn = occurredOn,
            Reason = reason,
            BeforeJson = afterJson is null ? null : "{}",
            AfterJson = afterJson,
        };
}
