#nullable enable
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.Users;

/// <summary>
/// Moving a customer's avatar blob, for the two commands that can do it:
/// <see cref="UpdateCurrentUserPhoto"/> (the picture on its own) and
/// <see cref="UpdateCurrentUser"/> (a profile save that happens to carry one).
///
/// <para>Extracted when the second caller appeared, not before. The ordering here is the part that
/// must not drift between them: upload BEFORE deleting, so a failed upload cannot destroy the
/// avatar the customer still has, and a fresh name per upload because clients cache their bitmap on
/// the stored name — reusing it would render the previous image forever, and an outstanding SAS
/// keeps resolving to the image it was issued for.</para>
/// </summary>
internal static class ProfilePhotoUpdater
{
    /// <summary>
    /// Applies a photo change to <paramref name="user"/>. A call that neither carries an image nor
    /// asks for removal says nothing about the avatar, so it leaves it alone — that is the shape of
    /// every ordinary profile save.
    /// </summary>
    public static async Task ApplyAsync(
        User user,
        BlobFileDto? photo,
        bool removePhoto,
        IBlobContainerClientFactory clientFactory,
        CancellationToken cancellationToken)
    {
        var hasNewPhoto = !string.IsNullOrWhiteSpace(photo?.Base64Content);

        if (!hasNewPhoto && !removePhoto)
        {
            return;
        }

        var supersededPhotoName = user.ProfilePhotoName;
        var client = clientFactory.GetBlobContainerClient(Constants.BlobContainers.UserFiles);

        if (hasNewPhoto)
        {
            var fileName = Guid.NewGuid().ToString();
            await UploadAsync(client, fileName, photo!.Base64Content!, cancellationToken);
            user.UpdateProfilePhotoName(fileName);
        }
        else
        {
            user.UpdateProfilePhotoName(null);
        }

        if (!string.IsNullOrWhiteSpace(supersededPhotoName))
        {
            await client.DeleteAsync(supersededPhotoName, cancellationToken);
        }
    }

    private static async Task UploadAsync(
        IBlobContainerClient client,
        string fileName,
        string base64Content,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(Convert.FromBase64String(base64Content.ExtractBase64Data()));
        await client.UploadAsync(fileName, stream, cancellationToken: cancellationToken);
    }
}
