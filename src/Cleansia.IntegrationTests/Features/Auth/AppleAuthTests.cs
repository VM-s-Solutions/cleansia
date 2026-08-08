using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Constants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// The Apple half of <see cref="GoogleAuthTests"/>: the same two facts over real Postgres, where the
/// absence of a row is a statement about the committed database rather than about a mock.
/// </summary>
[Collection("PostgresCollection")]
public class AppleAuthTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string IdentityToken = "valid-apple-token";
    private const string RawNonce = "raw-nonce";
    private const string AppleSubject = "apple-sub-001";

    [Fact]
    public async Task ShouldCreateNewUserAndReturnTokenWhenTheTermsWereAccepted()
    {
        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IAppleTokenVerifier>(_ => Verifier()));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                await context.SaveChangesAsync();
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(Command(termsAccepted: true));
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.NotEmpty(result.Value.Token);

                var user = await context.Users.FirstOrDefaultAsync(
                    u => u.Email == Constants.TestUserSession.TestUserEmail);
                Assert.NotNull(user);
                Assert.Equal(AppleSubject, user.AppleId);
                Assert.Equal(AuthenticationType.Apple, user.AuthenticationType);

                Assert.NotNull(await context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id));
            });
    }

    // The unit test proves the handler never calls Add; this proves the commit that follows it writes no
    // row either — the pipeline commits after the handler returns, so only the database can say so.
    [Fact]
    public async Task ShouldRefuseAnUnknownIdentityAndPersistNoUserWhenTheTermsWereNotAccepted()
    {
        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IAppleTokenVerifier>(_ => Verifier()));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                await context.SaveChangesAsync();
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(Command(termsAccepted: false));
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.SocialAccountNotFound, result.Error!.Message);
                Assert.Equal(nameof(AppleAuth.Command.TermsAccepted), result.Error!.Code);

                Assert.Empty(await context.Users.ToListAsync());
                Assert.Empty(await context.Carts.ToListAsync());
            });
    }

    private static IAppleTokenVerifier Verifier()
    {
        var verifier = new Mock<IAppleTokenVerifier>();
        verifier
            .Setup(v => v.VerifyAsync(IdentityToken, RawNonce, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(
                AppleSubject, Constants.TestUserSession.TestUserEmail, EmailVerified: true));

        return verifier.Object;
    }

    private static AppleAuth.Command Command(bool termsAccepted) =>
        new(
            IdentityToken: IdentityToken,
            RawNonce: RawNonce,
            FirstName: Constants.TestUserSession.TestFirstName,
            LastName: Constants.TestUserSession.TestLastName,
            TermsAccepted: termsAccepted);
}
