using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// Account pre-hijacking, walked end to end on Postgres, one DI scope per request as in production. Someone
/// registers an address that is not theirs and keeps the password; the owner confirms it from their mailbox,
/// whether or not they registered it themselves. Registering again used to keep the first registrant's
/// password, and confirming a single registration still kept it, so that stranger signed in to the owner's
/// account from then on. Now the confirmation leaves the password its confirmer chose and no other, the only
/// session is the one the confirmation opened (an unconfirmed account is never given one), and a confirmation
/// that brings no password leaves none, which the owner then sets through the reset.
/// </summary>
[Collection("PostgresCollection")]
public class AccountPreHijackingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Email = "owner@cleansia.test";
    private const string StrangersPassword = "stranger-Passw0rd";
    private const string OwnersPassword = "owner-Passw0rd";
    private const string OwnersNewPassword = "owner-NewPassw0rd";

    private static readonly JsonSerializerOptions WireJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task A_Customer_Who_Confirms_An_Address_A_Stranger_Registered_First_Keeps_No_Password_The_Stranger_Chose()
    {
        await TestMethod(
            arrange: SeedLanguageAsync,
            act: async provider =>
            {
                var strangerRegisters = await SendAsync(provider, Customer(StrangersPassword));
                var strangerSignsInEarly = await SendAsync(provider, new Login.Command(Email, StrangersPassword, true));
                var ownerRegisters = await SendAsync(provider, Customer(OwnersPassword));
                var sessionsBeforeConfirmation = await SessionsAsync(provider);
                var ownerConfirms = await SendAsync(provider, new ConfirmUserEmail.Command(await LiveCodeAsync(provider, EmailType.ConfirmationEmail), Email));
                var sessionsAfterConfirmation = await SessionsAsync(provider);
                var strangerSignsIn = await SendAsync(provider, new Login.Command(Email, StrangersPassword, true));
                var ownerSignsIn = await SendAsync(provider, new Login.Command(Email, OwnersPassword, true));
                await SendAsync(provider, new RequestPasswordChange.Command(Email));
                var ownerResets = await SendAsync(provider, new ChangePassword.Command(Email, OwnersNewPassword, await LiveCodeAsync(provider, EmailType.ResetPassword)));
                var ownerSignsInAfterReset = await SendAsync(provider, new Login.Command(Email, OwnersNewPassword, true));
                return new Walk(
                    strangerRegisters, strangerSignsInEarly, ownerRegisters, sessionsBeforeConfirmation, ownerConfirms,
                    sessionsAfterConfirmation, strangerSignsIn, ownerSignsIn, ownerResets, ownerSignsInAfterReset);
            },
            assert: async (CleansiaDbContext context, Walk walk) =>
            {
                Assert.True(walk.StrangerRegisters.IsSuccess);
                Assert.True(walk.StrangerSignsInEarly.IsSuccess);
                Assert.False(walk.StrangerSignsInEarly.Value!.IsEmailConfirmed);
                Assert.Empty(walk.StrangerSignsInEarly.Value.Token);
                Assert.True(walk.OwnerRegisters.IsSuccess);
                Assert.Equal(0, walk.SessionsBeforeConfirmation);
                Assert.True(walk.OwnerConfirms.IsSuccess);
                Assert.NotEmpty(walk.OwnerConfirms.Value!.Token);
                Assert.Equal(1, walk.SessionsAfterConfirmation);

                AssertRefusedAsAWrongPassword(walk.StrangerSignsIn);
                AssertRefusedAsAWrongPassword(walk.OwnerSignsIn);
                Assert.True(walk.OwnerResets.IsSuccess);
                Assert.True(walk.OwnerSignsInAfterReset.IsSuccess);
                Assert.NotEmpty(walk.OwnerSignsInAfterReset.Value!.Token);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == Email);
                Assert.True(user.IsEmailConfirmed);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Who_Registers_Twice_With_The_Same_Password_And_Confirms_With_It_Signs_In_With_It()
    {
        await TestMethod(
            arrange: SeedLanguageAsync,
            act: async provider =>
            {
                await SendAsync(provider, Customer(OwnersPassword));
                await SendAsync(provider, Customer(OwnersPassword));
                await SendAsync(provider, new ConfirmUserEmail.Command(await LiveCodeAsync(provider, EmailType.ConfirmationEmail), Email, OwnersPassword));
                return await SendAsync(provider, new Login.Command(Email, OwnersPassword, true));
            },
            assert: (CleansiaDbContext _, BusinessResult<JwtTokenResponse> signIn) =>
            {
                Assert.True(signIn.IsSuccess);
                Assert.NotEmpty(signIn.Value!.Token);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Who_Confirms_A_Strangers_Only_Registration_Signs_In_With_The_Password_They_Chose_And_The_Stranger_Does_Not()
    {
        await TestMethod(
            arrange: SeedLanguageAsync,
            act: async provider =>
            {
                await SendAsync(provider, Customer(StrangersPassword));
                var ownerConfirms = await SendAsync(provider, new ConfirmUserEmail.Command(await LiveCodeAsync(provider, EmailType.ConfirmationEmail), Email, OwnersPassword));
                var strangerSignsIn = await SendAsync(provider, new Login.Command(Email, StrangersPassword, true));
                var ownerSignsIn = await SendAsync(provider, new Login.Command(Email, OwnersPassword, true));
                return (ownerConfirms, strangerSignsIn, ownerSignsIn, await SessionsAsync(provider));
            },
            assert: (CleansiaDbContext _, (BusinessResult<JwtTokenResponse> OwnerConfirms, BusinessResult<JwtTokenResponse> StrangerSignsIn, BusinessResult<JwtTokenResponse> OwnerSignsIn, int Sessions) walk) =>
            {
                Assert.True(walk.OwnerConfirms.IsSuccess);
                Assert.NotEmpty(walk.OwnerConfirms.Value!.Token);
                AssertRefusedAsAWrongPassword(walk.StrangerSignsIn);
                Assert.True(walk.OwnerSignsIn.IsSuccess);
                Assert.NotEmpty(walk.OwnerSignsIn.Value!.Token);
                Assert.Equal(2, walk.Sessions);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Confirmation_That_Brings_No_Password_Leaves_The_Strangers_Only_Registration_With_None_Working()
    {
        await TestMethod(
            arrange: SeedLanguageAsync,
            act: async provider =>
            {
                await SendAsync(provider, Customer(StrangersPassword));
                var ownerConfirms = await SendAsync(provider, new ConfirmUserEmail.Command(await LiveCodeAsync(provider, EmailType.ConfirmationEmail), Email));
                var strangerSignsIn = await SendAsync(provider, new Login.Command(Email, StrangersPassword, true));
                await SendAsync(provider, new RequestPasswordChange.Command(Email));
                var ownerResets = await SendAsync(provider, new ChangePassword.Command(Email, OwnersNewPassword, await LiveCodeAsync(provider, EmailType.ResetPassword)));
                var ownerSignsInAfterReset = await SendAsync(provider, new Login.Command(Email, OwnersNewPassword, true));
                return (ownerConfirms, strangerSignsIn, ownerResets, ownerSignsInAfterReset);
            },
            assert: (CleansiaDbContext _, (BusinessResult<JwtTokenResponse> OwnerConfirms, BusinessResult<JwtTokenResponse> StrangerSignsIn, BusinessResult<ChangePassword.Response> OwnerResets, BusinessResult<JwtTokenResponse> OwnerSignsInAfterReset) walk) =>
            {
                Assert.True(walk.OwnerConfirms.IsSuccess);
                Assert.NotEmpty(walk.OwnerConfirms.Value!.Token);
                AssertRefusedAsAWrongPassword(walk.StrangerSignsIn);
                Assert.True(walk.OwnerResets.IsSuccess);
                Assert.True(walk.OwnerSignsInAfterReset.IsSuccess);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Cleaner_Who_Confirms_A_Strangers_Only_Registration_Signs_In_With_The_Password_They_Chose_And_The_Stranger_Does_Not()
    {
        await TestMethod(
            setup: services =>
            {
                services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Partner));
                return Task.CompletedTask;
            },
            arrange: SeedLanguageAsync,
            act: async provider =>
            {
                await SendAsync(provider, Cleaner(StrangersPassword));
                var ownerConfirms = await SendAsync(provider, new ConfirmUserEmail.Command(await LiveCodeAsync(provider, EmailType.ConfirmationEmail), Email, OwnersPassword));
                var strangerSignsIn = await SendAsync(provider, new PartnerLogin.Command(Email, StrangersPassword, true));
                var ownerSignsIn = await SendAsync(provider, new PartnerLogin.Command(Email, OwnersPassword, true));
                return (ownerConfirms, strangerSignsIn, ownerSignsIn);
            },
            assert: (CleansiaDbContext _, (BusinessResult<JwtTokenResponse> OwnerConfirms, BusinessResult<JwtTokenResponse> StrangerSignsIn, BusinessResult<JwtTokenResponse> OwnerSignsIn) walk) =>
            {
                Assert.True(walk.OwnerConfirms.IsSuccess);
                Assert.NotEmpty(walk.OwnerConfirms.Value!.Token);
                AssertRefusedAsAWrongPassword(walk.StrangerSignsIn);
                Assert.True(walk.OwnerSignsIn.IsSuccess);
                Assert.NotEmpty(walk.OwnerSignsIn.Value!.Token);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Cleaner_Who_Confirms_An_Address_A_Stranger_Registered_First_Keeps_No_Password_The_Stranger_Chose()
    {
        await TestMethod(
            setup: services =>
            {
                services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Partner));
                return Task.CompletedTask;
            },
            arrange: SeedLanguageAsync,
            act: async provider =>
            {
                await SendAsync(provider, Cleaner(StrangersPassword));
                await SendAsync(provider, Cleaner(OwnersPassword));
                var ownerConfirms = await SendAsync(provider, new ConfirmUserEmail.Command(await LiveCodeAsync(provider, EmailType.ConfirmationEmail), Email));
                var strangerSignsIn = await SendAsync(provider, new PartnerLogin.Command(Email, StrangersPassword, true));
                var ownerSignsIn = await SendAsync(provider, new PartnerLogin.Command(Email, OwnersPassword, true));
                return (ownerConfirms, strangerSignsIn, ownerSignsIn);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<JwtTokenResponse> OwnerConfirms, BusinessResult<JwtTokenResponse> StrangerSignsIn, BusinessResult<JwtTokenResponse> OwnerSignsIn) walk) =>
            {
                Assert.True(walk.OwnerConfirms.IsSuccess);
                Assert.NotEmpty(walk.OwnerConfirms.Value!.Token);
                AssertRefusedAsAWrongPassword(walk.StrangerSignsIn);
                AssertRefusedAsAWrongPassword(walk.OwnerSignsIn);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == Email);
                Assert.Equal(UserProfile.Employee, user.Profile);
                Assert.Single(await context.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == user.Id).ToListAsync());
            },
            transactional: false);
    }

    private sealed record Walk(
        BusinessResult StrangerRegisters,
        BusinessResult<JwtTokenResponse> StrangerSignsInEarly,
        BusinessResult OwnerRegisters,
        int SessionsBeforeConfirmation,
        BusinessResult<JwtTokenResponse> OwnerConfirms,
        int SessionsAfterConfirmation,
        BusinessResult<JwtTokenResponse> StrangerSignsIn,
        BusinessResult<JwtTokenResponse> OwnerSignsIn,
        BusinessResult<ChangePassword.Response> OwnerResets,
        BusinessResult<JwtTokenResponse> OwnerSignsInAfterReset);

    private static Register.Command Customer(string password) =>
        new(Email, password, "Jana", "Nováková", "en", TermsAccepted: true);

    private static RegisterEmployee.Command Cleaner(string password) =>
        new(Email, password, "Jana", "Nováková", "en");

    private static void AssertRefusedAsAWrongPassword(BusinessResult<JwtTokenResponse> signIn)
    {
        Assert.True(signIn.IsFailure);
        Assert.Contains(Assert.IsAssignableFrom<IValidationResult>(signIn).Errors, e => e.Message == BusinessErrorMessage.InvalidPassword);
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    /// <summary>
    /// The raw code the mailbox holds for the code the account will take now: of every code e-mailed to the
    /// address, the one whose hash the row stores.
    /// </summary>
    private static async Task<string> LiveCodeAsync(IServiceProvider provider, EmailType emailType)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        var user = await context.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Email == Email);
        var stored = emailType == EmailType.ConfirmationEmail ? user.ConfirmationCode : user.ResetPasswordCode;
        var bodies = await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.QueueName == QueueNames.SendEmail)
            .Select(m => m.Body)
            .ToListAsync();
        return bodies
            .Select(body => JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(body, WireJson)!.Payload)
            .Single(message => message.EmailType == emailType && message.Email == Email && SecurityTokens.Hash(message.Code) == stored)
            .Code;
    }

    private static async Task<int> SessionsAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        var userId = await context.Users.IgnoreQueryFilters().Where(u => u.Email == Email).Select(u => u.Id).SingleAsync();
        return await context.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.UserId == userId);
    }

    private static async Task SeedLanguageAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();
    }
}
