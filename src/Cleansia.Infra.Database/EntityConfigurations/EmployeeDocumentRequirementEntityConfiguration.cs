using Cleansia.Core.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeDocumentRequirementEntityConfiguration
    : AuditableEntityConfiguration<EmployeeDocumentRequirement, string>
{
    public override void Configure(EntityTypeBuilder<EmployeeDocumentRequirement> builder)
    {
        base.Configure(builder);

        builder.Property(r => r.CountryId).IsRequired();
        builder.Property(r => r.DocumentType).IsRequired();
        builder.Property(r => r.IsRequired).IsRequired();
        builder.Property(r => r.SortOrder).IsRequired();

        builder
            .HasOne(r => r.Country)
            .WithMany()
            .HasForeignKey(r => r.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CountryId);

        // One row per (country, type). A second row for the same pair is not a variant of the rule,
        // it is two rules disagreeing — and whichever the query happened to read first would win.
        //
        // NULLS NOT DISTINCT is deliberately absent because neither column is nullable, so the
        // single-tenant NULL hole that makes (TenantId, ...) indexes toothless does not apply here.
        builder
            .HasIndex(r => new { r.CountryId, r.DocumentType })
            .IsUnique();
    }
}
