using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

/// <summary>
/// Until this file existed, <c>OrderEmployee</c> was mapped entirely by convention — key, two
/// non-unique indexes, nothing else. That is what made the take race real: nothing in the database
/// bounded how many cleaners an order could carry, so two concurrent takes of the same single-seat job
/// both passed the in-memory checks and both inserted.
/// </summary>
public class OrderEmployeeEntityConfiguration : BaseEntityConfiguration<OrderEmployee, string>
{
    public override void Configure(EntityTypeBuilder<OrderEmployee> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderEmployees");

        builder.Property(oe => oe.SeatOrdinal).IsRequired();

        // The seat arbiter. Every other contested resource in this schema is guarded the same way —
        // MembershipBenefitUsage on (…, SlotOrdinal), PromoCodeRedemption on (…, SlotOrdinal) — and the
        // order seat was the only one without. The loser of a concurrent take is rejected HERE, at
        // commit, not by the three unlocked reads in TakeOrder; the handler turns the violation back into
        // the ordinary NoAvailableSpots refusal.
        //
        // No .AreNullsDistinct(false) and no filter, deliberately: both columns are non-nullable, and
        // UnassignEmployee hard-DELETEs the row (there is no soft-delete interceptor and no IsActive
        // query filter), so a released seat frees its ordinal by disappearing.
        // Deliberately NOT indexed. The reminder sweeps lead on Orders.CurrentStatus plus a narrow
        // CleaningDateTime range, which IX_Orders_CurrentStatus_CleaningDateTime already serves; these
        // stamps are a residual filter over the handful of assignment rows that survives it, and a
        // partial index on the non-null rows would index precisely what the predicate excludes. The
        // counter-precedent exists — UserMembership carries partial indexes on its reminder stamps —
        // but that one was forced by a measured seq-scan on a query with no such leading terms.
        builder.Property(oe => oe.ReminderSoonSentAt).IsRequired(false);
        builder.Property(oe => oe.ReminderNotStartedSentAt).IsRequired(false);

        builder.HasIndex(oe => new { oe.OrderId, oe.SeatOrdinal })
            .IsUnique()
            .HasDatabaseName("IX_OrderEmployees_OrderId_SeatOrdinal");
    }
}
