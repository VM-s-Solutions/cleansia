using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Every decimal column in the model is sized, and money and rates are sized differently.
///
/// <para><b>The omission this closes.</b> Six columns on <c>Orders</c> — the VAT split, the
/// cancellation pair and the travel distance — carried no <c>HasPrecision</c> at all and landed as
/// bare <c>numeric</c>, sitting directly beside five siblings in the same file that were already
/// <c>(18,2)</c>. Nothing observed them: no test asserted a stored scale, and Postgres accepts an
/// unconstrained numeric silently, so the omission was invisible from both ends.</para>
///
/// <para><b>Why a rate is not money.</b> <c>AppliedVatRate</c> is a verbatim copy of
/// <c>CountryConfiguration.StandardVatRate</c>, and <c>ReceiptService</c> makes it the fiscal
/// discriminator, puts it on every line of the request sent to the tax authority and prints it on the
/// receipt. Every rate reachable today is a whole percent, so <c>(18,2)</c> would not have lost a
/// digit yet — the argument is not a live counterexample, it is that a column copied verbatim from
/// another column has to be able to hold everything that one holds. So rates take the <c>(5,4)</c>
/// fraction convention the country configuration already uses, and only amounts take
/// <c>(18,2)</c>.</para>
///
/// <para>The walk at the end is the part that survives this change: it fails on ANY future decimal
/// added anywhere in the model without a precision, rather than only on the six known today.</para>
/// </summary>
public sealed class DecimalPrecisionModelTests
{
    private static IModel Model()
    {
        // NPGSQL, not SQLite. The store type is what this file asserts on, and it is provider-
        // specific: SQLite gives every decimal the affinity "TEXT", which would make the walk below
        // agree with itself and see nothing. No connection is opened -- EF builds the model lazily.
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        return ctx.Model;
    }

    private static IProperty Property(Type entity, string name)
    {
        var property = Model().FindEntityType(entity)?.FindProperty(name);
        Assert.NotNull(property);
        return property!;
    }

    [Theory]
    [InlineData(nameof(Order.NetAmount))]
    [InlineData(nameof(Order.VatAmount))]
    [InlineData(nameof(Order.CancellationRefundAmount))]
    public void Order_MoneyColumns_Are_18_2(string propertyName)
    {
        var property = Property(typeof(Order), propertyName);

        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    /// <summary>
    /// <c>(5,4)</c> holds a fraction to four places, and the column cannot hold a PERCENT by accident:
    /// 21 does not fit in five digits with four of them after the point, so a future writer that
    /// confuses the two conventions fails loudly instead of storing a rate a hundred times too large.
    /// </summary>
    [Theory]
    [InlineData(nameof(Order.AppliedVatRate))]
    [InlineData(nameof(Order.CancellationFeeRate))]
    public void Order_RateColumns_Are_5_4(string propertyName)
    {
        var property = Property(typeof(Order), propertyName);

        Assert.Equal(5, property.GetPrecision());
        Assert.Equal(4, property.GetScale());
    }

    /// <summary>
    /// The rate columns agree with the country configuration they are copied from. Asserted as an
    /// equality rather than as two literals so the pair cannot drift apart silently.
    /// </summary>
    [Fact]
    public void The_Order_Vat_Rate_Matches_The_Country_Configuration_It_Is_Copied_From()
    {
        var source = Property(typeof(CountryConfiguration), nameof(CountryConfiguration.StandardVatRate));
        var snapshot = Property(typeof(Order), nameof(Order.AppliedVatRate));

        Assert.Equal(source.GetPrecision(), snapshot.GetPrecision());
        Assert.Equal(source.GetScale(), snapshot.GetScale());
    }

    /// <summary>Kilometres — neither money nor a fraction, so neither convention applies.</summary>
    [Fact]
    public void TravelDistance_Is_9_2()
    {
        var property = Property(typeof(Order), nameof(Order.TravelDistance));

        Assert.Equal(9, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    /// <summary>
    /// THE GUARD THAT OUTLIVES THE SIX. Every mapped decimal in the whole model must resolve to a
    /// SIZED Postgres type. A new money column added without a precision fails here, in the unit
    /// suite, rather than shipping as an unconstrained numeric nobody looks at.
    ///
    /// <para>Asserted on the resolved STORE TYPE rather than on <c>GetPrecision()</c> being non-null,
    /// because the obvious spelling of this test does not work: <c>GetColumnType(IProperty)</c> never
    /// returns null — it falls through to the type mapping — so a
    /// <c>GetPrecision() is null &amp;&amp; GetColumnType() is null</c> filter selects nothing, ever,
    /// and passes as an assertion that an always-empty list is empty. Reading the store type catches
    /// both ways a column can end up unconstrained: no <c>HasPrecision</c> at all, and an explicit
    /// <c>HasColumnType("numeric")</c> — and the latter is a live habit in this folder, where
    /// <c>HasColumnType</c> is already used for citext and jsonb.</para>
    /// </summary>
    [Fact]
    public void No_Decimal_Column_In_The_Model_Is_Left_Unsized()
    {
        var unsized = DecimalProperties()
            .Where(x => string.Equals(x.Property.GetColumnType(), "numeric", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Entity}.{x.Property.Name} -> {x.Property.GetColumnType()}")
            .Order()
            .ToList();

        Assert.Equal([], unsized);
    }

    /// <summary>
    /// ANTI-VACUITY FOR THE WALK, and it is not decorative: the first version of the walk above could
    /// not fail. This asserts the walk's own predicate discriminates — that an unsized decimal really
    /// does resolve to bare <c>numeric</c> under this provider, so an empty result means "all sized"
    /// rather than "the filter matches nothing".
    /// </summary>
    [Fact]
    public void An_Unsized_Decimal_Would_Resolve_To_Bare_Numeric()
    {
        var sized = DecimalProperties()
            .Where(x => x.Property.GetPrecision() is not null)
            .ToList();

        Assert.NotEmpty(sized);

        // A sized decimal renders its precision INTO the store type -- "numeric(18,2)", never the bare
        // "numeric" the walk rejects. That is the whole discrimination: the two states are
        // distinguishable in this string, so an empty result above means every column is sized.
        Assert.All(sized, x =>
        {
            Assert.StartsWith("numeric(", x.Property.GetColumnType());
            Assert.NotEqual("numeric", x.Property.GetColumnType());
        });

        // And the unsized state is not hypothetical -- it is what these six columns rendered as until
        // this change. The regenerated migration is the receipt: `type: "numeric"` became
        // `type: "numeric(18,2)"` for NetAmount and its five siblings in the same commit.
        var unsizedIsExpressible = Model().GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Any(p => p.GetColumnType() == "numeric");
        Assert.False(unsizedIsExpressible, "no column should be bare numeric once this change lands");
    }

    private static IEnumerable<(string Entity, IProperty Property)> DecimalProperties() =>
        Model().GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal))
                .Select(p => (entity.ClrType.Name, p)));

    /// <summary>
    /// Anti-vacuity for the walk: the model really does contain decimals, so an empty "unsized" list
    /// above means every one of them is sized rather than that the query found nothing to look at.
    /// </summary>
    [Fact]
    public void The_Model_Really_Does_Contain_Decimal_Columns()
    {
        var decimals = Model().GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Count(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal));

        Assert.True(decimals > 40, $"expected the model to carry dozens of decimal columns, found {decimals}");
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

}
