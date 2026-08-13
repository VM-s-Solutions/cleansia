using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Blobs.Abstractions;

namespace Cleansia.Core.AppServices.Common.Validators;

/// <summary>The upload paths that decide a stored content type from the payload's own bytes.</summary>
internal enum UploadIntake
{
    Avatar,
    OrderPhoto,
    DisputeEvidence,
    EmployeeDocument
}

/// <summary>
/// The content type of an uploaded payload, decided by its first bytes. One function answers both
/// questions an intake has — may we accept this, and what is it — because they are the same fact;
/// <c>null</c> means neither.
///
/// <para><b>The declared ContentType and the file extension are both IGNORED.</b> Both are client
/// strings, so neither may decide what a stored object is served as. That is what keeps
/// <c>text/html</c> and <c>image/svg+xml</c> off a response header whatever a client sends.
/// <b>One table, many intakes</b> — a per-intake signature list drifts in both directions.
/// → /architecture/backend#content-sniffing</para>
/// </summary>
internal static class SniffedContentType
{
    /// <summary>
    /// WebP is the reason this is 12 and not 8: it is the only signature here with a fragment away from
    /// the start (<c>WEBP</c> at offset 8), and reading fewer bytes would make it indistinguishable from
    /// any other RIFF container. Base64 decodes in independent 4-character groups, so 16 characters yield
    /// exactly these 12 bytes without touching the rest of the payload — which is what lets the content
    /// rule run before the full decode.
    /// </summary>
    private const int SniffedBase64Characters = 16;

    private const int SniffedBytes = SniffedBase64Characters * 3 / 4;

    private readonly record struct Fragment(int Offset, byte[] Bytes);

    private sealed record FileSignature(string ContentType, string Extension, params Fragment[] Fragments);

    /// <summary>
    /// Every format any intake may store, with the extension the server mints for it. The extension is
    /// here rather than beside a call site because a blob whose name says one thing and whose bytes say
    /// another is the same drift as two signature tables, one step later.
    /// </summary>
    private static readonly FileSignature[] Signatures =
    [
        new("application/pdf", ".pdf", new Fragment(0, "%PDF-"u8.ToArray())),
        new("image/jpeg", ".jpg", new Fragment(0, [0xFF, 0xD8, 0xFF])),
        new("image/png", ".png", new Fragment(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])),
        new("image/gif", ".gif", new Fragment(0, "GIF8"u8.ToArray())),
        new("image/webp", ".webp", new Fragment(0, "RIFF"u8.ToArray()), new Fragment(8, "WEBP"u8.ToArray())),
        new("application/msword", ".doc", new Fragment(0, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])),
        new(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".docx",
            new Fragment(0, [0x50, 0x4B, 0x03, 0x04]))
    ];

    /// <summary>
    /// What each intake's clients already promise. A signature accepted here that the client never offers
    /// admits an upload the UI says it will refuse; one missing refuses an upload the UI invited. The web
    /// pickers and the five-locale <c>file.type_not_allowed</c> string ("Accepted: PDF, JPEG, PNG, DOC,
    /// DOCX") are the promise for documents; <c>AVATAR_ALLOWED_CONTENT_TYPES</c>,
    /// <c>PHOTO_ALLOWED_TYPES</c> and <c>DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES</c> are the promise for
    /// the other three.
    /// </summary>
    private static readonly Dictionary<UploadIntake, IReadOnlySet<string>> AcceptedByIntake = new()
    {
        [UploadIntake.Avatar] = new HashSet<string> { "image/jpeg", "image/png", "image/gif", "image/webp" },
        [UploadIntake.OrderPhoto] = new HashSet<string> { "image/jpeg", "image/png", "image/webp" },
        [UploadIntake.DisputeEvidence] = new HashSet<string>
        {
            "image/jpeg", "image/png", "image/webp", "application/pdf"
        },
        [UploadIntake.EmployeeDocument] = new HashSet<string>
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        }
    };

    public static string? FromContent(string? base64Content, UploadIntake intake)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            return null;
        }

        Span<byte> head = stackalloc byte[SniffedBytes];

        return Match(DecodeHead(base64Content.ExtractBase64Data(), head), intake);
    }

    public static string? FromContent(ReadOnlySpan<byte> content, UploadIntake intake) => Match(content, intake);

    /// <summary>
    /// The same question asked of a blob already in storage, for the rows written before the intake asked
    /// it — a write-path rule retypes nothing that is already there, and every one of those rows holds a
    /// string its uploader chose. An unrecognised payload falls back to
    /// <see cref="ServedContentType.Opaque"/> rather than being refused: it must not gain a capability,
    /// but an already-accepted document must not become undownloadable either.
    /// </summary>
    public static string ForDownload(ReadOnlySpan<byte> content, UploadIntake intake) =>
        Match(content, intake) ?? ServedContentType.Opaque.Value;

    /// <summary>
    /// The extension the server mints for a type this table produced. Unknown input yields no extension
    /// rather than a guessed one — a nameless blob is recoverable, a mislabelled one is not.
    /// </summary>
    public static string ExtensionFor(string? contentType) =>
        Signatures.FirstOrDefault(signature => signature.ContentType == contentType)?.Extension ?? string.Empty;

    private static string? Match(ReadOnlySpan<byte> content, UploadIntake intake)
    {
        var accepted = AcceptedByIntake[intake];

        foreach (var signature in Signatures)
        {
            if (accepted.Contains(signature.ContentType) && Matches(content, signature.Fragments))
            {
                return signature.ContentType;
            }
        }

        return null;
    }

    private static bool Matches(ReadOnlySpan<byte> content, Fragment[] fragments)
    {
        foreach (var (offset, bytes) in fragments)
        {
            if (content.Length < offset + bytes.Length || !content.Slice(offset, bytes.Length).SequenceEqual(bytes))
            {
                return false;
            }
        }

        return true;
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
