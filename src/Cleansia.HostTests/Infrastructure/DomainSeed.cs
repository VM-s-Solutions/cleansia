using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// Builds the entity graphs the authz ACs need, written through a real host DbContext. All builders
/// leave <c>TenantId</c> null (single-tenant) unless a <paramref name="tenantId"/> is supplied (the
/// cross-tenant AC). Reference data (Country / Currency / Language) is created on demand and de-duped.
/// </summary>
public static class DomainSeed
{
    public const string CountryId = "CZ-hosttests";
    public const string CurrencyId = "CZK-hosttests";
    public const string LanguageCode = "en";

    public static async Task EnsureReferenceDataAsync(CleansiaDbContext ctx)
    {
        if (!await ctx.Countries.IgnoreQueryFilters().AnyAsync(c => c.Id == CountryId))
        {
            var country = Country.Create("Czechia", "CZ", isServiced: true);
            country.Id = CountryId;
            ctx.Countries.Add(country);
        }

        if (!await ctx.Currencies.IgnoreQueryFilters().AnyAsync(c => c.Id == CurrencyId))
        {
            var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
            currency.Id = CurrencyId;
            ctx.Currencies.Add(currency);
        }

        if (!await ctx.Languages.IgnoreQueryFilters().AnyAsync(l => l.Code == LanguageCode))
        {
            ctx.Languages.Add(Language.Create(LanguageCode, "English"));
        }
    }

    public static User Customer(string email, string? tenantId = null)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Cust", "Omer", UserProfile.Customer);
        user.ConfirmEmail();
        if (tenantId is not null) user.TenantId = tenantId;
        return user;
    }

    public static User EmployeeUser(string email, string? tenantId = null)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Emp", "Loyee", UserProfile.Employee);
        user.ConfirmEmail();
        user.Update("Emp", "Loyee", "+420777111222", new DateOnly(1990, 1, 1));
        if (tenantId is not null) user.TenantId = tenantId;
        return user;
    }

    public static User Admin(string email, string? tenantId = null)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Ad", "Min", UserProfile.Administrator);
        user.ConfirmEmail();
        if (tenantId is not null) user.TenantId = tenantId;
        return user;
    }

    /// <summary>A fully registration-complete, APPROVED employee with an active document — passes the
    /// [RequireCompleteProfile] filter on the Partner Order/Payroll/Dashboard controllers.</summary>
    public static Employee ApprovedEmployee(User user, string? tenantId = null)
    {
        var employee = BuildCompleteEmployee(user, tenantId);
        employee.Approve(approvedByUserId: "admin-seed");
        return employee;
    }

    /// <summary>A registration-complete employee that the admin has REJECTED (EMP-GAP-01). Profile +
    /// docs are complete; only the contract status differs, isolating the status gate.</summary>
    public static Employee RejectedEmployee(User user, string? tenantId = null)
    {
        var employee = BuildCompleteEmployee(user, tenantId);
        employee.Reject(rejectedByUserId: "admin-seed", reason: "host-test rejected cleaner");
        return employee;
    }

    private static Employee BuildCompleteEmployee(User user, string? tenantId)
    {
        var address = Address.Create("Test St 1", "Prague", "11000", CountryId);
        var employee = Employee.CreateWithUser(user);
        employee.UpdateEmployeeDetails(
            entityType: EmployeeEntityType.NaturalPerson,
            registrationNumber: "REG-123456",
            vatNumber: null,
            legalEntityName: null,
            nationalityId: CountryId,
            passportId: "P1234567",
            iban: "CZ6508000000192000145399",
            address: address,
            availability: new Dictionary<string, List<Cleansia.Core.Domain.Users.TimeRange>>(),
            emergencyContactName: "ICE",
            emergencyContactPhone: "+420777000000");
        if (tenantId is not null) employee.TenantId = tenantId;
        return employee;
    }

    public static EmployeeDocument ActiveDocument(string employeeId, string? tenantId = null)
    {
        var doc = EmployeeDocument.Create(
            employeeId: employeeId,
            fileName: "id.pdf",
            filePath: "host-tests/id.pdf",
            contentType: "application/pdf",
            fileSizeBytes: 1024,
            documentType: DocumentType.Passport,
            description: "host-test active doc",
            createdBy: "seed");
        doc.Approve("admin-seed");
        if (tenantId is not null) doc.TenantId = tenantId;
        return doc; // IsActive defaults to true (BaseEntity)
    }

    public static PayPeriod PayPeriod(string? tenantId = null)
    {
        var period = Cleansia.Core.Domain.EmployeePayroll.PayPeriod.Create(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        if (tenantId is not null) period.TenantId = tenantId;
        return period;
    }

    public static EmployeeInvoice Invoice(string employeeId, string payPeriodId, string? tenantId = null)
    {
        var invoice = EmployeeInvoice.Create(
            employeeId: employeeId,
            payPeriodId: payPeriodId,
            totalOrders: 1,
            subTotal: 1000m,
            currencyId: CurrencyId);
        if (tenantId is not null) invoice.TenantId = tenantId;
        return invoice;
    }

    /// <summary>A simple Order owned by <paramref name="ownerUserId"/> with one open assignment spot and
    /// status New (so a cleaner can Take it).</summary>
    public static Order NewOrder(string ownerUserId, string customerEmail, string? tenantId = null)
    {
        var address = Address.Create("Order St 9", "Brno", "60200", CountryId);
        var order = Order.Create(
            customerName: "Order Owner",
            customerEmail: customerEmail,
            customerPhone: "+420777333444",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: ownerUserId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (tenantId is not null) order.TenantId = tenantId;
        return order;
    }

    public static Dispute Dispute(string orderId, string ownerUserId, string? tenantId = null)
    {
        var dispute = new Dispute(
            orderId: orderId,
            userId: ownerUserId,
            reason: DisputeReason.Other,
            description: "host-test dispute description",
            createdBy: ownerUserId);
        if (tenantId is not null) dispute.TenantId = tenantId;
        return dispute;
    }
}
