using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;
using Cleansia.Core.AppServices.Features.Referrals.Admin.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// Characterization of the admin all-referrals paged list across the §A
/// canonicalization (record Query + bespoke repo -> Request : DataRangeRequest +
/// Specification + GetPagedSort + MapToDto). Pins the row projection (incl. both
/// party emails from the joined users), the page metadata, and that the status
/// filter reaches the spec. Default order (AcceptedOn desc) preserved.
/// </summary>
public class GetPagedReferralsHandlerTests
{
    private readonly Mock<IReferralRepository> _repository = new();

    private Task<PagedData<AdminReferralListItem>> Handle(GetPagedReferrals.Request request)
    {
        var handlerType = typeof(GetPagedReferrals).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<AdminReferralListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static Referral QualifiedReferral(User? referrer, User? referred)
    {
        var referral = Referral.CreateAccepted("referrer-1", "referred-1", "code-1", "system");
        referral.MarkQualified("order-1", "czk", 150m, "eur", 6m, "system");
        referral.Id = "ref-1";
        SetNav(referral, nameof(Referral.Referrer), referrer);
        SetNav(referral, nameof(Referral.Referred), referred);
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = "eur";
        SetNav(referral, nameof(Referral.ReferrerCreditCurrency), czk);
        SetNav(referral, nameof(Referral.ReferredCreditCurrency), eur);
        return referral;
    }

    private static void SetNav(object entity, string property, object? value)
    {
        var prop = entity.GetType().GetProperty(property)!;
        prop.SetValue(entity, value);
    }

    [Fact]
    public async Task Projects_Row_With_Both_Emails_And_PageMetadata()
    {
        var referrer = User.CreateWithPassword("referrer@x.test", "Passw0rd!", "Ref", "Errer");
        var referred = User.CreateWithPassword("referred@x.test", "Passw0rd!", "Ref", "Erred");
        var referral = QualifiedReferral(referrer, referred);

        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(31);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                20, 10, It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { referral }.AsQueryable().BuildMock());

        var request = new GetPagedReferrals.Request { Offset = 20, Limit = 10 };

        var result = await Handle(request);

        Assert.Equal(31, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(10, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("ref-1", row.Id);
        Assert.Equal("referrer-1", row.ReferrerUserId);
        Assert.Equal("referrer@x.test", row.ReferrerEmail);
        Assert.Equal("referred-1", row.ReferredUserId);
        Assert.Equal("referred@x.test", row.ReferredEmail);
        Assert.Equal(ReferralStatus.Qualified, row.Status);
        Assert.NotNull(row.FirstQualifyingOrderOn);
        Assert.Equal(150m, row.CreditAwardedToReferrer);
        Assert.Equal("CZK", row.ReferrerCreditCurrencyCode);
        Assert.Equal(6m, row.CreditAwardedToReferred);
        Assert.Equal("EUR", row.ReferredCreditCurrencyCode);
        Assert.Null(row.HoldReasons);
    }

    [Fact]
    public async Task A_Held_Row_Carries_Its_Hold_Reasons()
    {
        var held = Referral.CreateAccepted("referrer-1", "referred-1", "code-1", "system");
        held.HoldForReview("order-1", "address,phone", "system");
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { held }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedReferrals.Request());

        var row = Assert.Single(result.Data);
        Assert.Equal(ReferralStatus.Accepted, row.Status);
        Assert.Equal("address,phone", row.HoldReasons);
        Assert.Null(row.ReferrerCreditCurrencyCode);
        Assert.Null(row.ReferredCreditCurrencyCode);
    }

    [Fact]
    public async Task Null_Users_Map_To_Null_Emails()
    {
        var referral = QualifiedReferral(referrer: null, referred: null);
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { referral }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedReferrals.Request());

        var row = Assert.Single(result.Data);
        Assert.Null(row.ReferrerEmail);
        Assert.Null(row.ReferredEmail);
    }

    [Fact]
    public async Task Status_Filter_Reaches_Specification()
    {
        Expression<Func<Referral, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Referral, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<Referral>().AsQueryable().BuildMock());

        var request = new GetPagedReferrals.Request
        {
            Filter = new ReferralFilter(Status: ReferralStatus.Reversed)
        };
        await Handle(request);

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var reversed = Referral.CreateAccepted("a", "b", "c", "system");
        reversed.MarkQualified("o", "czk", 1m, "czk", 1m, "system");
        reversed.Reverse("system");
        var accepted = Referral.CreateAccepted("d", "e", "f", "system");
        Assert.True(predicate(reversed));
        Assert.False(predicate(accepted));
    }

    /// <summary>
    /// Held is the Accepted row with hold reasons: neither a plain Accepted row nor one released (Qualified) or
    /// rejected (Reversed) after a hold, which keeps its reasons as history.
    /// </summary>
    [Theory]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, false, true, true, true)]
    public async Task Held_Filter_Reaches_Specification(bool held, bool matchesHeld, bool matchesAccepted, bool matchesReleased, bool matchesRejected)
    {
        Expression<Func<Referral, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Referral, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.ReferralSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Referral, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<Referral>().AsQueryable().BuildMock());

        await Handle(new GetPagedReferrals.Request { Filter = new ReferralFilter(Held: held) });

        var predicate = captured!.Compile();
        var heldRow = Referral.CreateAccepted("a", "b", "c", "system");
        heldRow.HoldForReview("o", Referral.HoldReasonAddress, "system");
        var released = Referral.CreateAccepted("a", "b", "c", "system");
        released.HoldForReview("o", Referral.HoldReasonAddress, "system");
        released.ForceQualify("czk", 1m, "czk", 1m, "admin");
        var rejected = Referral.CreateAccepted("a", "b", "c", "system");
        rejected.HoldForReview("o", Referral.HoldReasonAddress, "system");
        rejected.Reverse("admin");
        Assert.Equal(matchesHeld, predicate(heldRow));
        Assert.Equal(matchesAccepted, predicate(Referral.CreateAccepted("d", "e", "f", "system")));
        Assert.Equal(matchesReleased, predicate(released));
        Assert.Equal(matchesRejected, predicate(rejected));
    }
}
