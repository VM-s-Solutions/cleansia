using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminUsers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.AdminUsers;

/// <summary>
/// Admin create/edit accepts <c>BirthDate</c> + <c>PreferredLanguageCode</c>, and a name-only
/// update PRESERVES the stored values. Before the fix, <see cref="UpdateAdminUser"/> called
/// <c>User.Update(...)</c> without a birth date, so every admin edit silently nulled the stored
/// <c>BirthDate</c> — the preservation tests are RED on that code. Written red → green per
/// knowledge/testing.md.
/// </summary>
public class AdminUserProfileFieldsTests
{
    private const string AdminId = "admin-1";
    private const string SupportedLanguage = "cs";
    private const string UnknownLanguage = "xx";
    private static readonly DateOnly StoredBirthDate = new(1990, 5, 20);

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();

    public AdminUserProfileFieldsTests()
    {
        _languageRepository
            .Setup(l => l.ExistsWithCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _languageRepository
            .Setup(l => l.ExistsWithCodeAsync(SupportedLanguage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static User BuildAdmin(string id = AdminId, DateOnly? birthDate = null)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", UserProfile.Administrator);
        user.Id = id;
        user.UpdateBirthDate(birthDate);
        return user;
    }

    private User ArrangeStoredAdmin()
    {
        var admin = BuildAdmin(birthDate: StoredBirthDate);
        _userRepository.Setup(r => r.GetAll()).Returns(new[] { admin }.AsQueryable().BuildMock());
        return admin;
    }

    private static UpdateAdminUser.Command NameOnlyUpdate(
        DateOnly? birthDate = null, string? preferredLanguageCode = null)
        => new(
            UserId: AdminId,
            FirstName: "NewFirst",
            LastName: "NewLast",
            PhoneNumber: null,
            BirthDate: birthDate,
            PreferredLanguageCode: preferredLanguageCode);

    // an admin row with a non-null BirthDate survives a name-only edit untouched
    // (the old handler nulled it via User.Update(..., birthDate: null)).
    [Fact]
    public async Task When_Update_Omits_BirthDate_And_Language_Then_Stored_Values_Are_Preserved()
    {
        var admin = ArrangeStoredAdmin();
        var storedLanguage = admin.PreferredLanguageCode;

        var result = await InvokeUpdateHandler(NameOnlyUpdate());

        Assert.True(result.IsSuccess);
        Assert.Equal("NewFirst", admin.FirstName);
        Assert.Equal(StoredBirthDate, admin.BirthDate);
        Assert.Equal(storedLanguage, admin.PreferredLanguageCode);
    }

    // persist-and-read-back: supplied values land on the entity and come back through the
    // same projection GetAdminUserById uses.
    [Fact]
    public async Task When_Update_Supplies_BirthDate_And_Language_Then_Both_Persist_And_Read_Back()
    {
        var admin = ArrangeStoredAdmin();
        var newBirthDate = new DateOnly(1985, 2, 10);

        var result = await InvokeUpdateHandler(NameOnlyUpdate(newBirthDate, SupportedLanguage));

        Assert.True(result.IsSuccess);
        var dto = admin.MapToAdminDetailDto();
        Assert.Equal(newBirthDate, dto!.BirthDate);
        Assert.Equal(SupportedLanguage, dto.PreferredLanguageCode);
    }

    [Fact]
    public async Task When_Create_Supplies_BirthDate_And_Language_Then_Both_Are_Persisted()
    {
        User? captured = null;
        _userRepository.Setup(r => r.Add(It.IsAny<User>())).Callback<User>(u => captured = u);
        var birthDate = new DateOnly(1992, 7, 1);

        var result = await InvokeCreateHandler(new CreateAdminUser.Command(
            Email: "new-admin@example.com",
            Password: "Password1",
            FirstName: "First",
            LastName: "Last",
            PhoneNumber: null,
            BirthDate: birthDate,
            PreferredLanguageCode: SupportedLanguage));

        Assert.True(result.IsSuccess);
        Assert.Equal(birthDate, captured!.BirthDate);
        Assert.Equal(SupportedLanguage, captured.PreferredLanguageCode);
    }

    [Fact]
    public async Task When_Update_BirthDate_Is_Not_In_The_Past_Then_Validation_Fails_With_DateMustBeInPast()
    {
        ArrangeStoredAdmin();
        var validator = new UpdateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(
            NameOnlyUpdate(birthDate: DateOnly.FromDateTime(DateTime.Today).AddDays(1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.DateMustBeInPast);
    }

    // admins share the platform-wide 18+ floor — a birth date of yesterday is in the past
    // but still rejected.
    [Fact]
    public async Task When_Update_BirthDate_Is_Under_18_Then_Validation_Fails_With_InvalidAge()
    {
        ArrangeStoredAdmin();
        var validator = new UpdateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(
            NameOnlyUpdate(birthDate: DateOnly.FromDateTime(DateTime.Today).AddDays(-1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidAge);
    }

    [Fact]
    public async Task When_Update_Language_Is_Unknown_Then_Validation_Fails_With_LanguageNotSupported()
    {
        ArrangeStoredAdmin();
        var validator = new UpdateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(NameOnlyUpdate(preferredLanguageCode: UnknownLanguage));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.LanguageNotSupported);
    }

    [Fact]
    public async Task When_Update_Supplies_Valid_BirthDate_And_Language_Then_Validation_Passes()
    {
        ArrangeStoredAdmin();
        var validator = new UpdateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(NameOnlyUpdate(StoredBirthDate, SupportedLanguage));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_Create_BirthDate_Is_Not_In_The_Past_Then_Validation_Fails_With_DateMustBeInPast()
    {
        _userRepository.Setup(r => r.GetAll()).Returns(Array.Empty<User>().AsQueryable().BuildMock());
        var validator = new CreateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(new CreateAdminUser.Command(
            "new-admin@example.com", "Password1", "First", "Last", null,
            BirthDate: DateOnly.FromDateTime(DateTime.Today),
            PreferredLanguageCode: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.DateMustBeInPast);
    }

    [Fact]
    public async Task When_Create_BirthDate_Is_Under_18_Then_Validation_Fails_With_InvalidAge()
    {
        _userRepository.Setup(r => r.GetAll()).Returns(Array.Empty<User>().AsQueryable().BuildMock());
        var validator = new CreateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(new CreateAdminUser.Command(
            "new-admin@example.com", "Password1", "First", "Last", null,
            BirthDate: DateOnly.FromDateTime(DateTime.Today).AddDays(-1),
            PreferredLanguageCode: null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidAge);
    }

    [Fact]
    public async Task When_Create_Language_Is_Unknown_Then_Validation_Fails_With_LanguageNotSupported()
    {
        _userRepository.Setup(r => r.GetAll()).Returns(Array.Empty<User>().AsQueryable().BuildMock());
        var validator = new CreateAdminUser.Validator(_userRepository.Object, _languageRepository.Object);

        var result = await validator.ValidateAsync(new CreateAdminUser.Command(
            "new-admin@example.com", "Password1", "First", "Last", null,
            BirthDate: null,
            PreferredLanguageCode: UnknownLanguage));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.LanguageNotSupported);
    }

    private async Task<BusinessResult<UpdateAdminUser.Response>> InvokeUpdateHandler(UpdateAdminUser.Command command)
    {
        var handlerType = typeof(UpdateAdminUser).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _userRepository.Object)!;
        var task = (Task<BusinessResult<UpdateAdminUser.Response>>)handlerType!.GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }

    private async Task<BusinessResult<CreateAdminUser.Response>> InvokeCreateHandler(CreateAdminUser.Command command)
    {
        var handlerType = typeof(CreateAdminUser).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _userRepository.Object)!;
        var task = (Task<BusinessResult<CreateAdminUser.Response>>)handlerType!.GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }
}
