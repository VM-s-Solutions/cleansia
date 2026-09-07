using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common.Media;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// The avatar is its own edit.
///
/// <para>It used to travel as a full <see cref="UpdateCurrentUser.Command"/>, because that was the
/// only endpoint that could move one — so a picture upload was validated as a profile save. Owner,
/// 2026-09-03: uploading a photo on an account with no phone number failed. Two things were wrong
/// and both are pinned here: the phone uniqueness rule ran on an empty value (EF compiles a null
/// parameter to <c>IS NULL</c>, so the account matched the first OTHER account without a phone),
/// and a photo change had no reason to be carrying a phone number in the first place.</para>
/// </summary>
public class UpdateCurrentUserPhotoTests
{
    private const string UserId = "user-photo-1";

    private static User BuildUser(string? phoneNumber)
    {
        var user = User.CreateWithPassword("photo@cleansia.cz", "Password1", "First", "Last");
        user.Id = UserId;
        user.Update("First", "Last", phoneNumber);
        return user;
    }

    private static (UpdateCurrentUserPhoto.Handler Handler, Func<byte[]?> Uploaded, Func<string?> Deleted)
        BuildHandler(User user)
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(UserId);

        byte[]? uploaded = null;
        string? deleted = null;

        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Metadata?, CancellationToken>((_, stream, _, _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                uploaded = copy.ToArray();
            })
            .Returns(Task.CompletedTask);
        blobClient
            .Setup(c => c.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => deleted = name)
            .Returns(Task.FromResult(true));

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        return (
            new UpdateCurrentUserPhoto.Handler(userRepository.Object, session.Object, blobFactory.Object),
            () => uploaded,
            () => deleted);
    }

    private static BlobFileDto Jpeg() =>
        new("avatar.jpg", "data:image/jpeg;base64," + Convert.ToBase64String(SyntheticJpeg.Photo()), "image/jpeg");

    /// <summary>
    /// The reported bug, at the level it actually broke: an account with no phone number can change
    /// its picture. The command has no phone field at all, so no rule about one can reach it.
    /// </summary>
    [Fact]
    public async Task An_Account_With_No_Phone_Number_Can_Change_Its_Photo()
    {
        var user = BuildUser(phoneNumber: null);
        var (handler, uploaded, _) = BuildHandler(user);

        var result = await handler.Handle(
            new UpdateCurrentUserPhoto.Command(Jpeg(), RemovePhoto: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(uploaded());
        Assert.False(string.IsNullOrWhiteSpace(user.ProfilePhotoName));
    }

    /// <summary>
    /// The point of splitting the command: the fields a profile save owns are not reachable from
    /// here, so a photo change cannot rewrite them by accident.
    /// </summary>
    [Fact]
    public async Task Changing_The_Photo_Leaves_The_Rest_Of_The_Profile_Alone()
    {
        var user = BuildUser(phoneNumber: "+420777111222");
        var (handler, _, _) = BuildHandler(user);

        await handler.Handle(
            new UpdateCurrentUserPhoto.Command(Jpeg(), RemovePhoto: false), CancellationToken.None);

        Assert.Equal("First", user.FirstName);
        Assert.Equal("Last", user.LastName);
        Assert.Equal("+420777111222", user.PhoneNumber);
    }

    /// <summary>
    /// Removal clears the name and deletes the blob that was there.
    /// </summary>
    [Fact]
    public async Task Removing_The_Photo_Clears_The_Name_And_Deletes_The_Blob()
    {
        var user = BuildUser(phoneNumber: null);
        user.UpdateProfilePhotoName("existing-blob");
        var (handler, _, deleted) = BuildHandler(user);

        var result = await handler.Handle(
            new UpdateCurrentUserPhoto.Command(Photo: null, RemovePhoto: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(user.ProfilePhotoName);
        Assert.Equal("existing-blob", deleted());
    }

    /// <summary>
    /// A request that says nothing is rejected rather than silently doing nothing — otherwise the
    /// client gets a 200 for an edit that never happened.
    /// </summary>
    [Fact]
    public void A_Request_With_Neither_A_Photo_Nor_A_Removal_Is_Invalid()
    {
        var result = new UpdateCurrentUserPhoto.Validator()
            .Validate(new UpdateCurrentUserPhoto.Command(Photo: null, RemovePhoto: false));

        Assert.False(result.IsValid);
    }
}
