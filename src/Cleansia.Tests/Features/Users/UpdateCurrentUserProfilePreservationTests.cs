using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// UpdateCurrentUser is a full replace, so a field the client had nothing to say about used to DELETE
/// what was stored. Phone is the damaging one: the handler copies it onto every order that carried the
/// old number, so one save with a blank phone erases the contact number the crew calls from the user
/// row AND from their whole order history.
///
/// The same defect was already found and fixed on the admin path (see AdminUserProfileFieldsTests) —
/// these pin the identical contract here: absent means "no change", matching how Photo and LanguageCode
/// already behave in this handler.
///
/// Also pins the language rule. PreferredLanguageCode is an FK onto Languages.Code, so an unsupported
/// code is not a bad save, it is a constraint violation raised by the UnitOfWork commit AFTER the
/// handler returns — a 500 with no business error. It has to be rejected by the validator.
/// </summary>
public class UpdateCurrentUserProfilePreservationTests
{
    private const string CallerId = "caller-1";
    private const string CallerEmail = "caller@cleansia.cz";
    private const string StoredPhone = "+420777111222";
    private const string NewPhone = "+420777999888";
    private const string SupportedLanguage = "uk";
    private const string UnknownLanguage = "xx";
    private static readonly DateOnly StoredBirthDate = new(1990, 5, 20);

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();

    public UpdateCurrentUserProfilePreservationTests()
    {
        _languageRepository
            .Setup(l => l.ExistsWithCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _languageRepository
            .Setup(l => l.ExistsWithCodeAsync(SupportedLanguage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private User ArrangeCaller(IReadOnlyList<Order>? orders = null)
    {
        var caller = User.CreateWithPassword(CallerEmail, "Password1", "First", "Last", UserProfile.Employee);
        caller.ConfirmEmail();
        caller.Id = CallerId;
        caller.Update("First", "Last", StoredPhone, StoredBirthDate);
        caller.UpdateLanguagePreference("en");

        _session.Setup(s => s.GetUserId()).Returns(CallerId);
        _session.Setup(s => s.GetUserEmail()).Returns(CallerEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(CallerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepository
            .Setup(r => r.GetByIdAsync(CallerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _userRepository
            .Setup(r => r.GetByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _orderRepository
            .Setup(r => r.GetOrdersByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders ?? Array.Empty<Order>());
        return caller;
    }

    private static Order OrderWithPhone(string phone)
    {
        var address = Address.Create("Test St 1", "Prague", "11000", "cz");
        return Order.Create(
            customerName: "Cust Omer",
            customerEmail: "cust@cleansia.cz",
            customerPhone: phone,
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: CallerId);
    }

    private UpdateCurrentUser.Handler CreateHandler() => new(
        _userRepository.Object, _orderRepository.Object, _session.Object, _blobFactory.Object);

    private UpdateCurrentUser.Validator CreateValidator() => new(
        _userRepository.Object, _session.Object, _languageRepository.Object);

    private static UpdateCurrentUser.Command Save(
        string phoneNumber = NewPhone,
        DateOnly? birthDate = null,
        string? languageCode = null) => new(
        Id: null,
        FirstName: "First",
        LastName: "Last",
        PhoneNumber: phoneNumber,
        BirthDate: birthDate,
        Photo: null,
        LanguageCode: languageCode);

    [Fact]
    public async Task A_blank_phone_number_leaves_the_stored_phone_untouched()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(phoneNumber: string.Empty), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredPhone, caller.PhoneNumber);
    }

    [Fact]
    public async Task A_whitespace_phone_number_leaves_the_stored_phone_untouched()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(phoneNumber: "   "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredPhone, caller.PhoneNumber);
    }

    /// <summary>The damaging half: the blank does not stop at the user row.</summary>
    [Fact]
    public async Task A_blank_phone_number_is_not_copied_onto_the_users_orders()
    {
        var order = OrderWithPhone(StoredPhone);
        ArrangeCaller([order]);

        var result = await CreateHandler().Handle(Save(phoneNumber: string.Empty), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredPhone, order.CustomerPhone);
    }

    /// <summary>Non-vacuity: a real phone still replaces both the user row and the order history.</summary>
    [Fact]
    public async Task A_supplied_phone_number_still_replaces_the_user_and_the_orders()
    {
        var order = OrderWithPhone(StoredPhone);
        var caller = ArrangeCaller([order]);

        var result = await CreateHandler().Handle(Save(phoneNumber: NewPhone), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewPhone, caller.PhoneNumber);
        Assert.Equal(NewPhone, order.CustomerPhone);
    }

    [Fact]
    public async Task An_omitted_birth_date_leaves_the_stored_birth_date_untouched()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(birthDate: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredBirthDate, caller.BirthDate);
    }

    [Fact]
    public async Task A_supplied_birth_date_still_replaces_the_stored_one()
    {
        var caller = ArrangeCaller();
        var newBirthDate = new DateOnly(1985, 2, 10);

        var result = await CreateHandler().Handle(Save(birthDate: newBirthDate), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newBirthDate, caller.BirthDate);
    }

    /// <summary>The whole point of exposing this command on the partner host.</summary>
    [Fact]
    public async Task A_supplied_language_code_replaces_the_stored_preference()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(languageCode: SupportedLanguage), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupportedLanguage, caller.PreferredLanguageCode);
    }

    [Fact]
    public async Task An_omitted_language_code_leaves_the_stored_preference_untouched()
    {
        var caller = ArrangeCaller();

        var result = await CreateHandler().Handle(Save(languageCode: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("en", caller.PreferredLanguageCode);
    }

    [Fact]
    public async Task An_unsupported_language_code_is_rejected_by_the_validator()
    {
        ArrangeCaller();

        var result = await CreateValidator().ValidateAsync(Save(languageCode: UnknownLanguage));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.LanguageNotSupported);
    }

    [Fact]
    public async Task A_supported_language_code_passes_validation()
    {
        ArrangeCaller();

        var result = await CreateValidator().ValidateAsync(Save(languageCode: SupportedLanguage));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_absent_language_code_never_reaches_the_language_repository()
    {
        ArrangeCaller();

        var result = await CreateValidator().ValidateAsync(Save(languageCode: null));

        Assert.True(result.IsValid);
        _languageRepository.Verify(
            l => l.ExistsWithCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
