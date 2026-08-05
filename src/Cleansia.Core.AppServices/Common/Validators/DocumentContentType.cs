using Cleansia.Core.AppServices.Extensions;

namespace Cleansia.Core.AppServices.Common.Validators;

/// <summary>
/// The content type of an uploaded document, decided by its first bytes.
///
/// <para>One function answers both questions a document intake has — <i>may we accept this?</i> and
/// <i>what is it?</i> — because they are the same fact, and splitting them is how a path ends up
/// accepting on one basis and storing on another. A <c>null</c> result means neither: not a permitted
/// document, and no type to store.</para>
///
/// <para><b>The declared <see cref="Shared.DTOs.Files.BlobFileDto.ContentType"/> and the file extension
/// are both ignored.</b> Both are client strings, so neither can decide what a stored object is served
/// as; the extension is the weaker of the two, since it also survives a rename by anyone who later
/// touches the file name. Every type recorded from here on is therefore one of the five below, which is
/// what keeps <c>text/html</c> and <c>image/svg+xml</c> off a response header whatever a client sends.
/// Rows written before this existed still hold what their uploader claimed.</para>
///
/// <para><b>What this does not do.</b> A signature bounds the container format, not its contents: a ZIP
/// header says OOXML-or-any-other-zip, an OLE2 header says Office-compound-file-or-any-other, and both
/// admit a well-formed file carrying macros or an embedded payload. There is no malware scan on this
/// path. It refuses the classes that have no business here — markup, scripts, executables, arbitrary
/// binary — and it makes the stored type server-truth; it does not make the bytes safe to open.</para>
/// </summary>
internal static class DocumentContentType
{
    /// <summary>
    /// Base64 decodes in independent 4-character groups, so 12 characters yield the first 9 bytes
    /// without touching the rest of the payload — three more than the longest signature needs. Sniffing
    /// the head rather than the whole file is what lets this rule run before the full decode.
    /// </summary>
    private const int SniffedBase64Characters = 12;

    /// <summary>
    /// The set the three clients already promise: the web picker's accept list, and the message
    /// <c>file.type_not_allowed</c> carries in all five locales ("Accepted: PDF, JPEG, PNG, DOC, DOCX").
    /// A signature that maps to a type the clients never offer would accept uploads the UI says it will
    /// refuse; one missing from here refuses uploads the UI says it will take.
    /// </summary>
    private static readonly (byte[] Signature, string ContentType)[] Signatures =
    [
        ("%PDF-"u8.ToArray(), "application/pdf"),
        ([0xFF, 0xD8, 0xFF], "image/jpeg"),
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png"),
        ([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], "application/msword"),
        ([0x50, 0x4B, 0x03, 0x04], "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
    ];

    public static string? FromContent(string? base64Content)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            return null;
        }

        Span<byte> head = stackalloc byte[SniffedBase64Characters * 3 / 4];
        var sniffed = DecodeHead(base64Content.ExtractBase64Data(), head);

        foreach (var (signature, contentType) in Signatures)
        {
            if (sniffed.Length >= signature.Length && sniffed[..signature.Length].SequenceEqual(signature))
            {
                return contentType;
            }
        }

        return null;
    }

    private static ReadOnlySpan<byte> DecodeHead(string base64Data, Span<byte> destination)
    {
        // Whitespace is legal inside base64 and none of the three clients emits any, but a wrapped
        // payload would otherwise mis-align the 4-character groups and read as an unrecognised file.
        Span<char> prefix = stackalloc char[SniffedBase64Characters];
        var length = 0;

        foreach (var character in base64Data)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            prefix[length++] = character;

            if (length == SniffedBase64Characters)
            {
                break;
            }
        }

        var wholeGroups = length - (length % 4);

        return wholeGroups > 0 && Convert.TryFromBase64Chars(prefix[..wholeGroups], destination, out var written)
            ? destination[..written]
            : ReadOnlySpan<byte>.Empty;
    }
}
