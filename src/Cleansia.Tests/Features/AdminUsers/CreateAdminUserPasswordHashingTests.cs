using System.Reflection;
using Cleansia.Core.AppServices.Features.AdminUsers;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database.Converters;
using Moq;

namespace Cleansia.Tests.Features.AdminUsers;

/// <summary>
/// T-0108 (IA-1): <see cref="CreateAdminUser"/> double-hashed the password — the handler called
/// <see cref="PasswordExtensions.HashAndSaltPassword"/> on the raw password AND the EF write-side
/// <see cref="PasswordConverter"/> hashes again on persist, so the stored value was
/// <c>hash(hash(password))</c> and <see cref="PasswordExtensions.VerifyPassword"/> could never match —
/// every admin created this way was silently locked out.
///
/// The fix passes the RAW password into <see cref="User.CreateWithPassword"/> (matching the shipping
/// <c>Register</c>/<c>RegisterEmployee</c> pattern) and lets the converter hash exactly once.
///
/// The double-hash only manifests on persist, so the test models the real persist path: it runs the
/// password the handler stored on the entity through the REAL <see cref="PasswordConverter"/> write
/// conversion (<see cref="Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter.ConvertToProvider"/>),
/// then verifies the round-trip with <see cref="PasswordExtensions.VerifyPassword"/>. Written red → green
/// per knowledge/testing.md: RED on the current double-hash code, GREEN after the one-line fix.
/// </summary>
public class CreateAdminUserPasswordHashingTests
{
    private const string RawPassword = "Sup3rSecret!";
    private const string WrongPassword = "WrongPassword!";

    // The exact EF write-side conversion that runs on persist (UserEntityConfiguration wires this
    // converter on User.Password). Using the real converter proves the actual stored value.
    private static readonly Func<object?, object?> WriteConversion = new PasswordConverter().ConvertToProvider;

    // AC1 / AC3 — round-trip: an admin created with a raw password, persisted THROUGH the converter,
    // yields a stored value that VerifyPassword(raw) accepts (hashed exactly once). Guards the negative
    // too: a wrong password verifies false.
    [Fact]
    public async Task Created_Admin_Password_RoundTrips_Through_Converter_And_Verifies()
    {
        var captured = await CreateAdminAndCaptureUser();

        // Model the EF persist step: the converter hashes the value the handler put on the entity.
        var stored = (string?)WriteConversion(captured.Password);

        Assert.False(string.IsNullOrEmpty(stored));
        // On the buggy handler the entity already holds hash(P); the converter makes hash(hash(P)) and
        // this assertion fails (RED). After the fix the entity holds raw P, stored = hash(P) (GREEN).
        Assert.True(RawPassword.VerifyPassword(stored!),
            "The raw password must verify against the single-hashed stored value.");
        // Negative guard: a different password must never verify.
        Assert.False(WrongPassword.VerifyPassword(stored!),
            "A wrong password must not verify against the stored value.");
    }

    // AC2 — the handler stores the RAW password on the entity (the single hash happens in the converter),
    // i.e. the entity's Password is NOT pre-hashed. On the buggy code the stored value is already a
    // "v2$" hash, so this fails (RED); after the fix it equals the raw password (GREEN).
    [Fact]
    public async Task Handler_Stores_Raw_Password_On_Entity_Not_PreHashed()
    {
        var captured = await CreateAdminAndCaptureUser();

        Assert.Equal(RawPassword, captured.Password);
    }

    private static async Task<User> CreateAdminAndCaptureUser()
    {
        var userRepository = new Mock<IUserRepository>();
        User? captured = null;
        userRepository.Setup(r => r.Add(It.IsAny<User>())).Callback<User>(u => captured = u);

        var command = new CreateAdminUser.Command(
            Email: "new-admin@example.com",
            Password: RawPassword,
            FirstName: "First",
            LastName: "Last",
            PhoneNumber: null);

        var result = await InvokeHandler(userRepository.Object, command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        return captured!;
    }

    // CreateAdminUser.Handler is internal; resolve it via reflection (no compile-time reference),
    // matching ChangePasswordSecurityTests.
    private static async Task<BusinessResult<CreateAdminUser.Response>> InvokeHandler(
        IUserRepository userRepository, CreateAdminUser.Command command)
    {
        var handlerType = typeof(CreateAdminUser).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, userRepository)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<CreateAdminUser.Response>>)handleMethod!.Invoke(
            handler, [command, CancellationToken.None])!;
        return await task;
    }
}
