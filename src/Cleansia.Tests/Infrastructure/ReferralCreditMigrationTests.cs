using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Migrations;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// The committed <c>Initial</c> migration builds the referral credit the model maps (owner rulings
/// 2026-10-04 and 2026-10-05): the two tables the change touched carry exactly the model's columns, the
/// money columns are <c>numeric(18,2)</c>, each side's credit currency is a Restrict key into Currencies,
/// and the hold reasons are a short nullable text.
/// </summary>
public sealed class ReferralCreditMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ReferralCreditMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"),
        new FixedTenantProvider());

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }

    private static CreateTableOperation Table(string name) =>
        new Initial().UpOperations.OfType<CreateTableOperation>().Single(t => t.Name == name);

    [Theory]
    [InlineData(typeof(Referral))]
    [InlineData(typeof(Currency))]
    public void The_Migration_Creates_Exactly_The_Columns_The_Model_Maps(Type entityType)
    {
        using var ctx = NewContext();
        var entity = ctx.Model.FindEntityType(entityType)!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
        var mapped = entity.GetProperties().Select(p => p.GetColumnName(table)!).Order(StringComparer.Ordinal);

        // xmin is Postgres's row version: the migration declares it, the SQLite model this test builds cannot.
        var created = Table(entity.GetTableName()!).Columns.Select(c => c.Name)
            .Where(name => name != "xmin")
            .Order(StringComparer.Ordinal);

        Assert.Equal(mapped, created);
    }

    [Theory]
    [InlineData("Referrals", nameof(Referral.CreditAwardedToReferrer))]
    [InlineData("Referrals", nameof(Referral.CreditAwardedToReferred))]
    [InlineData("Currencies", nameof(Currency.ReferralCredit))]
    public void A_Referral_Credit_Column_Is_A_Nullable_Money_Column(string tableName, string columnName)
    {
        var column = Table(tableName).Columns.Single(c => c.Name == columnName);

        Assert.Equal("numeric(18,2)", column.ColumnType);
        Assert.True(column.IsNullable);
    }

    [Theory]
    [InlineData(nameof(Referral.ReferrerCreditCurrencyId))]
    [InlineData(nameof(Referral.ReferredCreditCurrencyId))]
    public void Each_Sides_Credit_Currency_Is_A_Restrict_Key_Into_Currencies(string columnName)
    {
        var foreignKey = Assert.Single(
            Table("Referrals").ForeignKeys,
            fk => fk.Columns.SequenceEqual([columnName]));

        Assert.Equal("Currencies", foreignKey.PrincipalTable);
        Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete);
        Assert.True(Table("Referrals").Columns.Single(c => c.Name == columnName).IsNullable);
    }

    [Fact]
    public void The_Hold_Reasons_Are_A_Nullable_Varchar_32()
    {
        var column = Table("Referrals").Columns.Single(c => c.Name == nameof(Referral.HoldReasons));

        Assert.Equal("character varying(32)", column.ColumnType);
        Assert.True(column.IsNullable);
    }

    [Fact]
    public void The_Migration_Maps_A_Referrals_Row_Version()
    {
        var xmin = Table("Referrals").Columns.Single(c => c.Name == "xmin");

        Assert.True(xmin.IsRowVersion);
    }
}
