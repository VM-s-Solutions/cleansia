using Cleansia.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeActionAuditConfiguration : TenantAuditableEntityConfiguration<EmployeeActionAudit, string>
{
    public override void Configure(EntityTypeBuilder<EmployeeActionAudit> builder)
    {
        base.Configure(builder);

        builder.ToTable("EmployeeActionAudits");

        // Bare scalars, no FKs. The act being recorded DELETES the OrderEmployee row it describes, and
        // an order can be anonymised under erasure while this row must survive — the same argument
        // CreditTransaction makes for not FK-ing its own OrderId.
        builder.Property(e => e.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasConversion<int>();

        // Two declared indexes, one per reader, CreatedOn descending because every read of a history is
        // newest-first: the cleaner's own history ("what has this cleaner done"), and the customer
        // timeline, which reads the table by OrderId — one order by resource, the user's recent orders
        // by user. None on (CreatedOn) alone: no caller, and a speculative index is paid on every insert.
        //
        // The inherited TenantId index comes from AuditableEntityConfiguration and is accepted as-is;
        // 61 configurations carry it. It is NOT a uniqueness arbiter here — nothing on this table is
        // unique, which is what append-only means.
        builder.HasIndex(e => new { e.EmployeeId, e.CreatedOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_EmployeeActionAudits_EmployeeId_CreatedOn");

        builder.HasIndex(e => new { e.OrderId, e.CreatedOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_EmployeeActionAudits_OrderId_CreatedOn");
    }
}
