using System.Collections;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <see cref="PendingOfferItem"/> is the third pre-acceptance cleaner-facing DTO, and the only one
/// with no redaction step: it is served ONLY to the beneficiary of a live reservation, who has not
/// accepted, so the whole record is the projection. That makes the member list itself the rule, and
/// an unclassified member the defect — the same shape as
/// <see cref="OrderRedactionSurfaceTests"/>, whose two DTOs got the real guard while this one shipped
/// with an assertion that a coarse string does not contain a street, which is true by construction
/// and blind to every member added after it.
///
/// <para>Each member is classified as the coarse LOCATION, a fact about the JOB, or the RESERVATION
/// itself. A member in none of the three fails the build naming itself, so a <c>CustomerName</c>
/// cannot reach a cleaner who has not taken the job because nobody remembered this file.</para>
/// </summary>
public class PendingOfferItemSurfaceTests
{
    private const string City = "Brno";
    private const string ZipCode = "60200";
    private const string Street = "Held St 7";
    private const string ApproximateAddress = "Brno · 602 xx";

    /// <summary>
    /// The ONE location member, and the ceiling on it: city + zip prefix, from the shared builder every
    /// other pre-acceptance surface uses. A second location member is not a wider field, it is a second
    /// decision about the same disclosure.
    /// </summary>
    private static readonly string[] CoarseLocation =
    [
        nameof(PendingOfferItem.CustomerAddressApproximate),
    ];

    /// <summary>
    /// The shape of the work — what a cleaner needs to answer yes or no. None of it describes the
    /// household: room and bathroom counts size the job, the price and currency are the cleaner's own
    /// commercial decision, and the timings are the calendar question.
    /// </summary>
    private static readonly string[] JobFacts =
    [
        nameof(PendingOfferItem.Id),
        nameof(PendingOfferItem.DisplayOrderNumber),
        nameof(PendingOfferItem.CleaningDateTime),
        nameof(PendingOfferItem.EstimatedTime),
        nameof(PendingOfferItem.Rooms),
        nameof(PendingOfferItem.Bathrooms),
        nameof(PendingOfferItem.TotalPrice),
        nameof(PendingOfferItem.CurrencyCode),
    ];

    /// <summary>The reservation itself — the only thing this surface adds over the ordinary board.</summary>
    private static readonly string[] ReservationFacts =
    [
        nameof(PendingOfferItem.RespondByUtc),
    ];

    [Fact]
    public void Every_Member_Is_Classified()
    {
        var declared = typeof(PendingOfferItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToArray();

        var classified = CoarseLocation.Concat(JobFacts).Concat(ReservationFacts).ToArray();

        Assert.Empty(classified.Except(declared));
        Assert.Empty(declared.Except(classified));
        Assert.Equal(declared.Length, classified.Distinct().Count());
    }

    /// <summary>
    /// The classification above says which members exist; this says the location member carries the
    /// ceiling rather than merely a string. Driven through the production mapper — a hand-built DTO
    /// would pin the assertion and not the feature.
    /// </summary>
    [Fact]
    public void The_Location_Member_Is_The_Shared_Coarse_Form()
    {
        Assert.Equal(ApproximateAddress, Map().CustomerAddressApproximate);
        Assert.Equal(
            OrderMappers.BuildApproximateAddress(City, ZipCode),
            Map().CustomerAddressApproximate);
    }

    /// <summary>
    /// Anti-vacuity for the member-by-member reading: every classified member is non-default on a row
    /// production could produce, so "the DTO carries what its classification says" is measured rather
    /// than satisfied by an unpopulated fixture.
    /// </summary>
    [Fact]
    public void Every_Classified_Member_Is_Populated_By_The_Mapper()
    {
        var offer = Map();

        foreach (var member in CoarseLocation.Concat(JobFacts).Concat(ReservationFacts))
        {
            var value = typeof(PendingOfferItem).GetProperty(member)!.GetValue(offer);
            Assert.False(IsDefault(value), $"{member} was never populated by the mapper");
        }
    }

    /// <summary>
    /// The row the mapper is handed carries no street at all — the handler's projection selects city
    /// and zip and nothing else — so this asserts the property one level up: whatever the DTO grows,
    /// no member of it may echo the street. The real end-to-end proof over Postgres is
    /// <c>PendingOffersSurfaceTests</c>; this is the one that survives a new member.
    /// </summary>
    [Fact]
    public void No_Member_Echoes_The_Street()
    {
        var offer = Map();

        foreach (var property in typeof(PendingOfferItem).GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(
                Street,
                Convert.ToString(property.GetValue(offer), System.Globalization.CultureInfo.InvariantCulture)
                    ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static PendingOfferItem Map() => new PendingOfferRow(
        Id: "order-offer-1",
        DisplayOrderNumber: "ORD-ABCD1234",
        CleaningDateTime: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
        EstimatedTime: 180,
        RespondByUtc: new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc),
        City: City,
        ZipCode: ZipCode,
        Rooms: 3,
        Bathrooms: 2,
        TotalPrice: 1500m,
        CurrencyCode: "CZK").MapToDto();

    private static bool IsDefault(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        IEnumerable sequence => !sequence.GetEnumerator().MoveNext(),
        int number => number == 0,
        decimal amount => amount == 0m,
        DateTime instant => instant == default,
        _ => false,
    };
}
