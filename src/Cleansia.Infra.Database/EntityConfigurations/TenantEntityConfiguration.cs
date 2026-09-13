using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class TenantEntityConfiguration : BaseEntityConfiguration<Tenant, string>
{
    public override void Configure(EntityTypeBuilder<Tenant> builder)
    {
        base.Configure(builder);

        builder.ToTable("Tenants");

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);
    }
}
