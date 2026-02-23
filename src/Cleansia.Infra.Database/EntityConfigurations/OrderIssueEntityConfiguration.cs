using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderIssueEntityConfiguration : AuditableEntityConfiguration<OrderIssue, string>
{
    public override void Configure(EntityTypeBuilder<OrderIssue> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderIssues");

        builder.Property(i => i.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(i => i.ReportedByEmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(i => i.IsResolved)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.ResolvedAt)
            .IsRequired(false);

        builder.HasOne(i => i.Order)
            .WithMany(o => o.OrderIssues)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.OrderId)
            .HasDatabaseName("IX_OrderIssues_OrderId");
    }
}
