using Cleansia.Core.Blobs.Abstractions;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// The closed set of content types a stored blob may be SERVED as.
///
/// <para><b>This type exists because of what the obvious fix would have done.</b> Five
/// <c>MetadataName</c> constants were named as if they set blob HTTP headers and in fact went to
/// <c>x-ms-meta-*</c>, which Azure never serves from — so every order photo and dispute-evidence file
/// has always been served <c>application/octet-stream</c>, and browsers sniff, and nobody noticed.
/// "Wire the constants up to the real headers" is the natural fix and it is the dangerous one:
/// <c>SaveOrderPhotos</c> takes the content type from the client's own <c>data:</c> URI prefix with no
/// allowlist anywhere on the path, so the value it would have promoted onto the wire is an attacker
/// string. <c>data:image/svg+xml;…</c> or <c>data:text/html;…</c> served with that header, from a
/// storage host shared by every tenant, is stored XSS — and <c>application/octet-stream</c>, the bug,
/// is what has been preventing it.</para>
///
/// <para>So the served type is not a string that is validated; it is a value that can only be one of a
/// closed set. A recorded type that is not in the set resolves to <see cref="ServedContentType.Opaque"/>
/// — today's behaviour, and the safe one — rather than being rejected, because a cleaner's photo must
/// keep rendering while a malformed record must not gain a capability.</para>
/// </summary>
public class ServedContentTypeTests
{
    [Theory]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData("image/jpg", "image/jpeg")]
    [InlineData("IMAGE/PNG", "image/png")]
    [InlineData("  image/webp  ", "image/webp")]
    [InlineData("image/gif", "image/gif")]
    [InlineData("application/pdf", "application/pdf")]
    // A MIME parameter is not part of the type, and leaving it on would make the closed set open again.
    [InlineData("image/jpeg; charset=utf-8", "image/jpeg")]
    public void RecordedType_OnTheSet_IsServedAsItself(string recorded, string expected) =>
        Assert.Equal(expected, ServedContentType.ForRecordedType(recorded).Value);

    /// <summary>
    /// The whole point. Each of these is a real value <c>SaveOrderPhotos.DetermineContentType</c> would
    /// have accepted straight off the client's data URI.
    /// </summary>
    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xhtml+xml")]
    [InlineData("application/javascript")]
    [InlineData("text/xml")]
    // SVG is an image AND a script host: navigate to one and its <script> runs with the origin of the
    // storage account. It is excluded deliberately, not by oversight — an "images are safe" allowlist
    // that includes it is the same vulnerability with extra steps.
    [InlineData("image/svg+xml")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nonsense")]
    public void RecordedType_OffTheSet_IsServedOpaquely(string? recorded) =>
        Assert.Equal("application/octet-stream", ServedContentType.ForRecordedType(recorded).Value);

    [Theory]
    [InlineData("receipt.JPG", "image/jpeg")]
    [InlineData("before.jpeg", "image/jpeg")]
    [InlineData("after.png", "image/png")]
    [InlineData("shot.webp", "image/webp")]
    [InlineData("invoice.pdf", "application/pdf")]
    public void FileName_WithAKnownExtension_ResolvesThroughTheSameSet(string fileName, string expected) =>
        Assert.Equal(expected, ServedContentType.ForFileName(fileName).Value);

    [Theory]
    [InlineData("payload.svg")]
    [InlineData("payload.html")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void FileName_WithoutAKnownExtension_IsServedOpaquely(string? fileName) =>
        Assert.Equal("application/octet-stream", ServedContentType.ForFileName(fileName).Value);

    /// <summary>
    /// The property that makes this a type rather than a validator: there is no path from a caller's
    /// string to a served header except through the factories above. If a public constructor or an
    /// implicit conversion is ever added, the guarantee is gone and this reddens.
    /// </summary>
    [Fact]
    public void NoPublicConstructorOrConversionLetsACallerNameItsOwnType()
    {
        Assert.Empty(typeof(ServedContentType).GetConstructors());
        Assert.DoesNotContain(
            typeof(ServedContentType).GetMethods(),
            m => m.Name is "op_Implicit" or "op_Explicit");
    }
}
