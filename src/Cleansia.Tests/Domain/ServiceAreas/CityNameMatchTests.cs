using Cleansia.Core.Domain.ServiceAreas;

namespace Cleansia.Tests.Domain.ServiceAreas;

/// <summary>
/// The gate that decides whether a customer may book at all.
///
/// <para>It was an exact case-insensitive compare, which refuses every spelling an operator did not
/// seed verbatim — and this repo's own seed contains such a pair: a customer address at <c>'Plzen'</c>
/// (<c>sql-scripts/seed/insert_addresses.sql:39</c>) against a serviced row <c>'Plzeň'</c>
/// (<c>prod-bootstrap.sql</c>). That address could not be booked, and the customer found out at
/// payment.</para>
///
/// <para><b>The refusals below are the half that matters.</b> A rule that only widened would be
/// trivially satisfied by returning <c>true</c>; these pin what must still be turned away, and the
/// okres cases are the ones that make the city-part strip a decision rather than a convenience.</para>
/// </summary>
public class CityNameMatchTests
{
    /// <summary>
    /// Owner ruling 2026-10-25 (decision 9): Praha first, by its postcode. Every Prague row carries ZIP
    /// prefix 1, and a row with a prefix serves an address whose postcode, all whitespace removed, is
    /// five digits starting with it — and refuses one whose valid postcode does not, whatever its city
    /// says; only an address with no valid postcode falls back to the name. A row without a prefix serves
    /// by name. This is the table the server, iOS, Android and the customer web all pin.
    /// </summary>
    [Theory]
    [InlineData("Praha", "110 00", true)]
    [InlineData("Прага", "110 00", true)]
    [InlineData("Prague", "15000", true)]
    [InlineData("Praha 4 - Chodov", "149 00", true)]
    [InlineData("PRAHA", "110 00", true)]
    [InlineData("Praha 8", "180\u00A000", true)]
    [InlineData("", "120 00", true)]
    [InlineData("Praha", "252 42", false)]
    [InlineData("Praha", "602 00", false)]
    [InlineData("Praha", null, true)]
    [InlineData("Praha", "1100", true)]
    [InlineData("Praha-západ", "252 00", false)]
    [InlineData("Praha-západ", null, false)]
    [InlineData("Nová Praha", null, false)]
    [InlineData("Jesenice", "252 42", false)]
    [InlineData("Brno-střed", "602 00", true)]
    [InlineData("Brno - Královo Pole", "612 00", true)]
    [InlineData("Brno-venkov", "664 00", false)]
    [InlineData("Ostrava-Poruba", "708 00", true)]
    [InlineData("Pardubice II", "530 02", true)]
    [InlineData("Liberec XIV-Ruprechtice", "460 14", true)]
    [InlineData("Plzeň-jih", null, false)]
    [InlineData("Plzen", "301 00", true)]
    [InlineData("Pilsen", "301 00", false)]
    [InlineData("Ústí nad Labem-Střekov", "400 03", true)]
    [InlineData("Kladno", "272 01", false)]
    [InlineData("Brno", "110 00", true)]
    [InlineData("Praha 8 - Libeň", "180 00", true)]
    public void The_Shared_Case_Table(string city, string? postcode, bool serviced)
    {
        Assert.Equal(serviced, Serviced(ServicedRows, city, postcode));
    }

    /// <summary>
    /// Only five ASCII digits, once every whitespace character is removed, are a postcode. A no-break space
    /// inside five digits still leaves one, and it decides against the name; four or six digits, a letter,
    /// a country code, a dash or a blank leave none, and the name decides. The shared table cannot show
    /// which answered: its no-break-space and four-digit cases get the same answer from the name either way.
    /// </summary>
    [Theory]
    [InlineData("Прага", "110\u00A000", true)]
    [InlineData("Praha", "602\u00A000", false)]
    [InlineData("Прага", "1100", false)]
    [InlineData("Praha", "6020", true)]
    [InlineData("Прага", "1abcd", false)]
    [InlineData("Praha", "252\u00A042", false)]
    [InlineData("Praha", "602000", true)]
    [InlineData("Praha", "602 000", true)]
    [InlineData("Praha", "110 000", true)]
    [InlineData("Прага", "110 000", false)]
    [InlineData("Praha", "CZ-110 00", true)]
    [InlineData("Прага", "CZ-110 00", false)]
    [InlineData("Praha", "110-00", true)]
    [InlineData("Прага", "110-00", false)]
    [InlineData("Praha", "", true)]
    [InlineData("Praha", "   ", true)]
    [InlineData("Прага", "", false)]
    [InlineData("Прага", "   ", false)]
    [InlineData("Praha-západ", "", false)]
    [InlineData("Nová Praha", "", false)]
    [InlineData("Plzeň-jih", "", false)]
    public void Only_Five_Ascii_Digits_Are_A_Postcode_And_Without_One_The_Name_Decides(string city, string postcode, bool serviced)
    {
        Assert.Equal(serviced, Serviced(ServicedRows, city, postcode));
    }

    [Fact]
    public void With_No_Rows_No_Address_Is_Serviced()
    {
        Assert.False(Serviced([], "Praha", "110 00"));
    }

