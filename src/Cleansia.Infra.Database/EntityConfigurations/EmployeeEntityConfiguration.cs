using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeEntityConfiguration : AuditableEntityConfiguration<Employee, string>
{
    public override void Configure(EntityTypeBuilder<Employee> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasDefaultValue(Core.Domain.Enums.EmployeeEntityType.NaturalPerson);

        builder.Property(e => e.RegistrationNumber)
            .HasMaxLength(50);

        builder.Property(e => e.VatNumber)
            .HasMaxLength(50);

        builder.Property(e => e.LegalEntityName)
            .HasMaxLength(200);

        builder.Property(e => e.PassportId)
            .HasMaxLength(50);

        builder.Property(e => e.IBAN)
            .HasMaxLength(50);

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

        builder.Property(s => s.Availability)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, List<TimeRange>>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, List<TimeRange>>>());
    }
}