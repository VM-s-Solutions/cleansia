using System.Collections;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A redaction list is only as good as the thing that notices when the DTO grows past it. Every member
/// of both order DTOs is classified below — <b>blanked</b>, <b>kept</b>, or <b>reshaped</b> — and a
/// member in none of the three fails the build naming itself, so the next field added to
/// <see cref="OrderItem"/> cannot reach a browsing cleaner because nobody remembered this file.
///
/// <para>The lists are the specification; the behavioural assertions below prove
/// <see cref="OrderPiiRedaction"/> agrees with them.</para>
/// </summary>
public class OrderRedactionSurfaceTests
{
    private const string ApproximateAddress = "Praha · 120";

    // A multi-seat order one cleaner short of its crew. Every seat member is therefore non-default:
    // an unpopulated fixture yields 0 and false, which is precisely what a half-crewed 2-seat order
    // does NOT produce on any of the five.
    private const int RequiredEmployees = 2;
    private const int MaxEmployees = 2;
    private const int AssignedEmployeesCount = 1;
    private const int AvailableSpots = 1;
    private const bool HasAvailableSpots = true;

    private static readonly string[] DetailBlanked =
    [
        nameof(OrderItem.CustomerName),
        nameof(OrderItem.CustomerEmail),
        nameof(OrderItem.CustomerPhone),
        nameof(OrderItem.Address),
        nameof(OrderItem.ConfirmationCode),
        nameof(OrderItem.Notes),
        nameof(OrderItem.SpecialInstructions),
        nameof(OrderItem.AccessInstructions),
        nameof(OrderItem.CompletionNotes),
        nameof(OrderItem.RecurringTemplateId),
        nameof(OrderItem.ReceiptNumber),
        nameof(OrderItem.OrderNotes),
        nameof(OrderItem.OrderIssues),
        nameof(OrderItem.Review),
        nameof(OrderItem.ExpressWaiverForfeitedOnCancel),
        nameof(OrderItem.PreferredOffer),
    ];

    private static readonly string[] DetailReshaped =
    [
        nameof(OrderItem.AssignedEmployees),
    ];

    /// <summary>
    /// Everything a cleaner reads to decide whether to take the job, plus the identifiers they arrived
    /// with. None of it describes the customer: the discount amounts are already unblanked on the list
    /// row, the timestamps and counts are job state, <c>CustomerAddressApproximate</c> is city + zip
    /// prefix and survives on the list row for the same reason, the five seat members survive on the
    /// list row for that same reason again, and <c>EstimatedCleanerPay</c> is the browse branch's whole
    /// purpose.
    /// </summary>
    private static readonly string[] DetailKept =
    [
        nameof(OrderItem.Id),
        nameof(OrderItem.CustomerAddressApproximate),
        nameof(OrderItem.RequiredEmployees),
        nameof(OrderItem.MaxEmployees),
        nameof(OrderItem.AvailableSpots),
        nameof(OrderItem.AssignedEmployeesCount),
        nameof(OrderItem.HasAvailableSpots),
        nameof(OrderItem.DisplayOrderNumber),
        nameof(OrderItem.Rooms),
        nameof(OrderItem.Bathrooms),
        nameof(OrderItem.Extras),
        nameof(OrderItem.CleaningDateTime),
        nameof(OrderItem.PaymentType),
        nameof(OrderItem.PaymentStatus),
        nameof(OrderItem.TotalPrice),
        nameof(OrderItem.OriginalSubtotal),
        nameof(OrderItem.AppliedDiscountSource),
        nameof(OrderItem.TierDiscountAmount),
        nameof(OrderItem.MembershipDiscountAmount),
        nameof(OrderItem.PromoDiscountAmount),
        nameof(OrderItem.EstimatedTime),
        nameof(OrderItem.ActualCompletionTime),
        nameof(OrderItem.CompletedAt),
        nameof(OrderItem.OrderStatus),
        nameof(OrderItem.SelectedPackages),
        nameof(OrderItem.Currency),
        nameof(OrderItem.SelectedServices),
        nameof(OrderItem.StatusHistory),
        nameof(OrderItem.CreatedOn),
        nameof(OrderItem.UpdatedOn),
        nameof(OrderItem.EstimatedCleanerPay),
        nameof(OrderItem.IsAssignedToCurrentUser),
        nameof(OrderItem.HasAfterPhotos),
    ];

