using Cleansia.Core.AppServices.Common;
using Cleansia.Infra.Azure.Storage.Blobs;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// AC3 — the exposure the avatar read path actually hands out. Drives the REAL blob client (the SAS is
/// computed locally from the shared key, no storage round-trip) and asserts the token grants read on ONE
/// blob for ONE hour: it cannot be widened to list the container, cannot write, and cannot outlive its
/// window. If <c>GenerateSasUri</c> is ever changed to sign a container (<c>sr=c</c>) or to add write /
/// delete / list permissions, this goes red.
/// </summary>
public class ProfilePhotoSasGrantScopeTests
{
    private const string DevelopmentStorage = "UseDevelopmentStorage=true";
    private const string BlobName = "0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b";

    private static Uri GenerateAvatarSas(TimeSpan lifetime) =>
        new BlobContainerClient(DevelopmentStorage, Constants.BlobContainers.UserFiles)
            .GenerateSasUri(BlobName, lifetime);

    [Fact]
    public void AvatarSas_GrantsReadOnASingleBlob_NotTheContainer()
    {
        var parameters = QueryOf(GenerateAvatarSas(TimeSpan.FromHours(1)));

        Assert.Equal("b", parameters["sr"]);
        Assert.Equal("r", parameters["sp"]);
        Assert.False(string.IsNullOrWhiteSpace(parameters["sig"]));
    }

    [Fact]
    public void AvatarSas_TargetsTheStoredBlobNameInsideTheUserFilesContainer()
    {
        var uri = GenerateAvatarSas(TimeSpan.FromHours(1));

        Assert.EndsWith($"/{Constants.BlobContainers.UserFiles}/{BlobName}", uri.AbsolutePath);
    }

    [Fact]
    public void AvatarSas_ExpiresWithinTheRequestedWindow()
    {
        var before = DateTimeOffset.UtcNow;

        var parameters = QueryOf(GenerateAvatarSas(TimeSpan.FromHours(1)));

        var expiry = DateTimeOffset.Parse(parameters["se"]!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.InRange(expiry, before.AddMinutes(59), before.AddMinutes(61));
    }

    private static Dictionary<string, string> QueryOf(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : string.Empty));
}
