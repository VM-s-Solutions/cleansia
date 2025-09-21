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

        builder
            .HasMany(u => u.Orders)
            .WithOne(o => o.Employee)
            .HasForeignKey(o => o.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.ICO)
            .HasMaxLength(50);

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

        builder.Property(s => s.Availability)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, List<TimeRange>>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, List<TimeRange>>>());
    }
}