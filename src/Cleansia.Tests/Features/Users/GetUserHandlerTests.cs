using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// ADR-0001 §D3 part 3 — the inner ownership gate inside
/// <c>GetUser.Handler</c>. The policy is the outer gate; this handler check is the inner gate that
/// holds regardless of host or invocation path:
///   - a NON-admin caller asking for a UserId that is NOT their own sub gets the not-found business
///     error (<see cref="BusinessErrorMessage.NotExistingUserWithId"/>) and NOT the other user's PII;
///   - an Admin caller, or a caller asking for their OWN UserId, gets the detail.
/// These tests predate the handler fix (red → green) per knowledge/testing.md.
/// </summary>
public class GetUserHandlerTests
{
    private const string CallerSub = "caller-sub-1";
    private const string OtherUserId = "other-user-2";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private GetUser.Handler CreateHandler() =>
        (GetUser.Handler)Activator.CreateInstance(
            typeof(GetUser.Handler),
            _userRepository.Object,
            _session.Object)!;

    private void SetCaller(string sub, UserProfile role)
    {
        _session.Setup(s => s.GetUserId()).Returns(sub);
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
    }

    private User ArrangeUser(string id)
    {
        var user = UserMockFactory.Generate();
        // Force the entity id to the value the query asks for (BaseEntity.Id has a public setter).
        user.Id = id;
        _userRepository
            .Setup(r => r.GetByIdNoTrackingAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task NonAdmin_Requesting_Other_UserId_Returns_NotFound_And_No_Pii()
    {
        var other = ArrangeUser(OtherUserId);
        SetCaller(CallerSub, UserProfile.Employee);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetUser.Query(OtherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithId, result.Error!.Message);
        Assert.Equal(nameof(GetUser.Query.UserId), result.Error.Code);
        // The other user's PII never leaves the handler.
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public async Task NonAdmin_Requesting_Own_UserId_Returns_Detail()
    {
        ArrangeUser(CallerSub);
        SetCaller(CallerSub, UserProfile.Customer);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetUser.Query(CallerSub), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CallerSub, result.Value.Id);
    }

    [Fact]
    public async Task Admin_Requesting_Other_UserId_Returns_Detail()
    {
        var other = ArrangeUser(OtherUserId);
        SetCaller("admin-sub-9", UserProfile.Administrator);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetUser.Query(OtherUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OtherUserId, result.Value.Id);
    }
}
