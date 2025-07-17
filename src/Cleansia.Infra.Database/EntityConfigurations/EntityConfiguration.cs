using Cleansia.Core.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T>
    where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasMaxLength(26)
            .IsRequired();
    }
}

public class AuditableEntityConfiguration<T, TKey> : BaseEntityConfiguration<T>
    where T : Auditable
{
    public override void Configure(EntityTypeBuilder<T> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.CreatedOn)
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(255);

        builder.Property(e => e.UpdatedOn)
            .IsRequired(false);

        builder.Property(e => e.DeactivatedBy)
            .HasMaxLength(255);

        builder.Property(e => e.DeactivatedOn)
            .IsRequired(false);
    }
}