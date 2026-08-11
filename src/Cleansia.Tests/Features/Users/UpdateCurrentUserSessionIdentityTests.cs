using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// The identity contract of UpdateCurrentUser: the row written is the JWT caller's and the body's
/// <c>Id</c> is inert (S1, [OWN-DATA]).
///
/// The rule it replaces compared the SESSION user's id to the CLIENT-supplied one, so it only ever
/// passed for a caller that already knew the answer. Mobile knows it (it decodes its own token); the
/// web cannot (HttpOnly cookie session, and MyProfileDto carries no id), so every web profile save was
/// rejected. The existing validator tests all handed the validator a matching id, which is exactly why
/// nothing caught it — these pin the wrong-id, absent-id and foreign-session cases instead.
/// </summary>
public class UpdateCurrentUserSessionIdentityTests
{
    private const string CallerId = "caller-1";
    private const string CallerEmail = "caller@cleansia.cz";
    private const string OtherUserId = "someone-else";
    private const string Phone = "+420123456789";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();

    private static User UserWith(string id, string email)
    {
        var user = User.CreateWithPassword(email, "Password1", "First", "Last");
        user.ConfirmEmail();
        user.Id = id;
        return user;
    }

    private User ArrangeCaller()
    {
        var caller = UserWith(CallerId, CallerEmail);
        _session.Setup(s => s.GetUserId()).Returns(CallerId);
        _session.Setup(s => s.GetUserEmail()).Returns(CallerEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(CallerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepository
            .Setup(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepository
            .Setup(r => r.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _orderRepository
            .Setup(r => r.GetOrdersByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());
        return caller;
    }

    private UpdateCurrentUser.Validator CreateValidator() =>
        new(_userRepository.Object, _session.Object, _languageRepository.Object);

    private UpdateCurrentUser.Handler CreateHandler() => new(
        _userRepository.Object, _orderRepository.Object, _session.Object, _blobFactory.Object);

    private static UpdateCurrentUser.Command Save(string? id) => new(
        Id: id,
        FirstName: "New",
        LastName: "Name",
        PhoneNumber: Phone,
        BirthDate: null,
        Photo: null,
        LanguageCode: null);

    [Fact]
    public async Task Validator_accepts_a_foreign_id_because_the_id_is_never_read()
    {
        ArrangeCaller();

        var result = await CreateValidator().ValidateAsync(Save(OtherUserId));

        Assert.True(result.IsValid);
    }

    /// <summary>The web case: NSwag leaves <c>id</c> undefined and JSON.stringify drops it.</summary>
    [Fact]
    public async Task Validator_accepts_a_command_with_no_id_at_all()
    {
        ArrangeCaller();

        var result = await CreateValidator().ValidateAsync(Save(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_accepts_a_blank_id()
    {
        ArrangeCaller();

        var result = await CreateValidator().ValidateAsync(Save(string.Empty));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The phone-uniqueness rule has to exempt the CALLER's own number, and it too resolved the owner
    /// by the client-supplied id — so a web save that changed nothing but the name was rejected as a
    /// duplicate of the caller's own phone.
    /// </summary>
    [Fact]
    public async Task Validator_exempts_the_callers_own_phone_number_when_the_command_carries_no_id()
    {
        var caller = ArrangeCaller();
        _userRepository
            .Setup(r => r.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var result = await CreateValidator().ValidateAsync(Save(null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_still_rejects_a_phone_number_owned_by_another_user()
    {
        ArrangeCaller();
        _userRepository
            .Setup(r => r.GetByPhoneNumberAsync(Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWith(OtherUserId, "other@cleansia.cz"));

        var result = await CreateValidator().ValidateAsync(Save(null));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ExistingPhoneNumber);
    }

    /// <summary>
    /// The property that must never regress: a session may only ever write its OWN row. A body naming
    /// another user is not rejected — it is ignored, and the caller's own row is what changes.
    /// </summary>
    [Fact]
    public async Task Handler_writes_the_session_users_row_and_never_loads_the_id_from_the_body()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(OtherUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CallerId, result.Value.Id);
        Assert.Equal("New", caller.FirstName);
        _userRepository.Verify(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.GetByIdAsync(OtherUserId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handler_writes_the_session_users_row_when_the_body_carries_no_id()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CallerId, result.Value.Id);
        Assert.Equal("New", caller.FirstName);
    }
}
