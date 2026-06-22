using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Referrals;

/// <summary>
/// Characterization of the customer "people I invited" paged list across the §A
/// canonicalization (record Query + bespoke GetByReferrerAsync/CountByReferrerAsync ->
/// Request : DataRangeRequest + ReferralSpecification(ReferrerUserId) + GetPagedSort +
/// MapToDto). Pins the empty-user short-circuit, the row projection (invitee first name),
/// the page metadata, and that the session user reaches the spec. Default order
/// (AcceptedOn desc) preserved.
/// </summary>
public class GetMyReferralsHandlerTests
{
    private const string UserId = "referrer-1";

    private readonly Mock<IReferralRepository> _repository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private Task<PagedData<GetMyReferrals.ReferralListItem>> Handle(GetMyReferrals.Request request)
    {
        var handlerType = typeof(GetMyReferrals).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object, _session.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<GetMyReferrals.ReferralListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static Referral QualifiedReferral(User? referred)
    {
        var referral = Referral.CreateAccepted(UserId, "referred-1", "code-1", "system");
        referral.MarkQualified("order-1", 150, 120, "system");
        referral.Id = "ref-1";
        var prop = typeof(Referral).GetProperty(nameof(Referral.Referred))!;
        prop.SetValue(referral, referred);
        return referral;
    }

    [Fact]
    public async Task Empty_Session_User_Short_Circuits_To_Empty_Page()
    {
        _session.Setup(s => s.GetUserId()).Returns(string.Empty);

        var result = await Handle(new GetMyReferrals.Request { Offset = 0, Limit = 20 });

        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Empty(result.Data);

        _repository.Verify(
            r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Projects_Row_With_Invitee_Name_And_PageMetadata()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var referred = User.CreateWithPassword("referred@x.test", "Passw0rd!", "Iva", "Erred");
        var referral = QualifiedReferral(referred);

        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(31);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                20, 10, It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { referral }.AsQueryable().BuildMock());

        var result = await Handle(new GetMyReferrals.Request { Offset = 20, Limit = 10 });

        Assert.Equal(31, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(10, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("ref-1", row.Id);
        Assert.Equal("Iva", row.ReferredFirstName);
        Assert.Equal(ReferralStatus.Qualified, row.Status);
        Assert.NotNull(row.FirstQualifyingOrderOn);
        Assert.Equal(150, row.PointsAwardedToReferrer);
    }

    [Fact]
    public async Task Null_Invitee_Maps_To_Empty_FirstName()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var referral = QualifiedReferral(referred: null);
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { referral }.AsQueryable().BuildMock());

        var result = await Handle(new GetMyReferrals.Request());

        var row = Assert.Single(result.Data);
        Assert.Equal(string.Empty, row.ReferredFirstName);
    }

    [Fact]
    public async Task Session_User_Reaches_Specification()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        Expression<Func<Referral, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Referral, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<Referral>().AsQueryable().BuildMock());

        await Handle(new GetMyReferrals.Request());

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var mine = Referral.CreateAccepted(UserId, "x", "c", "system");
        var other = Referral.CreateAccepted("someone-else", "y", "c", "system");
        Assert.True(predicate(mine));
        Assert.False(predicate(other));
    }
}
