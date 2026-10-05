using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeEntityConfiguration : TenantAuditableEntityConfiguration<Employee, string>
{
    public override void Configure(EntityTypeBuilder<Employee> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasDefaultValue(Core.Domain.Enums.EmployeeEntityType.NaturalPerson);

        // Approval asks the register about the number it loaded, which can take seconds, and a Pending
        // cleaner's save checks less than approval does. Either write that commits second was judged
        // against a row that no longer holds, so it is refused at commit rather than leaving an approved
        // cleaner on a number nobody checked at approval grade.
        builder.Property(e => e.RegistrationNumber)
            .HasMaxLength(50)
            .IsConcurrencyToken();

        builder.Property(e => e.ContractStatus)
            .IsConcurrencyToken();

        builder.Property(e => e.LegalEntityName)
            .HasMaxLength(200);

        builder.Property(e => e.PassportId)
            .HasMaxLength(50);

        builder.Property(e => e.IBAN)
            .HasMaxLength(50);

        builder.Property(e => e.HasPayoutDetails)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.EmergencyContactName)
            .HasMaxLength(100);

        builder.Property(e => e.EmergencyContactPhone)
            .HasMaxLength(20);

        builder.Property(e => e.AverageRating)
            .HasPrecision(18, 2)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ComplaintsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder
            .HasOne(e => e.User)
            .WithOne(u => u.Employee)
            .HasForeignKey<Employee>(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(e => e.Nationality)
            .WithMany(c => c.Employees)
            .HasForeignKey(e => e.NationalityId)
            .OnDelete(DeleteBehavior.NoAction);

        // Work-country FK. One-sided nav (no reverse collection on
        // Country) — keeps the Country entity surface small and we
        // don't need to iterate "all employees who work here" from
        // the Country side. Restrict on delete: a country can't be
        // hard-deleted while employees are scoped to it; admin would
        // re-scope them first.
        builder
            .HasOne(e => e.WorkCountry)
            .WithMany()
            .HasForeignKey(e => e.WorkCountryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.WorkCountryId);

        // Index the contract status — the new-jobs digest sweep filters
        // employees by `ContractStatus ∈ {Approved, Active}` on every
        // 30-min tick, joined with WorkCountryId. WorkCountryId is already
        // indexed; an index on ContractStatus too keeps the sweep cheap
        // even as the employee table grows.
        builder.HasIndex(e => e.ContractStatus);
    }
}