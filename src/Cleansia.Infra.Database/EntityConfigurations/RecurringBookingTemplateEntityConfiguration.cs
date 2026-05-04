using Cleansia.Core.Domain.Bookings;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class RecurringBookingTemplateEntityConfiguration : AuditableEntityConfiguration<RecurringBookingTemplate, string>
{
    public override void Configure(EntityTypeBuilder<RecurringBookingTemplate> builder)
    {
        base.Configure(builder);

        builder.ToTable("RecurringBookingTemplates");

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(t => t.SavedAddressId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(t => t.Frequency)
            .IsRequired();

        builder.Property(t => t.DayOfWeek)
            .IsRequired();

        builder.Property(t => t.TimeOfDay)
            .IsRequired();

        builder.Property(t => t.Rooms);
        builder.Property(t => t.Bathrooms);
        builder.Property(t => t.PaymentType).IsRequired();
        builder.Property(t => t.StartsOn).IsRequired();
        builder.Property(t => t.EndsOn);
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.LastMaterializedFor);

        // Service / package id collections persisted as JSON, backed by the
        // private `_selectedServiceIds` / `_selectedPackageIds` fields. Same
        // JsonValueConverter pattern as Order.Extras.
        builder.Property<List<string>>("_selectedServiceIds")
            .HasColumnName("SelectedServiceIds")
            .HasField("_selectedServiceIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new JsonValueConverter<List<string>>())
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>>());

        builder.Property<List<string>>("_selectedPackageIds")
            .HasColumnName("SelectedPackageIds")
            .HasField("_selectedPackageIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new JsonValueConverter<List<string>>())
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>>());

        // The public `SelectedServiceIds` / `SelectedPackageIds` properties
        // are computed projections of the backing fields — EF doesn't need to
        // map them since the field-backed Property above persists the data.
        builder.Ignore(t => t.SelectedServiceIds);
        builder.Ignore(t => t.SelectedPackageIds);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Materializer query: active templates whose StartsOn has passed.
        // Composite index keeps the daily scan O(active templates) not O(all).
        builder.HasIndex(t => new { t.IsActive, t.StartsOn });
    }
}
