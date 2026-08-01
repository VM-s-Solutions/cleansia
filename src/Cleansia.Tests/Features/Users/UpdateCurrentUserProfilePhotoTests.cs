using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// Data-loss guard for UpdateCurrentUser's profile-photo arm. A profile save that carries no photo
/// used to be indistinguishable from "the user asked to remove their photo": the handler branched on
/// <c>string.IsNullOrWhiteSpace(command.Photo?.FileName)</c>, which is also true when Photo is null,
/// so it deleted the blob and nulled the column. No client echoed the stored FileName back, so every
/// Android/iOS/web profile save destroyed the avatar.
///
/// The contract these tests pin: absence means "leave it alone", and removal is only ever the
/// explicit <c>RemovePhoto</c> flag.
/// </summary>
public class UpdateCurrentUserProfilePhotoTests
{
    private const string UserId = "user-1";
    private const string StoredPhotoName = "stored-avatar-blob";

    // 1x1 PNG, data-URI prefixed the way the browser clients send it, so UploadPhotoAsync's
    // ExtractBase64Data + Convert.FromBase64String path is exercised for real.
    private const string NewImageBase64 =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<IBlobContainerClient> _blobClient = new();
    private readonly List<string> _blobCalls = [];

    private UpdateCurrentUser.Handler CreateHandler(User user)
    {
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _orderRepository
            .Setup(r => r.GetOrdersByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());

        _blobFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_blobClient.Object);
        _blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback((string name, Stream _, Metadata? _, CancellationToken _) => _blobCalls.Add($"upload:{name}"))
            .Returns(Task.CompletedTask);
        _blobClient
            .Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, CancellationToken _) => _blobCalls.Add($"delete:{name}"))
            .Returns(Task.CompletedTask);

        return new UpdateCurrentUser.Handler(
            _userRepository.Object, _orderRepository.Object, _blobFactory.Object);
    }

    private static User UserWith(string? profilePhotoName)
    {
        var user = User.CreateWithPassword("user@cleansia.cz", "Password1", "First", "Last");
        user.Id = UserId;
        user.Update("First", "Last", "+420777111222");
        user.UpdateProfilePhotoName(profilePhotoName);
        return user;
    }

    private static UpdateCurrentUser.Command Save(BlobFileDto? photo = null, bool removePhoto = false) => new(
        Id: UserId,
        FirstName: "First",
        LastName: "Last",
        PhoneNumber: "+420777111222",
        BirthDate: null,
        Photo: photo,
        LanguageCode: "cs",
        RemovePhoto: removePhoto);

    private void VerifyNoDelete() =>
        _blobClient.Verify(
            c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    /// <summary>
    /// THE regression. Android's UpdateCurrentUserCommand has no photo field at all and iOS/web send
    /// it as null, so this is what literally every mobile profile save looks like on the wire.
    /// </summary>
    [Fact]
    public async Task SaveWithNoPhotoField_KeepsStoredPhoto_AndNeverDeletesTheBlob()
    {
        var user = UserWith(StoredPhotoName);

        var result = await CreateHandler(user).Handle(Save(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StoredPhotoName, user.ProfilePhotoName);
        VerifyNoDelete();
    }

    /// <summary>
    /// The partner web store defaults an unset photo to <c>BlobFileDto { fileName: '' }</c>. A blank
    /// FileName carries no removal intent — only RemovePhoto does — so this must be a no-op too.
    /// </summary>
    [Fact]
    public async Task SaveWithBlankFileNamePlaceholder_KeepsStoredPhoto_AndNeverDeletesTheBlob()
    {
        var user = UserWith(StoredPhotoName);
        var placeholder = new BlobFileDto(FileName: string.Empty, Base64Content: string.Empty, ContentType: string.Empty);

        await CreateHandler(user).Handle(Save(placeholder), CancellationToken.None);

        Assert.Equal(StoredPhotoName, user.ProfilePhotoName);
        VerifyNoDelete();
    }

    /// <summary>
    /// A replacement is stored under a NEW name. The old behaviour reused the stored name, which made
    /// the name non-content-addressed: every client caches its avatar bitmap on <c>fileName</c>, so a
    /// reused name renders the PREVIOUS image forever after a successful upload.
    /// </summary>
    [Fact]
    public async Task SaveWithNewImage_MintsAFreshBlobName_NeverReusingTheStoredOne()
    {
        var user = UserWith(StoredPhotoName);
        var newImage = new BlobFileDto(FileName: "avatar.png", Base64Content: NewImageBase64, ContentType: "image/png");

        await CreateHandler(user).Handle(Save(newImage), CancellationToken.None);

        Assert.NotEqual(StoredPhotoName, user.ProfilePhotoName);
        Assert.True(Guid.TryParse(user.ProfilePhotoName, out _));
        _blobClient.Verify(
            c => c.UploadAsync(
                user.ProfilePhotoName!, It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The other half of minting a fresh name: the superseded blob must still be deleted, or every
    /// replacement orphans one blob in the container forever.
    /// </summary>
    [Fact]
    public async Task SaveWithNewImage_DeletesTheSupersededBlob_SoAReplaceDoesNotOrphan()
    {
        var user = UserWith(StoredPhotoName);
        var newImage = new BlobFileDto(FileName: "avatar.png", Base64Content: NewImageBase64, ContentType: "image/png");

        await CreateHandler(user).Handle(Save(newImage), CancellationToken.None);

        _blobClient.Verify(c => c.DeleteAsync(StoredPhotoName, It.IsAny<CancellationToken>()), Times.Once);
        _blobClient.Verify(
            c => c.DeleteAsync(user.ProfilePhotoName!, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Ordering is the safety property: with distinct names the upload must land BEFORE the old blob is
    /// deleted, so a failed upload cannot leave the user with no avatar and a row pointing at a blob
    /// that no longer exists.
    /// </summary>
    [Fact]
    public async Task SaveWithNewImage_UploadsTheReplacementBeforeDeletingTheSuperseded()
    {
        var user = UserWith(StoredPhotoName);
        var newImage = new BlobFileDto(FileName: "avatar.png", Base64Content: NewImageBase64, ContentType: "image/png");

        await CreateHandler(user).Handle(Save(newImage), CancellationToken.None);

        Assert.Equal([$"upload:{user.ProfilePhotoName}", $"delete:{StoredPhotoName}"], _blobCalls);
    }

    [Fact]
    public async Task SaveWithNewImage_WhenTheUploadFails_LeavesTheStoredPhotoAndItsBlobIntact()
    {
        var user = UserWith(StoredPhotoName);
        var newImage = new BlobFileDto(FileName: "avatar.png", Base64Content: NewImageBase64, ContentType: "image/png");
        var handler = CreateHandler(user);
        _blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unreachable"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(Save(newImage), CancellationToken.None));

        Assert.Equal(StoredPhotoName, user.ProfilePhotoName);
        VerifyNoDelete();
    }

    [Fact]
    public async Task SaveWithNewImage_WhenNoStoredPhoto_UploadsUnderAFreshName()
    {
        var user = UserWith(null);
        var newImage = new BlobFileDto(FileName: "avatar.png", Base64Content: NewImageBase64, ContentType: "image/png");

        await CreateHandler(user).Handle(Save(newImage), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(user.ProfilePhotoName));
        _blobClient.Verify(
            c => c.UploadAsync(
                user.ProfilePhotoName!, It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoDelete();
    }

    [Fact]
    public async Task ExplicitRemoval_DeletesTheBlob_AndClearsTheColumn()
    {
        var user = UserWith(StoredPhotoName);

        await CreateHandler(user).Handle(Save(removePhoto: true), CancellationToken.None);

        Assert.Null(user.ProfilePhotoName);
        _blobClient.Verify(c => c.DeleteAsync(StoredPhotoName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplicitRemoval_WhenNoStoredPhoto_IsANoOp()
    {
        var user = UserWith(null);

        await CreateHandler(user).Handle(Save(removePhoto: true), CancellationToken.None);

        Assert.Null(user.ProfilePhotoName);
        VerifyNoDelete();
    }

    [Fact]
    public async Task SaveWithNoPhotoField_WhenNoStoredPhoto_TouchesNoBlobAtAll()
    {
        var user = UserWith(null);

        await CreateHandler(user).Handle(Save(), CancellationToken.None);

        Assert.Null(user.ProfilePhotoName);
        VerifyNoDelete();
        _blobClient.Verify(
            c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
