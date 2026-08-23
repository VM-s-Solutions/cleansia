using Cleansia.Core.Domain.ServiceAreas;

namespace Cleansia.Tests.Domain.ServiceAreas;

/// <summary>
/// The gate that decides whether a customer may book at all.
///
/// <para>It was an exact case-insensitive compare, which refuses every spelling an operator did not
/// seed verbatim — and this repo's own seed contains such a pair: a customer address at <c>'Plzen'</c>
/// (<c>sql-scripts/seed/insert_addresses.sql:39</c>) against a serviced row <c>'Plzeň'</c>
/// (<c>insert_seed_data.sql:358</c>). That address could not be booked, and the customer found out at
/// payment.</para>
///
/// <para><b>The refusals below are the half that matters.</b> A rule that only widened would be
/// trivially satisfied by returning <c>true</c>; these pin what must still be turned away, and the
/// okres cases are the ones that make the district strip a decision rather than a convenience.</para>
/// </summary>
public class CityNameMatchTests
{
    /// <summary>Diacritics, in both directions — the provable in-repo defect.</summary>
    [Theory]
    [InlineData("Plzeň", "Plzen")]
    [InlineData("Plzen", "Plzeň")]
    [InlineData("České Budějovice", "Ceske Budejovice")]
    [InlineData("Ústí nad Labem", "Usti nad Labem")]
    [InlineData("Hradec Králové", "Hradec Kralove")]
    public void A_Spelling_Without_Diacritics_Is_The_Same_City(string row, string typed)
    {
        Assert.True(CityNameMatch.Matches(row, typed));
    }

    /// <summary>A district belongs to its city.</summary>
    [Theory]
    [InlineData("Praha", "Praha 8")]
    [InlineData("Praha", "Praha 22")]
    [InlineData("Prague", "Prague 8")]
    [InlineData("Praha", "Praha 4 - Chodov")]
    [InlineData("Praha", "Praha 5 – Smíchov")]
    [InlineData("Praha", "Praha 4-Chodov")]
    public void A_District_Is_Served_By_Its_City(string row, string typed)
    {
        Assert.True(CityNameMatch.Matches(row, typed));
    }

    [Theory]
    [InlineData("Praha", "  PRAHA  ")]
    [InlineData("Hradec Králové", "Hradec  Kralove")]
    public void Case_And_Spacing_Are_Not_A_Difference(string row, string typed)
    {
        Assert.True(CityNameMatch.Matches(row, typed));
    }

    /// <summary>
    /// <b>An okres is not a district.</b> <c>Praha-západ</c>, <c>Praha-východ</c> and <c>Brno-venkov</c>
    /// are the rural rings AROUND those cities and share the exact syntax of a quarter. A strip that
    /// admitted them would serve the countryside on the strength of a row for the city — which is why
    /// the pattern requires a NUMBER before the dash.
    /// </summary>
    [Theory]
    [InlineData("Praha", "Praha-západ")]
    [InlineData("Praha", "Praha-východ")]
    [InlineData("Brno", "Brno-venkov")]
    public void The_Okres_Around_A_City_Is_Not_The_City(string row, string typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    /// <summary>Not a substring match, and not a fuzzy one.</summary>
    [Theory]
    [InlineData("Praha", "Nová Praha")]
    [InlineData("Ústí nad Labem", "Ústí nad Orlicí")]
    [InlineData("Praha", "Kladno")]
    [InlineData("Brno", "Brno-střed")]
    public void A_Different_City_Is_Refused(string row, string typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    /// <summary>
    /// Exonyms are DATA. The algorithm does not know that Prague is Praha, and asserting so here stops
    /// a later reader assuming it does and deleting the <c>Prague</c> seed row as redundant.
    /// </summary>
    [Theory]
    [InlineData("Praha", "Prague 8")]
    [InlineData("Praha", "Prague")]
    [InlineData("Plzeň", "Pilsen")]
    [InlineData("Praha", "Прага")]
    public void An_Exonym_Matches_Nothing_Without_Its_Own_Row(string row, string typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    /// <summary>
    /// The strip runs on the CUSTOMER's string only. An operator who seeded one district meant one
    /// district, and must not end up serving the whole city — nor a sibling district.
    /// </summary>
    [Theory]
    [InlineData("Praha 8", "Praha 22")]
    [InlineData("Praha 8", "Praha")]
    public void A_Row_Naming_One_District_Does_Not_Claim_The_City(string row, string typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    [Theory]
    [InlineData("Praha", "8")]
    [InlineData("Praha", "")]
    [InlineData("", "Praha")]
    [InlineData("Praha", " ")]
    [InlineData(null, "Praha")]
    [InlineData("Praha", null)]
    public void Nothing_Matches_Nothing(string? row, string? typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    /// <summary>
    /// Monotonicity, asserted rather than assumed: every pair the old exact compare accepted is still
    /// accepted. This is what made the change safe to ship without touching a single seeded row.
    /// </summary>
    [Theory]
    [InlineData("Praha")]
    [InlineData("Brno")]
    [InlineData("Plzeň")]
    [InlineData("České Budějovice")]
    public void Everything_The_Exact_Compare_Accepted_Still_Matches(string name)
    {
        Assert.True(CityNameMatch.Matches(name, name));
        Assert.True(CityNameMatch.Matches(name, name.ToUpperInvariant()));
        Assert.True(CityNameMatch.Matches(name, $"  {name} "));
    }
}
