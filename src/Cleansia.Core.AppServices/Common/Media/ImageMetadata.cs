namespace Cleansia.Core.AppServices.Common.Media;

/// <summary>
/// What reached storage, and whether a walker claimed it. <see cref="Scrubbed"/> is false for a container
/// no walker recognises and for one whose structure the walk refused — in both cases <see cref="Bytes"/>
/// is the input, unchanged, and the guarantee this type carries is deliberately absent rather than
/// silently assumed.
/// </summary>
public readonly record struct ScrubbedImage(byte[] Bytes, bool Scrubbed);

/// <summary>
/// The metadata scrub: capture coordinates, device identity, author names and revision history are
/// removed from an uploaded image at intake, before it reaches the blob client (ADR-0043 D2/D3). It is a
/// scrub and not a sanitizer — it removes the metadata containers a camera and an editor write, and it
/// makes no claim about an ICC profile, a JPEG comment or anything embedded in the image data itself.
///
/// <para><b>Nothing here decodes.</b> Each format is walked as the length-prefixed container it is and
/// re-emitted, so a bounded upload stays a bounded allocation. A decoder would turn a 300 KB PNG into
/// gigabytes of bitmap on a request path, chosen by the uploader.</para>
///
/// <para><b>The walker is chosen by the bytes in hand</b>, at the moment this runs — never by a declared
/// content type, never by a persisted one, not even a correct one. A per-format transform dispatching on
/// a caller's string is a control whose no-op branch its own uploader selects: declare
/// <c>data:image/png</c>, send JPEG, and the PNG walker finds no <c>IHDR</c>, bails, and the coordinates
/// survive under a green test. That is why this signature has no content type on it, and why an
/// unrecognised payload is reported <i>not scrubbed</i> rather than passed off as scrubbed.</para>
///
/// <para><b>Not every surface calls this.</b> It runs where the audience is not the uploader — order
/// photos and dispute evidence. The avatar is exempt by a recorded decision that expires the day an
/// avatar URL appears on a cross-user DTO; PDF and Office documents are excluded because an object-graph
/// rewrite is the thing the no-decoder ruling refuses.</para>
/// </summary>
public static class ImageMetadata
{
    public static ScrubbedImage Scrub(byte[] content)
    {
        var rewritten = Rewrite(content);

        return rewritten is null ? new ScrubbedImage(content, false) : new ScrubbedImage(rewritten, true);
    }

    private static byte[]? Rewrite(byte[] content)
    {
        if (JpegMetadata.Identifies(content))
        {
            return JpegMetadata.WithoutMetadata(content);
        }

        if (PngMetadata.Identifies(content))
        {
            return PngMetadata.WithoutMetadata(content);
        }

        // GIF has no EXIF and its only metadata container is a comment extension no camera writes, so it
        // joins the formats no walker claims rather than earning one.
        return WebPMetadata.Identifies(content) ? WebPMetadata.WithoutMetadata(content) : null;
    }
}
