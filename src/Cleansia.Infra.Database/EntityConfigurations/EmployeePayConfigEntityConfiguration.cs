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
        builder.HasIndex(e => new { e.EmployeeId, e.ServiceId, e.PackageId })
            .IsUnique()
            .HasFilter("\"EmployeeId\" IS NOT NULL");
    }
}