    private static readonly string[] ListBlanked =
    [
        nameof(OrderListItem.CustomerName),
        nameof(OrderListItem.CustomerEmail),
        nameof(OrderListItem.CustomerPhone),
        nameof(OrderListItem.CustomerAddress),
        nameof(OrderListItem.ConfirmationCode),
        nameof(OrderListItem.CustomerAddressLatitude),
        nameof(OrderListItem.CustomerAddressLongitude),
    ];

    private static readonly string[] ListKept =
    [
        nameof(OrderListItem.Id),
        nameof(OrderListItem.CustomerAddressApproximate),
        nameof(OrderListItem.DisplayOrderNumber),
        nameof(OrderListItem.Rooms),
        nameof(OrderListItem.Bathrooms),
        nameof(OrderListItem.Extras),
        nameof(OrderListItem.CleaningDateTime),
        nameof(OrderListItem.PaymentType),
        nameof(OrderListItem.PaymentStatus),
        nameof(OrderListItem.TotalPrice),
        nameof(OrderListItem.OriginalSubtotal),
        nameof(OrderListItem.AppliedDiscountSource),
        nameof(OrderListItem.TierDiscountAmount),
        nameof(OrderListItem.MembershipDiscountAmount),
        nameof(OrderListItem.PromoDiscountAmount),
        nameof(OrderListItem.EstimatedTime),
        nameof(OrderListItem.OrderStatus),
        nameof(OrderListItem.SelectedPackages),
        nameof(OrderListItem.CurrencyId),
        nameof(OrderListItem.Currency),
        nameof(OrderListItem.AssignedEmployees),
        nameof(OrderListItem.SelectedServices),
        nameof(OrderListItem.RequiredEmployees),
        nameof(OrderListItem.MaxEmployees),
        nameof(OrderListItem.AvailableSpots),
        nameof(OrderListItem.AssignedEmployeesCount),
        nameof(OrderListItem.HasAvailableSpots),
        nameof(OrderListItem.EstimatedCleanerPay),
    ];

    [Fact]
    public void Every_Detail_Member_Is_Classified()
    {
        AssertClassificationCovers(typeof(OrderItem), DetailBlanked, DetailReshaped, DetailKept);
    }

    [Fact]
    public void Every_List_Member_Is_Classified()
    {
        AssertClassificationCovers(typeof(OrderListItem), ListBlanked, [], ListKept);
    }

    [Theory]
    [MemberData(nameof(DetailBlankedMembers))]
    public void A_Detail_Member_Classified_Blanked_Is_Blank(string member)
    {
        var populated = FullyPopulatedDetail();

        // Anti-vacuity: the fixture must carry a value for the member before the redaction removes it.
        Assert.False(IsBlank(Read(populated, member)), $"{member} was never populated by the fixture");
        Assert.True(IsBlank(Read(populated.RedactForBrowsingCleaner(), member)));
    }

    [Theory]
    [MemberData(nameof(DetailKeptMembers))]
    public void A_Detail_Member_Classified_Kept_Survives(string member)
    {
        var populated = FullyPopulatedDetail();

        Assert.Equal(Read(populated, member), Read(populated.RedactForBrowsingCleaner(), member));
    }

    [Theory]
    [MemberData(nameof(ListBlankedMembers))]
    public void A_List_Member_Classified_Blanked_Is_Blank(string member)
    {
        var populated = FullyPopulatedListItem();

        Assert.False(IsBlank(Read(populated, member)), $"{member} was never populated by the fixture");
        Assert.True(IsBlank(Read(populated.RedactForBrowsingCleaner(), member)));
    }

    [Theory]
    [MemberData(nameof(ListKeptMembers))]
    public void A_List_Member_Classified_Kept_Survives(string member)
    {
        var populated = FullyPopulatedListItem();

        Assert.Equal(Read(populated, member), Read(populated.RedactForBrowsingCleaner(), member));
    }

