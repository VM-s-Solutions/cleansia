using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderNoteEntityConfiguration : AuditableEntityConfiguration<OrderNote, string>
{
    public override void Configure(EntityTypeBuilder<OrderNote> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderNotes");

        builder.Property(n => n.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(n => n.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(n => n.Order)
            .WithMany(o => o.OrderNotes)
            .HasForeignKey(n => n.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.OrderId)
            .HasDatabaseName("IX_OrderNotes_OrderId");
    }
}
