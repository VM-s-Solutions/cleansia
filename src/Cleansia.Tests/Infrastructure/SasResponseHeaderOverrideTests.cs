using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Infra.Azure.Storage.Blobs;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// What a reader actually receives, pinned on the SAS token itself.
///
/// <para>Nothing on the write path ever set real blob HTTP headers — the five <c>MetadataName</c>
/// constants that looked like they did were routed into <c>SetMetadataAsync</c>, i.e.
/// <c>x-ms-meta-*</c>, which the storage service never serves from. So every order photo, every
/// dispute-evidence file and every avatar in storage carries <c>Content-Type:
/// application/octet-stream</c>, and the defect survived three shipped pipelines precisely because
/// nothing asserted the served header.</para>
///
/// <para><b>Why the token and not a fetch.</b> <c>rsct</c>/<c>rscc</c> are properties of the read token,
/// not of the blob: the storage service returns them for any request bearing this signature, whatever
/// the blob was stored with. That is exactly what makes the fix retroactive over blobs written years
/// ago with no backfill — and it means the assertion belongs on the minted token, where it holds
/// without a storage account. Mirrors <see cref="ProfilePhotoSasGrantScopeTests"/>, which pins the grant
/// scope of the same mint the same way.</para>
/// </summary>
public class SasResponseHeaderOverrideTests
{
    private const string DevelopmentStorage = "UseDevelopmentStorage=true";
    private const string BlobName = "2024/order-1/order-1_Before_20240101120000_ab12cd34.jpg";

    private static Uri Mint(ServedContentType servedAs) =>
        new BlobContainerClient(DevelopmentStorage, Constants.BlobContainers.OrderPhotos)
            .GenerateSasUri(BlobName, TimeSpan.FromHours(1), servedAs);

    /// <summary>
    /// AC6 — remove the <c>ContentType</c> assignment from the mint and this is what goes red. The
    /// blob's own stored type is irrelevant to the assertion, which is the property being claimed.
    /// </summary>
    [Fact]
    public void MintedToken_PinsTheServedContentType()
    {
        var parameters = QueryOf(Mint(ServedContentType.ForRecordedType("image/jpeg")));

        Assert.Equal("image/jpeg", parameters["rsct"]);
    }

    /// <summary>
    /// The trap this ticket was most likely to ship. <c>Metadata.CacheMetadata</c> hardcoded
    /// <c>"public, max-age=31536000"</c> and the AVATAR used it; it was inert only because it went to
    /// custom metadata. Mapping the old constants onto real headers — the obvious fix — would have
    /// activated <c>Cache-Control: public</c> on a private image reachable only by signature, so an
    /// intermediary could retain it past the token that authorised it. The directive is set on the mint
    /// itself and takes no parameter, so no call site can pass the wrong one.
    /// </summary>
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("application/pdf")]
    [InlineData("nonsense-falls-back-to-opaque")]
    public void MintedToken_AlwaysSaysPrivate_NeverPublic(string recordedContentType)
    {
        var parameters = QueryOf(Mint(ServedContentType.ForRecordedType(recordedContentType)));

        Assert.Equal("private, max-age=3600", parameters["rscc"]);
        Assert.DoesNotContain("public", parameters["rscc"]);
    }

    /// <summary>The avatar takes the opaque overload — it too must never be cached publicly.</summary>
    [Fact]
    public void OpaqueOverload_StillSaysPrivate()
    {
        var uri = new BlobContainerClient(DevelopmentStorage, Constants.BlobContainers.UserFiles)
            .GenerateSasUri("0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b", TimeSpan.FromHours(1));

        var parameters = QueryOf(uri);

        Assert.Equal("private, max-age=3600", parameters["rscc"]);
        Assert.Equal("application/octet-stream", parameters["rsct"]);
    }

    /// <summary>
    /// The override rides INSIDE the signature. A reader cannot edit <c>rsct</c> on a URL they were
    /// handed to get a type of their choosing back — which is the other half of why the served type has
    /// to be a closed set on our side: nobody else can change it, so we had better not put a caller's
    /// string in it ourselves.
    /// </summary>
    [Fact]
    public void ChangingTheServedType_ChangesTheSignature()
    {
        var jpeg = QueryOf(Mint(ServedContentType.ForRecordedType("image/jpeg")));
        var opaque = QueryOf(Mint(ServedContentType.Opaque));

        Assert.NotEqual(jpeg["sig"], opaque["sig"]);
    }

    /// <summary>
    /// AC4 — the grant scope is untouched. <c>ProfilePhotoSasGrantScopeTests</c> asserts this for the
    /// avatar container and must keep passing unmodified; this repeats it on the container the override
    /// was added for, so "we added response headers" and "we widened the grant" cannot be confused.
    /// </summary>
    [Fact]
    public void TheGrantIsStillReadOnOneBlob()
    {
        var parameters = QueryOf(Mint(ServedContentType.ForRecordedType("image/png")));

        Assert.Equal("b", parameters["sr"]);
        Assert.Equal("r", parameters["sp"]);
    }

    // The SDK form-encodes the query, so a space arrives as '+' — decode it before comparing, or the
    // Cache-Control assertion reads "private,+max-age=3600" and tells you nothing about the directive.
    private static Dictionary<string, string> QueryOf(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => Uri.UnescapeDataString((parts.Length > 1 ? parts[1] : string.Empty).Replace('+', ' ')));
}
