using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Users;

/// <summary>
/// The avatar read path end-to-end through the real mediator pipeline and the real blob client (the SAS
/// is signed locally from the configured storage key — no storage round-trip). Pins that a stored profile
/// photo comes back as a resolvable, single-blob, read-only URI beside its stable blob name, and that a
/// user without a photo gets no reference at all rather than a broken one.
/// </summary>
[Collection("PostgresCollection")]
public class GetCurrentUserProfilePhotoUriTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string StoredPhotoName = "0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b";

    [Fact]
    public async Task Profile_WithStoredPhoto_CarriesASingleBlobReadSasBesideTheBlobName()
    {
        await TestMethod(
            arrange: context => SeedConfirmedUser(context, StoredPhotoName),
            act: async provider =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetCurrentUser.Query());
            },
            assert: (CleansiaDbContext _, BusinessResult<MyProfileDto> result) =>
            {
                Assert.True(result.IsSuccess);
                var photo = result.Value.ProfilePhoto;
                Assert.NotNull(photo);
                Assert.Equal(StoredPhotoName, photo!.FileName);

                Assert.False(string.IsNullOrWhiteSpace(photo.BlobUrl));
                var uri = new Uri(photo.BlobUrl!);
                Assert.EndsWith($"/{Constants.BlobContainers.UserFiles}/{StoredPhotoName}", uri.AbsolutePath);
                Assert.Contains("sig=", uri.Query);
                Assert.Contains("sr=b", uri.Query);
                Assert.Contains("sp=r", uri.Query);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Profile_WithNoStoredPhoto_EmitsNoReferenceAtAll()
    {
        await TestMethod(
            arrange: context => SeedConfirmedUser(context, profilePhotoName: null),
            act: async provider =>
            {
                using var scope = provider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetCurrentUser.Query());
            },
            assert: (CleansiaDbContext _, BusinessResult<MyProfileDto> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.Null(result.Value.ProfilePhoto);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedConfirmedUser(CleansiaDbContext context, string? profilePhotoName)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();
        }

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        user.Id = TestConstants.TestUserSession.TestUserId;
        user.ConfirmEmail();
        user.UpdateProfilePhotoName(profilePhotoName);
        context.Users.Add(user);
        await context.CommitAsync(CancellationToken.None);
    }
}
