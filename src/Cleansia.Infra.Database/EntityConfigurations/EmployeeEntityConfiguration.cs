using Cleansia.Core.Domain.Internalization;
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

        builder.Property(u => u.Password)
            .HasMaxLength(255)
            .HasConversion(new PasswordConverter());

        builder.Property(u => u.FirstName)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.LastName)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Email)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.PhoneNumber)
            .HasColumnType("citext")
            .HasMaxLength(50);

        builder.Property(u => u.GoogleId)
            .HasMaxLength(512);

        builder.Property(u => u.ResetPasswordCode)
            .HasMaxLength(6);

        builder.Property(u => u.Profile)
            .HasConversion<int>();

        builder.Property(u => u.AuthenticationType)
            .HasConversion<int>();

        builder.Property(u => u.ConfirmationCode)
            .HasMaxLength(6);

        builder
            .HasMany(u => u.Orders)
            .WithOne(o => o.Employee)
            .HasForeignKey(o => o.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.ICO)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.AverageRating)
            .HasPrecision(18, 2)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ComplaintsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.Availability)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, List<TimeRange>>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, List<TimeRange>>>());
    }
}