    /// <summary>
    /// A row's ZIP prefix is trimmed, never compacted: <c>" 1 "</c> serves <c>110 00</c> whatever the city,
    /// while <c>"1 1"</c> starts no valid postcode, and a valid postcode leaves no fallback to the name. A
    /// blank prefix is no prefix, and a row without one answers by its name even beside a valid postcode.
    /// </summary>
    [Theory]
    [InlineData("Praha", " 1 ", "Kladno", "110 00", true)]
    [InlineData("Praha", "1 1", "Praha", "110 00", false)]
    [InlineData("Praha", " ", "Praha", "252 42", true)]
    [InlineData("Praha", "", "Прага", "110 00", false)]
    [InlineData("Plzeň", null, "Plzen", "301 00", true)]
    public void A_Zip_Prefix_Is_Trimmed_Never_Compacted_And_A_Blank_One_Is_None(
        string row, string? zipPrefix, string city, string postcode, bool serviced)
    {
        Assert.Equal(serviced, Serviced([(row, zipPrefix)], city, postcode));
    }

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

    /// <summary>A district belongs to its city, whether it is numbered, written in Roman numerals or named after a dash.</summary>
    [Theory]
    [InlineData("Praha", "Praha 8")]
    [InlineData("Praha", "Praha 22")]
    [InlineData("Prague", "Prague 8")]
    [InlineData("Praha", "Praha 4 - Chodov")]
    [InlineData("Praha", "Praha 5 \u2013 Smíchov")]
    [InlineData("Praha", "Praha 4-Chodov")]
    [InlineData("Praha", "Praha 4\u2011Chodov")]
    [InlineData("Praha", "Praha\u00A08")]
    [InlineData("Praha", "Praha-Kunratice")]
    [InlineData("Praha", "Praha - Smíchov")]
    [InlineData("Brno", "Brno-střed")]
    [InlineData("Brno", "Brno-sever")]
    [InlineData("Brno", "Brno - Královo Pole")]
    [InlineData("Ostrava", "Ostrava-Poruba")]
    [InlineData("Ostrava", "Ostrava-Jih")]
    [InlineData("Ostrava", "Ostrava 3 - Ostrava-Jih")]
    [InlineData("Ústí nad Labem", "Ústí nad Labem-Střekov")]
    [InlineData("Pardubice", "Pardubice II")]
    [InlineData("Liberec", "Liberec XIV-Ruprechtice")]
    [InlineData("Plzeň", "Plzeň 2-Slovany")]
    public void A_District_Is_Served_By_Its_City(string row, string typed)
    {
        Assert.True(CityNameMatch.Matches(row, typed));
    }

    [Fact]
    public void A_Row_Naming_One_District_Serves_Its_Own_Quarter()
    {
        Assert.True(CityNameMatch.Matches("Praha 8", "Praha 8 - Libeň"));
    }

    /// <summary>A dash with nothing after it splits off no city part, so it stays part of the name.</summary>
    [Theory]
    [InlineData("Praha", "Praha-")]
    [InlineData("Brno", "Brno -")]
    public void A_Dash_With_Nothing_After_It_Is_Not_A_City_Part(string row, string typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    [Theory]
    [InlineData("Praha", "  PRAHA  ")]
    [InlineData("Hradec Králové", "Hradec  Kralove")]
    [InlineData("Ústí nad Labem", "Ústí\u00A0nad\u00A0Labem")]
    public void Case_And_Spacing_Are_Not_A_Difference(string row, string typed)
    {
        Assert.True(CityNameMatch.Matches(row, typed));
    }

    /// <summary>
    /// <b>An okres is not a district.</b> <c>Praha-západ</c>, <c>Praha-východ</c>, <c>Brno-venkov</c>,
    /// <c>Plzeň-sever</c> and <c>Plzeň-jih</c> are the rural rings AROUND those cities and share the exact
    /// syntax of a city part. A strip that admitted them would serve the countryside on the strength of a
    /// row for the city, so these five are named and refused.
    /// </summary>
    [Theory]
    [InlineData("Praha", "Praha-západ")]
    [InlineData("Praha", "Praha-východ")]
    [InlineData("Brno", "Brno-venkov")]
    [InlineData("Brno", "Brno - venkov")]
    [InlineData("Plzeň", "Plzeň-sever")]
    [InlineData("Plzeň", "Plzeň-jih")]
    public void The_Okres_Around_A_City_Is_Not_The_City(string row, string typed)
    {
        Assert.False(CityNameMatch.Matches(row, typed));
    }

    /// <summary>Not a substring match, and not a fuzzy one.</summary>
    [Theory]
    [InlineData("Praha", "Nová Praha")]
    [InlineData("Ústí nad Labem", "Ústí nad Orlicí")]
    [InlineData("Praha", "Kladno")]
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
    /// By name, the strip runs on the CUSTOMER's string only: a row naming one district matches neither the
    /// whole city nor a sibling district, though that district's own quarters are still it. A row with a
    /// ZIP prefix answers by the postcode before its name (the shared case table).
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

    private static bool Serviced((string Name, string? ZipPrefix)[] rows, string city, string? postcode) =>
        rows.Any(row => CityNameMatch.Serves(row.Name, row.ZipPrefix, city, postcode));

    private static readonly (string Name, string? ZipPrefix)[] ServicedRows =
    [
        ("Praha", "1"),
        ("Prague", "1"),
        .. Enumerable.Range(1, 22).Select(n => ($"Praha {n}", (string?)"1")),
        .. Enumerable.Range(1, 22).Select(n => ($"Prague {n}", (string?)"1")),
        ("Brno", null),
        ("Ostrava", null),
        ("Plzeň", null),
        ("Liberec", null),
        ("Olomouc", null),
        ("České Budějovice", null),
        ("Hradec Králové", null),
        ("Ústí nad Labem", null),
        ("Pardubice", null),
    ];
}
