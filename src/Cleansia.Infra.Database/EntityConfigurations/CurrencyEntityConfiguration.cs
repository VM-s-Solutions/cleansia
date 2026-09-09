using Cleansia.Core.Domain.Internationalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CurrencyEntityConfiguration : AuditableEntityConfiguration<Currency, string>
{
    public override void Configure(EntityTypeBuilder<Currency> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(5)
            .HasColumnType("citext");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("citext");

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.ExchangeRate)
            .IsRequired()
            .HasPrecision(18, 6);

        // A CODE NAMES A CURRENCY, so two rows may not answer to the same one. Until now nothing
        // enforced that: the only guard was a FluentValidation existence check on the create path,
        // which crosses a snapshot boundary with no lock, and DeleteCurrency's hard Remove frees the
        // slot cleanly so no IsActive predicate is needed here.
        //
        // The reader that makes duplicates expensive is not the admin screen. GetByCodeAsync is an
        // unordered FirstOrDefault, and GenerateInvoice resolves an employee's payout currency through
        // it -- so a duplicate would stamp a cleaner's tax document with whichever row Postgres
        // happened to return. Code is citext, so this is case-insensitive: 'czk' and 'CZK' collide,
        // which is what a currency code means.
        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_Currencies_Code_Unique");

        // EXACTLY ONE DEFAULT, enforced by the database rather than by the one handler that happens to
        // clear before it sets. A partial unique index over a column whose filter pins that column to a
        // single value admits exactly one row -- the same shape SavedAddresses already uses for its
        // per-user default, minus the user term because this default is platform-wide.
        //
        // No IsActive predicate, unlike SavedAddresses: that filter exists there because a soft delete
        // leaves IsDefault set on the removed row and would occupy the slot forever. Currencies are hard
        // deleted, and an INACTIVE default is a state SetDefaultCurrency already refuses to create.
        //
        // POSTGRES CANNOT DEFER THIS. A partial unique index is not a constraint, so DEFERRABLE is not
        // available and the check lands at the end of every statement -- which is why SetDefaultCurrency
        // now flushes the clear before it emits the promote instead of leaving both to one batch whose
        // statement order EF does not guarantee.
        builder.HasIndex(c => c.IsDefault)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true")
            .HasDatabaseName("IX_Currencies_IsDefault_Unique");
    }
}