    /// <summary>
    /// The kept theory compares the member against itself before and after, which a fixture that never
    /// populated it satisfies with <c>"" == ""</c>. The coarse location is the one kept member whose
    /// entire content is the point — a cleaner deciding on a job needs to know where it is — so both
    /// shapes are pinned against the literal instead, on one assertion, because the two routes
    /// disagreeing about it is precisely the bug this field closed.
    /// </summary>
    [Fact]
    public void The_Coarse_Location_Survives_On_Both_Shapes_With_Its_Value()
    {
        Assert.Equal(
            ApproximateAddress,
            FullyPopulatedDetail().RedactForBrowsingCleaner().CustomerAddressApproximate);
        Assert.Equal(
            ApproximateAddress,
            FullyPopulatedListItem().RedactForBrowsingCleaner().CustomerAddressApproximate);
    }

    /// <summary>
    /// The same anti-vacuity problem as the coarse location, and worse: four of the five seat members
    /// are numeric and one is a bool, so the kept theory's before/after comparison is satisfied by
    /// <c>0 == 0</c> and <c>false == false</c> — exactly what a fixture that never populated them
    /// yields. Both shapes are pinned against the literals of a half-crewed two-seat order instead,
    /// where not one of the five equals its type default.
    /// </summary>
    [Fact]
    public void The_Seat_Counts_Survive_On_Both_Shapes_With_Their_Values()
    {
        var detail = FullyPopulatedDetail().RedactForBrowsingCleaner();
        Assert.Equal(2, detail.RequiredEmployees);
        Assert.Equal(2, detail.MaxEmployees);
        Assert.Equal(1, detail.AvailableSpots);
        Assert.Equal(1, detail.AssignedEmployeesCount);
        Assert.True(detail.HasAvailableSpots);

        var listItem = FullyPopulatedListItem().RedactForBrowsingCleaner();
        Assert.Equal(2, listItem.RequiredEmployees);
        Assert.Equal(2, listItem.MaxEmployees);
        Assert.Equal(1, listItem.AvailableSpots);
        Assert.Equal(1, listItem.AssignedEmployeesCount);
        Assert.True(listItem.HasAvailableSpots);
    }

    [Fact]
    public void The_Reshaped_Crew_Keeps_Its_Ids_And_Loses_Its_Contact_Details()
    {
        var redacted = FullyPopulatedDetail().RedactForBrowsingCleaner();

        var crewMember = Assert.Single(redacted.AssignedEmployees);
        Assert.Equal("assignment-1", crewMember.Id);
        Assert.Equal("employee-1", crewMember.EmployeeId);
        Assert.Equal("Petra", crewMember.FullName);
        Assert.Null(crewMember.PhoneNumber);
    }

    public static TheoryData<string> DetailBlankedMembers => ToTheoryData(DetailBlanked);

    public static TheoryData<string> DetailKeptMembers => ToTheoryData(DetailKept);

    public static TheoryData<string> ListBlankedMembers => ToTheoryData(ListBlanked);

    public static TheoryData<string> ListKeptMembers => ToTheoryData(ListKept);

    private static TheoryData<string> ToTheoryData(string[] members)
    {
        var data = new TheoryData<string>();
        foreach (var member in members)
        {
            data.Add(member);
        }

        return data;
    }

    private static void AssertClassificationCovers(
        Type dto, string[] blanked, string[] reshaped, string[] kept)
    {
        var declared = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToArray();

        var classified = blanked.Concat(reshaped).Concat(kept).ToArray();

        Assert.Empty(classified.Except(declared));
        Assert.Empty(declared.Except(classified));
        Assert.Equal(declared.Length, classified.Distinct().Count());
    }

    private static object? Read(object dto, string member) =>
        dto.GetType().GetProperty(member)!.GetValue(dto);

    private static bool IsBlank(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        IEnumerable sequence => !sequence.GetEnumerator().MoveNext(),
        _ => false,
    };

