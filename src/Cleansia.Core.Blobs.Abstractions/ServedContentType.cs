namespace Cleansia.Core.Blobs.Abstractions;

/// <summary>
/// The content type a stored blob may be served back as, as a value rather than a string.
///
/// <para><b>Why a type and not a validated string.</b> Every recorded content type in this system is
/// ultimately something a client said: <c>UploadOrderPhoto</c> and <c>UploadDisputeEvidence</c> check
/// theirs against an allowlist, but <c>SaveOrderPhotos</c> reads it straight off the client's
/// <c>data:</c> URI prefix and stores it. Promoting a stored string onto the served <c>Content-Type</c>
/// header therefore hands an attacker the header — and <c>image/svg+xml</c> or <c>text/html</c> served
/// from the storage account is stored XSS on a host shared by every tenant. A validator on the write
/// path would fix that for the rows written after it; a closed set on the READ path fixes it for the
/// rows already there, which is the same argument that chose a SAS response-header override over
/// re-uploading every blob.</para>
///
/// <para>Unknown input resolves to <see cref="Opaque"/> rather than throwing: an unrecognised record
/// must not gain a capability, but it also must not break a cleaner's photo. <see cref="Opaque"/> is
/// what every one of these blobs is served as today, and browsers sniff images regardless of it, so
/// falling back costs rendering nothing.</para>
/// </summary>
public sealed class ServedContentType
{
    /// <summary>
    /// <c>application/octet-stream</c>. Not in the MIME-sniffing standard's sniffable set, so a browser
    /// navigating to one downloads it instead of parsing it as markup — which is why it is the floor.
    /// </summary>
    public static readonly ServedContentType Opaque = new("application/octet-stream");

    /// <summary>
    /// Inert by construction: a renderer for one of these cannot reach script or the DOM.
    /// <c>image/svg+xml</c> is absent on purpose — SVG is XML that can carry <c>&lt;script&gt;</c> and
    /// runs it with the serving origin, so it belongs with <c>text/html</c> and not with the images.
    /// </summary>
    private static readonly Dictionary<string, string> ServableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "image/jpeg",
        ["image/jpg"] = "image/jpeg",
        ["image/png"] = "image/png",
        ["image/webp"] = "image/webp",
        ["image/gif"] = "image/gif",
        ["application/pdf"] = "application/pdf",
    };

    private static readonly Dictionary<string, string> ServableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".pdf"] = "application/pdf",
    };

    public string Value { get; }

    private ServedContentType(string value) => Value = value;

    public static ServedContentType ForRecordedType(string? recordedContentType)
    {
        // A MIME parameter ("image/jpeg; charset=utf-8") is not part of the type, and carrying one
        // through would let a caller append arbitrary text to a header we are claiming to control.
        var normalized = recordedContentType?.Split(';')[0].Trim();

        return normalized is not null && ServableTypes.TryGetValue(normalized, out var served)
            ? new ServedContentType(served)
            : Opaque;
    }

    public static ServedContentType ForFileName(string? fileName)
    {
        var extension = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetExtension(fileName);

        return extension is not null && ServableExtensions.TryGetValue(extension, out var served)
            ? new ServedContentType(served)
            : Opaque;
    }
}
