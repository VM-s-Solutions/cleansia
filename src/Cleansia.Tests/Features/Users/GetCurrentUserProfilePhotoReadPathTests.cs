using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// The avatar READ path. The write path stores a bare blob name, and the profile DTO used to hand the
/// client that name with no bytes and no URI — so no client could render an avatar even after a
/// completely successful upload. These pin the contract that <c>GetCurrentUser</c> emits a resolvable,
/// narrowly-scoped, short-lived read SAS for the caller's OWN blob, and degrades to a null URL rather
/// than failing the profile read when storage is unavailable.
/// </summary>
public class GetCurrentUserProfilePhotoReadPathTests
{
    private const string UserId = "user-1";
    private const string UserEmail = "user@cleansia.cz";
    private const string StoredPhotoName = "0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b";

    private const string SasUrl =
        "https://acct.blob.core.windows.net/user-files/0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b" +
        "?sv=2025-01-05&sr=b&sp=r&se=2026-07-30T18%3A00%3A00Z&sig=Zm9vYmFy";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<IBlobContainerClient> _blobClient = new();
    private readonly Mock<ILogger<GetCurrentUser.Handler>> _logger = new();

    private GetCurrentUser.Handler CreateHandler(string? profilePhotoName)
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.Id = UserId;
        user.UpdateProfilePhotoName(profilePhotoName);

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailNoTrackingAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _orderRepository
            .Setup(r => r.GetCustomerProfileStatsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CustomerProfileStats.Empty);

        _blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobClient.Object);
        _blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(new Uri(SasUrl));

        return new GetCurrentUser.Handler(
            _userRepository.Object,
            _orderRepository.Object,
            _session.Object,
            _blobFactory.Object,
            _logger.Object);
    }

    /// <summary>
    /// AC1 / Gate 6.5 — the defect itself. Goes RED the moment the mapper falls back to emitting a bare
    /// blob name with no URI (the pre-fix <c>BlobMapper.MapToDto</c> behaviour).
    /// </summary>
    [Fact]
    public async Task Profile_WithStoredPhoto_CarriesAResolvableUrlAlongsideTheStableBlobName()
    {
        var result = await CreateHandler(StoredPhotoName).Handle(new GetCurrentUser.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var photo = result.Value.ProfilePhoto;
        Assert.NotNull(photo);
        Assert.Equal(SasUrl, photo!.BlobUrl);
        Assert.Equal(StoredPhotoName, photo.FileName);
        Assert.True(Uri.IsWellFormedUriString(photo.BlobUrl, UriKind.Absolute));
    }

    /// <summary>
    /// AC3 — the grant asked for is one blob for one hour, from the user-files container. A widened
    /// container-scoped or long-lived request would fail here.
    /// </summary>
    [Fact]
    public async Task Profile_WithStoredPhoto_AsksForExactlyOneOneHourSasOnTheStoredBlobName()
    {
        await CreateHandler(StoredPhotoName).Handle(new GetCurrentUser.Query(), CancellationToken.None);

        _blobFactory.Verify(f => f.GetBlobContainerClient(Constants.BlobContainers.UserFiles), Times.Once);
        _blobClient.Verify(c => c.GenerateSasUri(StoredPhotoName, TimeSpan.FromHours(1)), Times.Once);
        _blobClient.VerifyNoOtherCalls();
    }

    /// <summary>AC2 — no photo means no reference at all, and no pointless trip to storage.</summary>
    [Fact]
    public async Task Profile_WithNoStoredPhoto_EmitsNoPhoto_AndNeverTouchesBlobStorage()
    {
        var result = await CreateHandler(null).Handle(new GetCurrentUser.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ProfilePhoto);
        _blobFactory.VerifyNoOtherCalls();
        _blobClient.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Graceful degradation: the avatar is decoration, the profile is the core action. A storage
    /// outage must cost the image, never the screen.
    /// </summary>
    [Fact]
    public async Task Profile_WhenSasGenerationThrows_StillReturnsTheProfile_WithANullUrl()
    {
        var handler = CreateHandler(StoredPhotoName);
        _blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Throws(new InvalidOperationException("storage unreachable"));

        var result = await handler.Handle(new GetCurrentUser.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ProfilePhoto);
        Assert.Equal(StoredPhotoName, result.Value.ProfilePhoto!.FileName);
        Assert.Null(result.Value.ProfilePhoto.BlobUrl);
    }

    [Fact]
    public async Task Profile_WhenSasGenerationThrows_LogsTheFailure_WithoutSwallowingItSilently()
    {
        var handler = CreateHandler(StoredPhotoName);
        _blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Throws(new InvalidOperationException("storage unreachable"));

        await handler.Handle(new GetCurrentUser.Query(), CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// S1/S3 — the query carries no identifier at all; the blob signed is the one on the row resolved
    /// from the session email, so there is no input a caller could bend towards another user's avatar.
    /// </summary>
    [Fact]
    public async Task Profile_SignsTheBlobNameOffTheSessionUsersOwnRow()
    {
        var otherUsersPhoto = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        var handler = CreateHandler(StoredPhotoName);

        await handler.Handle(new GetCurrentUser.Query(), CancellationToken.None);

        _userRepository.Verify(
            r => r.GetByEmailNoTrackingAsync(UserEmail, It.IsAny<CancellationToken>()), Times.Once);
        _blobClient.Verify(
            c => c.GenerateSasUri(otherUsersPhoto, It.IsAny<TimeSpan>()), Times.Never);
        _blobClient.Verify(
            c => c.GenerateSasUri(StoredPhotoName, It.IsAny<TimeSpan>()), Times.Once);
    }

    /// <summary>
    /// S6 tripwire — a signed URL in a log is a credential in a log. Nothing on the success path logs
    /// today; this goes red the moment anything logs the resolved URI.
    /// </summary>
    [Fact]
    public async Task Profile_NeverLogsTheSignedUrl_OnTheSuccessPath()
    {
        await CreateHandler(StoredPhotoName).Handle(new GetCurrentUser.Query(), CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("sig=")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
