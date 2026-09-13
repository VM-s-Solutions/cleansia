using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// ADR-0062 D3 tier 2 at the producer: the four schedule acts emit ONE
/// <see cref="RecurringTemplateEvidence"/> of before/after facts — frequency, weekday, time, the
/// catalogue ids, the saved-address id and the active flag — and never the address text or the
/// preferred cleaner. A create has no before; a delete has no after, and its before is the last place
/// the hard-deleted schedule survives.
/// </summary>
public sealed class RecurringBookingAuditEvidenceTests
{
    private const string UserId = "user-plus-1";
    private const string SavedAddressId = "saved-address-1";
    private const string TemplateId = "tmpl-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly AuditContext _auditContext = new();

    public RecurringBookingAuditEvidenceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ArrangeSavedAddress()]);
        var plan = MembershipPlan.Create("PLUS", "Cleansia Plus", 10m, freeCancellationWindowHours: 4, allowsExpressUpgrade: true);
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(UserId, plan.Id, "currency-czk", "sub_1", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1)));
    }

    private static SavedAddress ArrangeSavedAddress()
    {
        var address = Address.Create("Dlouhá 12", "Praha", "11000", "country-cz");
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!.GetSetMethod(nonPublic: true)!.Invoke(saved, [address]);
        return saved;
    }

    private RecurringBookingTemplate ArrangeExistingTemplate()
    {
        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(9, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: ["service-1"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(3),
            endsOn: null,
            preferredEmployeeId: "emp-favourite");
        template.Id = TemplateId;
        _templateRepository.Setup(r => r.GetByIdAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        return template;
    }

    private static JsonElement Payload(AuditSnapshot? snapshot) => JsonDocument.Parse(snapshot!.AfterJson!).RootElement;

    private static void AssertTuesdayNineFacts(JsonElement facts, bool isActive)
    {
        Assert.Equal("weekly", facts.GetProperty("frequency").GetString());
        Assert.Equal("tuesday", facts.GetProperty("weekday").GetString());
        Assert.Equal("09:00:00", facts.GetProperty("timeOfDay").GetString());
        Assert.Empty(facts.GetProperty("packageIds").EnumerateArray());
        Assert.Equal(["service-1"], facts.GetProperty("serviceIds").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Equal(SavedAddressId, facts.GetProperty("savedAddressId").GetString());
        Assert.Equal(isActive, facts.GetProperty("isActive").GetBoolean());
        Assert.Equal(7, facts.EnumerateObject().Count());
    }

    /// <summary>
    /// The three acts on an existing schedule name it <c>TemplateId</c> on the wire, so their markers
    /// name that property for the failure row's id; a create has no template yet and names none.
    /// </summary>
    [Theory]
    [InlineData(typeof(CreateRecurringBooking.Command), "customer.recurring.create", null)]
    [InlineData(typeof(UpdateRecurringBooking.Command), "customer.recurring.update", "TemplateId")]
    [InlineData(typeof(SetRecurringBookingActive.Command), "customer.recurring.set_active", "TemplateId")]
    [InlineData(typeof(DeleteRecurringBooking.Command), "customer.recurring.delete", "TemplateId")]
    public void The_Schedule_Markers_Are_Frozen_On_The_Template(Type commandType, string expectedLabel, string? expectedIdProperty)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("RecurringBookingTemplate", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
        Assert.Equal(expectedIdProperty, descriptor.ResourceIdProperty);
        if (expectedIdProperty is not null)
        {
            Assert.NotNull(commandType.GetProperty(expectedIdProperty));
        }
    }

    [Fact]
    public async Task A_Create_Records_The_New_Schedule_And_No_Before()
    {
        RecurringBookingTemplate? added = null;
        _templateRepository.Setup(r => r.Add(It.IsAny<RecurringBookingTemplate>())).Callback<RecurringBookingTemplate>(t => added = t);

        var result = await new CreateRecurringBooking.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _membershipRepository.Object, _session.Object, _auditContext)
            .Handle(new CreateRecurringBooking.Command(
                Frequency: (int)RecurrenceFrequency.Weekly, DayOfWeek: (int)System.DayOfWeek.Tuesday, TimeOfDay: "09:00",
                Rooms: 2, Bathrooms: 1, SavedAddressId: SavedAddressId, SelectedServiceIds: ["service-1"], SelectedPackageIds: [],
                PaymentType: (int)PaymentType.Card, StartsOn: DateTime.UtcNow.AddDays(3), PreferredEmployeeId: "emp-favourite"),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal("RecurringBookingTemplate", snapshot!.ResourceType);
        Assert.Equal(added!.Id, snapshot.ResourceId);
        var payload = Payload(snapshot);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("before").ValueKind);
        AssertTuesdayNineFacts(payload.GetProperty("after"), isActive: true);
        Assert.DoesNotContain("emp-favourite", snapshot.AfterJson!);
        Assert.DoesNotContain("Dlouhá", snapshot.AfterJson!);
    }

    [Fact]
    public async Task An_Update_Records_The_Schedule_Before_And_After()
    {
        ArrangeExistingTemplate();

        var result = await new UpdateRecurringBooking.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _session.Object, _auditContext)
            .Handle(new UpdateRecurringBooking.Command(
                TemplateId, Frequency: (int)RecurrenceFrequency.Biweekly, DayOfWeek: (int)System.DayOfWeek.Friday, TimeOfDay: "14:30",
                Rooms: 3, Bathrooms: 2, SavedAddressId: SavedAddressId, SelectedServiceIds: ["service-1", "service-2"],
                SelectedPackageIds: ["package-1"], PaymentType: (int)PaymentType.Cash, StartsOn: DateTime.UtcNow.AddDays(5)),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(TemplateId, snapshot!.ResourceId);
        var payload = Payload(snapshot);
        AssertTuesdayNineFacts(payload.GetProperty("before"), isActive: true);
        var after = payload.GetProperty("after");
        Assert.Equal("biweekly", after.GetProperty("frequency").GetString());
        Assert.Equal("friday", after.GetProperty("weekday").GetString());
        Assert.Equal("14:30:00", after.GetProperty("timeOfDay").GetString());
        Assert.Equal(["package-1"], after.GetProperty("packageIds").EnumerateArray().Select(e => e.GetString()).ToList());
        Assert.Equal(["service-1", "service-2"], after.GetProperty("serviceIds").EnumerateArray().Select(e => e.GetString()).ToList());
    }

    [Fact]
    public async Task A_Pause_Records_The_Active_Flag_Flipping()
    {
        ArrangeExistingTemplate();

        var result = await new SetRecurringBookingActive.Handler(_templateRepository.Object, _auditContext)
            .Handle(new SetRecurringBookingActive.Command(TemplateId, IsActive: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Payload(_auditContext.DrainSnapshot());
        Assert.True(payload.GetProperty("before").GetProperty("isActive").GetBoolean());
        Assert.False(payload.GetProperty("after").GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task A_Delete_Records_The_Last_Schedule_As_Before_And_No_After()
    {
        ArrangeExistingTemplate();

        var result = await new DeleteRecurringBooking.Handler(_templateRepository.Object, _auditContext)
            .Handle(new DeleteRecurringBooking.Command(TemplateId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(TemplateId, snapshot!.ResourceId);
        var payload = Payload(snapshot);
        AssertTuesdayNineFacts(payload.GetProperty("before"), isActive: true);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("after").ValueKind);
        _templateRepository.Verify(r => r.Remove(It.IsAny<RecurringBookingTemplate>()), Times.Once);
    }

    [Fact]
    public async Task A_Create_Refused_For_Want_Of_A_Membership_Records_No_Evidence()
    {
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var result = await new CreateRecurringBooking.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _membershipRepository.Object, _session.Object, _auditContext)
            .Handle(new CreateRecurringBooking.Command(
                Frequency: (int)RecurrenceFrequency.Weekly, DayOfWeek: (int)System.DayOfWeek.Tuesday, TimeOfDay: "09:00",
                Rooms: 2, Bathrooms: 1, SavedAddressId: SavedAddressId, SelectedServiceIds: ["service-1"], SelectedPackageIds: [],
                PaymentType: (int)PaymentType.Card, StartsOn: DateTime.UtcNow.AddDays(3)),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RecurringTemplateMembershipRequired, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}
