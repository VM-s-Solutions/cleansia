using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common.Media;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// ADR-0043 D4 exempts the avatar, and this pins the exemption rather than leaving it to whoever reads
/// the roster next: the only fetcher of a stored avatar is its own subject, so scrubbing it discloses
/// nothing to nobody and buys a rewrite of every profile save for it.
///
/// <para><b>What this does NOT assert, said plainly:</b> that the exemption is still correct. It expires
/// the moment an avatar URL appears on a cross-user DTO — one line in <c>UserMappers</c> or
/// <c>EmployeeMappers</c>, neither of which this test can see. The change that adds one owes the scrub
/// and owes deleting this test in the same diff.</para>
/// </summary>
public class UpdateCurrentUserAvatarScrubExemptionTests
{
    private const string UserId = "user-avatar-scrub-1";

    [Fact]
    public async Task An_Avatar_Is_Stored_Exactly_As_It_Was_Sent()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var stored = await SaveAvatarAsync(photo);

        Assert.Equal(photo, stored);
        Assert.True(
            ByteSequence.Contains(stored, SyntheticJpeg.GpsCoordinateBytes()),
            "the avatar path scrubs. That is a decision with an audience argument behind it (ADR-0043 D4) "
            + "— if the audience changed, change the ADR and this test together.");
    }

    private static async Task<byte[]> SaveAvatarAsync(byte[] fileData)
    {
        var user = User.CreateWithPassword("avatar@cleansia.cz", "Password1", "First", "Last");
        user.Id = UserId;
        user.Update("First", "Last", "+420777111222");

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetOrdersByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(UserId);

        byte[]? uploaded = null;
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

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var handler = new UpdateCurrentUser.Handler(
            userRepository.Object, orderRepository.Object, session.Object, blobFactory.Object);

        var result = await handler.Handle(
            new UpdateCurrentUser.Command(
                Id: UserId,
                FirstName: "First",
                LastName: "Last",
                PhoneNumber: "+420777111222",
                BirthDate: null,
                Photo: new BlobFileDto("avatar.jpg", "data:image/jpeg;base64," + Convert.ToBase64String(fileData), "image/jpeg"),
                LanguageCode: "cs"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(uploaded);
        return uploaded!;
    }
}
