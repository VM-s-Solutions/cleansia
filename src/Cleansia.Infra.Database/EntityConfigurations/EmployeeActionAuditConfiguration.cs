using Cleansia.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeActionAuditConfiguration : AuditableEntityConfiguration<EmployeeActionAudit, string>
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

        // ONE declared index, for the one question the table exists to answer: "what has this cleaner
        // done, most recent first". CreatedOn descending because every read of a history is newest-first.
        //
        // No index on OrderId and none on (CreatedOn) alone: neither has a caller today, and a
        // speculative index is paid on every insert forever. They arrive with the query that needs them.
        //
        // The inherited TenantId index comes from AuditableEntityConfiguration and is accepted as-is;
        // 61 configurations carry it. It is NOT a uniqueness arbiter here — nothing on this table is
        // unique, which is what append-only means.
        builder.HasIndex(e => new { e.EmployeeId, e.CreatedOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_EmployeeActionAudits_EmployeeId_CreatedOn");
    }
}
