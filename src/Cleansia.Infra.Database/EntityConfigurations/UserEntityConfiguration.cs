using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserEntityConfiguration : AuditableEntityConfiguration<User, string>
{
    public override void Configure(EntityTypeBuilder<User> builder)
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

        builder.Property(u => u.PreferredLanguageCode)
            .HasMaxLength(5)
            .IsRequired(false);

        builder
            .HasOne(u => u.PreferredLanguage)
            .WithMany()
            .HasForeignKey(u => u.PreferredLanguageCode)
            .HasPrincipalKey(l => l.Code)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder
            .HasMany(u => u.Orders)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}