    private static OrderItem FullyPopulatedDetail() =>
        new(
            Id: "order-1",
            DisplayOrderNumber: "ORD-ABCD1234",
            CustomerName: "Jana Novakova",
            CustomerEmail: "jana@example.test",
            CustomerPhone: "+420777123456",
            Address: new OrderAddress("Vinohradska 12", "Praha", "12000", "Czechia", 50.0755, 14.4378),
            CustomerAddressApproximate: ApproximateAddress,
            Rooms: 3,
            Bathrooms: 2,
            Extras: new Dictionary<string, bool> { ["insideOven"] = true },
            CleaningDateTime: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            PaymentType: new Code("PaymentType", "Card", 2),
            PaymentStatus: new Code("PaymentStatus", "Paid", 2),
            TotalPrice: 1500m,
            OriginalSubtotal: 1700m,
            AppliedDiscountSource: AppliedDiscountSource.Tier,
            TierDiscountAmount: 200m,
            MembershipDiscountAmount: 50m,
            PromoDiscountAmount: 30m,
            EstimatedTime: 180,
            ActualCompletionTime: 175,
            CompletedAt: new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc),
            CompletionNotes: "Balcony door was jammed.",
            OrderStatus: new Code("OrderStatus", "Completed", 5),
            ConfirmationCode: "CONF-1234",
            Notes: "Cat is friendly.",
            SpecialInstructions: "Use the eco products under the sink.",
            AccessInstructions: "Code 1234 at the gate.",
            RecurringTemplateId: "tmpl-weekly",
            SelectedPackages: [],
            Currency: new CurrencyDetailDto("czk", "CZK", "Czech Koruna", "Kč", 1m, true),
            SelectedServices: [],
            StatusHistory: [new OrderStatusTrackDto(new Code("OrderStatus", "Completed", 5), DateTimeOffset.UtcNow)],
            CreatedOn: DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedOn: DateTimeOffset.UtcNow.AddHours(-1),
            AssignedEmployees: [new AssignedEmployeeDto("assignment-1", "employee-1", "Petra Svobodova", "+420602987654")],
            RequiredEmployees: RequiredEmployees,
            MaxEmployees: MaxEmployees,
            AvailableSpots: AvailableSpots,
            AssignedEmployeesCount: AssignedEmployeesCount,
            HasAvailableSpots: HasAvailableSpots,
            ReceiptNumber: "CZ-2026-000123",
            OrderNotes: [new OrderNoteDto("note-1", "employee-1", "Second bathroom needed a re-do.", DateTimeOffset.UtcNow)],
            OrderIssues: [new OrderIssueDto("issue-1", "employee-1", "Broken tile.", false, null, DateTimeOffset.UtcNow)],
            Review: new OrderReviewDto("review-1", "order-1", 5, "Spotless.", DateTimeOffset.UtcNow, null),
            EstimatedCleanerPay: 620m,
            IsAssignedToCurrentUser: false,
            HasAfterPhotos: true,
            ExpressWaiverForfeitedOnCancel: true,
            PreferredOffer: new PreferredOfferDetails(
                PreferredOfferState.AwaitingConfirmation, "Petra", DateTime.UtcNow.AddHours(2), true));

    private static OrderListItem FullyPopulatedListItem() =>
        new(
            Id: "order-1",
            CustomerName: "Jana Novakova",
            CustomerEmail: "jana@example.test",
            CustomerPhone: "+420777123456",
            CustomerAddress: "Vinohradska 12, Praha, 12000",
            CustomerAddressApproximate: ApproximateAddress,
            DisplayOrderNumber: "ORD-ABCD1234",
            Rooms: 3,
            Bathrooms: 2,
            Extras: new Dictionary<string, bool> { ["insideOven"] = true },
            CleaningDateTime: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            PaymentType: new Code("PaymentType", "Card", 2),
            PaymentStatus: new Code("PaymentStatus", "Paid", 2),
            TotalPrice: 1500m,
            OriginalSubtotal: 1700m,
            AppliedDiscountSource: AppliedDiscountSource.Tier,
            TierDiscountAmount: 200m,
            MembershipDiscountAmount: 50m,
            PromoDiscountAmount: 30m,
            EstimatedTime: 180,
            OrderStatus: new Code("OrderStatus", "Confirmed", 2),
            ConfirmationCode: "CONF-1234",
            SelectedPackages: [],
            CurrencyId: "czk",
            Currency: new CurrencyListItem("czk", "CZK", "Kč", "Czech Koruna", 1m, true),
            AssignedEmployees: ["assignment-1"],
            SelectedServices: [],
            RequiredEmployees: RequiredEmployees,
            MaxEmployees: MaxEmployees,
            AvailableSpots: AvailableSpots,
            AssignedEmployeesCount: AssignedEmployeesCount,
            HasAvailableSpots: HasAvailableSpots,
            EstimatedCleanerPay: 620m,
            CustomerAddressLatitude: 50.0755,
            CustomerAddressLongitude: 14.4378);
}
