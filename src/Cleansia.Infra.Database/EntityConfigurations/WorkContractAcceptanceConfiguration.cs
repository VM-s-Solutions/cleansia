using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class WorkContractAcceptanceConfiguration : TenantAuditableEntityConfiguration<WorkContractAcceptance, string>
{
    public override void Configure(EntityTypeBuilder<WorkContractAcceptance> builder)
    {
        base.Configure(builder);

        builder.ToTable("WorkContractAcceptances");

        builder.Property(a => a.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        // The job and the exact text row are real foreign keys, Restrict: an acceptance without either
        // is not a record of anything. No navigation on either side — the readers join by id.
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(a => a.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bare scalars, no FKs. The seat row is hard-deleted by the next drop, cover, rejection or
        // reassignment, and the cleaner is anonymised on erasure; this row must survive both.
        builder.Property(a => a.OrderEmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(a => a.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(a => a.LegalDocumentTextId)
            .IsRequired()
            .HasMaxLength(26);

        // Named: the conventional name runs past Postgres's identifier limit and lands truncated.
        builder.HasOne<LegalDocumentText>()
            .WithMany()
            .HasForeignKey(a => a.LegalDocumentTextId)
            .HasConstraintName("FK_WorkContractAcceptances_LegalDocumentTexts_TextId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.DocumentVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(a => a.AcceptedOn)
            .IsRequired();

        builder.Property(a => a.ClientAudience)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.DeviceLabel)
            .HasMaxLength(120);

        builder.Property(a => a.DeviceId)
            .HasMaxLength(64);

        builder.Property(a => a.FactsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        // One contract per seat: the legal truth and the arbiter of a concurrent double accept — two
        // requests that both read "no row" for the seat are separated here, at commit.
        builder.HasIndex(a => a.OrderEmployeeId)
            .IsUnique()
            .HasDatabaseName("IX_WorkContractAcceptances_OrderEmployeeId");

        builder.HasIndex(a => new { a.OrderId, a.EmployeeId })
            .HasDatabaseName("IX_WorkContractAcceptances_OrderId_EmployeeId");

        builder.HasIndex(a => new { a.EmployeeId, a.AcceptedOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_WorkContractAcceptances_EmployeeId_AcceptedOn");

        // The per-company metadata sweep and the archive stream read the table by company and age.
        builder.HasIndex(a => new { a.TenantId, a.AcceptedOn })
            .HasDatabaseName("IX_WorkContractAcceptances_TenantId_AcceptedOn");
    }
}
