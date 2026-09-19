using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cleansia.Infra.Database;

/// <summary>
/// Which stamped rows a frozen company's freeze lets through (ADR-0064 D3). The account surface is
/// the person's, not the company's: a customer of a closed company keeps signing in, consenting,
/// being notified and erased, and the request pipeline's own audit row and the two queue envelopes
/// commit beside whatever they describe. Everything else is books and is guarded by default, so a
/// new stamped table fails closed until it is sorted here or in the roster test. <c>CreditAccount</c>
/// is books: a balance is a liability on the company's ledger.
/// </summary>
public static class ArchivedCompanyWriteGuard
{
    public static readonly IReadOnlySet<Type> AccountSurface = new HashSet<Type>
    {
        typeof(User),
        typeof(RefreshToken),
        typeof(Device),
        typeof(LiveActivityToken),
        typeof(Cart),
        typeof(SavedAddress),
        typeof(UserConsent),
        typeof(UserNotificationPreferences),
        typeof(UserNotification),
        typeof(GdprRequest),
        typeof(UserStripeCustomer),
        typeof(UserMembership),
        typeof(MembershipBenefitUsage),
        typeof(LoyaltyAccount),
        typeof(LoyaltyTransaction),
        typeof(ReferralCode),
        typeof(Referral),
        typeof(AdminActionAudit),
        typeof(CustomerActionAudit),
        typeof(EmployeeActionAudit),
        typeof(OutboxMessage),
        typeof(DeadLetter),
    };

    /// <summary>The companies whose books this unit of work is about to change.</summary>
    public static IReadOnlyCollection<string> TouchedBooksTenantIds(ChangeTracker changeTracker)
    {
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in changeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || entry.Entity.TenantId is null
                || AccountSurface.Contains(entry.Metadata.ClrType))
            {
                continue;
            }

            touched.Add(entry.Entity.TenantId);
        }

        return touched;
    }
}
