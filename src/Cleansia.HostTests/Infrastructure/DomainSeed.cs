using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
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

    /// <summary>A registration-complete employee that the admin has REJECTED. Profile +
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
        if (tenantId is not null)
        {
            employee.TenantId = tenantId;
            // The address is ITenantEntity; seeding runs outside an HTTP request so the interceptor would
            // stamp it null, and the in-tenant caller's filtered .Include(e => e.Address) would then not
            // see it (employee looks profile-incomplete). Stamp it to the employee's tenant explicitly.
            address.TenantId = tenantId;
        }
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
        var newTrack = OrderStatusTrack.Create(OrderStatus.New, order);
        order.AddOrderStatus(newTrack);
        if (tenantId is not null)
        {
            order.TenantId = tenantId;
            // Seeding runs outside an HTTP request, so the interceptor's tenant auto-stamp resolves null;
            // child rows (status track, customer address — both ITenantEntity) must carry the order's
            // tenant explicitly, otherwise the tenant filter on an Include() hides them from the in-tenant
            // caller and the order looks status-less / address-less.
            newTrack.TenantId = tenantId;
            address.TenantId = tenantId;
        }
        return order;
    }

    /// <summary>Assign <paramref name="employee"/> to <paramref name="order"/> and advance it to
    /// Confirmed — the state StartOrder requires. Mirrors what TakeOrder.Handler does, so a cleaner who
    /// is NOT this assignee trips EmployeeNotAssignedToOrder on StartOrder.
    ///
    /// The status tracks are stamped with explicit, ORDERED CreatedOn values: in production New and
    /// Confirmed are written in separate commits, but a single seed commit stamps every Added row with
    /// the same interceptor timestamp (a tie that makes OrderByDescending(CreatedOn) nondeterministic).
    /// Setting CreatedBy here also opts the rows out of the auto-stamp so these explicit times survive.</summary>
    public static void ConfirmAndAssign(Order order, Employee employee)
    {
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));

        var now = DateTimeOffset.UtcNow;
        foreach (var track in order.OrderStatusHistory)
        {
            track.Created("seed", now.AddMinutes(-5));
            track.TenantId = order.TenantId;
        }

        var confirmed = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
        confirmed.Created("seed", now);
        confirmed.TenantId = order.TenantId;
        order.AddOrderStatus(confirmed);
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

    /// <summary>An <see cref="Address"/> + <see cref="SavedAddress"/> pair owned by
    /// <paramref name="ownerUserId"/>. Both are <c>ITenantEntity</c>, so both are stamped with the same
    /// <paramref name="tenantId"/> — the SavedAddress repo's <c>GetByIdAsync</c> is tenant-filtered, so a
    /// foreign-tenant caller can never see the row (existence fails → NotFound) while the in-tenant
    /// ownership gate (BeOwnedByCaller) is what a same-tenant, different-user caller trips.</summary>
    public static (Address address, SavedAddress saved) SavedAddressFor(
        string ownerUserId, string label = "Home", string? tenantId = null)
    {
        var address = Address.Create("Saved St 7", "Plzen", "30100", CountryId, null, 49.7475, 13.3776);
        if (tenantId is not null) address.TenantId = tenantId;

        var saved = SavedAddress.Create(ownerUserId, address.Id, label, isDefault: false);
        if (tenantId is not null) saved.TenantId = tenantId;
        return (address, saved);
    }

    public static MembershipPlan MembershipPlan(string code = "HOSTTEST-MONTHLY", string? tenantId = null)
    {
        var plan = Cleansia.Core.Domain.Memberships.MembershipPlan.Create(
            code: code,
            name: "Host-test plan",
            monthlyPriceCzk: 299m,
            stripePriceId: "price_hosttest",
            discountPercentage: 10m,
            freeCancellationWindowHours: 24,
            allowsExpressUpgrade: true);
        if (tenantId is not null) plan.TenantId = tenantId;
        return plan;
    }

    /// <summary>An ACTIVE <see cref="UserMembership"/> for <paramref name="ownerUserId"/> with a period
    /// that ends in the future (so <c>IsActive</c> and <c>GetActiveForUserAsync</c> resolve it). The
    /// resolve is tenant-filtered, so a foreign-tenant caller resolves null → MembershipNotFound.</summary>
    public static UserMembership ActiveMembership(string ownerUserId, string membershipPlanId, string? tenantId = null)
    {
        var membership = UserMembership.Create(
            userId: ownerUserId,
            membershipPlanId: membershipPlanId,
            stripeSubscriptionId: "sub_hosttest",
            currentPeriodStart: DateTime.UtcNow.AddDays(-3),
            currentPeriodEnd: DateTime.UtcNow.AddDays(27));
        if (tenantId is not null) membership.TenantId = tenantId;
        return membership;
    }
}
