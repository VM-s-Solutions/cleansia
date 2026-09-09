using Cleansia.Core.Domain.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeePayConfigEntityConfiguration : AuditableEntityConfiguration<EmployeePayConfig, string>
{
    public override void Configure(EntityTypeBuilder<EmployeePayConfig> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.EmployeeId)
            .HasMaxLength(26);

        builder.Property(e => e.ServiceId)
            .HasMaxLength(26);

        builder.Property(e => e.PackageId)
            .HasMaxLength(26);

        builder.Property(e => e.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.BasePay)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.ExtraPerRoom)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.ExtraPerBathroom)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.DistanceRatePerKm)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.MinimumPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.MaximumPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.HasOne(e => e.Service)
            .WithMany()
            .HasForeignKey(e => e.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Package)
            .WithMany()
            .HasForeignKey(e => e.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ServiceId);
        builder.HasIndex(e => e.PackageId);
        builder.HasIndex(e => e.EmployeeId);
        builder.HasIndex(e => new { e.ServiceId, e.PackageId });
        // One pay config per target per scope — a platform-wide row (EmployeeId null) or one
        // cleaner's override of it.
        //
        // NULLS NOT DISTINCT, and UNFILTERED, because this index has never rejected a single row.
        // Every row carries a null by construction: CreateForService sets PackageId null,
        // CreateForPackage sets ServiceId null, and the validator forbids both — so with nulls
        // distinct every tuple was unique and the constraint was decoration. The filter then
        // excluded exactly the rows that matter most, the platform-wide defaults the seed writes.
        //
        // It matters because the estimator does not choose between duplicates, it takes whichever
        // Postgres hands back first: CalculateOrderPay.SelectPreferredConfigs is
        // `g.FirstOrDefault(c => c.EmployeeId != null) ?? g.First()` with no ORDER BY. Two
        // platform-wide rows for one service means a cleaner's pay depends on row order.
        // CreatePayConfig's validator already checks for a duplicate; a check-then-add is not an
        // arbiter under concurrency, which is the one thing a unique index is for.
        // CURRENCYID IS PART OF THE KEY. A pay rate is an amount in a currency, and without this term
        // a cleaner could hold exactly ONE rate for a service across the whole platform -- so a second
        // market could not pay anybody. Adding it is what makes per-currency pay storable at all.
        //
        // It is also the term that makes the reads below it safe to widen: with the currency in the
        // key there is at most one platform-wide row and one override per (target, currency), so the
        // estimator's `FirstOrDefault(EmployeeId != null) ?? First()` is choosing between exactly those
        // two rather than between an unbounded set. Widening the index WITHOUT scoping the reads would
        // have made the arbitrary pick worse instead of fixing it, which is why they move together.
        //
        // .AreNullsDistinct(false) STAYS. Every config carries a null by construction -- one is written
        // per service OR per package, never both, and EmployeeId is null on the platform-wide rows --
        // so without it Postgres treats the tuples as distinct and the index enforces nothing.
        // NullsNotDistinctIndexModelTests walks every unique index in the model and fails any that
        // drops it, which is what would catch this if a future edit reformats the chain.
        builder.HasIndex(e => new { e.EmployeeId, e.ServiceId, e.PackageId, e.CurrencyId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
