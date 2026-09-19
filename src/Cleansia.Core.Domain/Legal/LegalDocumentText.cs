using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Legal;

/// <summary>
/// One language of one <see cref="LegalDocument"/>. The hash is what the seeder compares and what the
/// admin sees beside a version, so a wording that drifted from its seed file is visible without a diff.
/// </summary>
public class LegalDocumentText : BaseEntity
{
    [Required]
    [MaxLength(26)]
    public string LegalDocumentId { get; private set; } = default!;

    public LegalDocument? Document { get; private set; }

    [Required]
    [MaxLength(2)]
    public string Language { get; private set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Title { get; private set; } = default!;

    [Required]
    public string ContentMarkdown { get; private set; } = default!;

    [Required]
    [MaxLength(64)]
    public string ContentHash { get; private set; } = default!;

    internal static LegalDocumentText Create(LegalDocument document, string language, string title, string contentMarkdown)
        => new()
        {
            LegalDocumentId = document.Id,
            Document = document,
            Language = language.ToLowerInvariant(),
            Title = title,
            ContentMarkdown = contentMarkdown,
            ContentHash = HashOf(contentMarkdown)
        };

    /// <summary>Only for a document not yet in force — the seeder guards that, the entity does not know the date.</summary>
    public void Replace(string title, string contentMarkdown)
    {
        Title = title;
        ContentMarkdown = contentMarkdown;
        ContentHash = HashOf(contentMarkdown);
    }

    public bool Matches(string title, string contentMarkdown) =>
        Title == title && ContentHash == HashOf(contentMarkdown);

    public static string HashOf(string contentMarkdown) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contentMarkdown)));
}
