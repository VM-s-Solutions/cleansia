using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.ReceiptTemplates;

public class ReceiptTemplate : Auditable
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; private set; } = default!;

    [Required]
    public string CountryId { get; private set; } = default!;
    public Country? Country { get; private set; }

    [Required]
    public string LanguageId { get; private set; } = default!;
    public Language? Language { get; private set; }

    [Required]
    public int Version { get; private set; }

    [Required]
    [MaxLength(500)]
    public string BlobUrl { get; private set; } = default!;

    public new bool IsActive { get; private set; }

    public DateTime? ActivatedAt { get; private set; }

    [MaxLength(1000)]
    public string? Description { get; private set; }

    public static ReceiptTemplate Create(
        string templateName,
        string countryId,
        string languageId,
        int version,
        string blobUrl,
        string? description = null)
    {
        return new ReceiptTemplate
        {
            TemplateName = templateName,
            CountryId = countryId,
            LanguageId = languageId,
            Version = version,
            BlobUrl = blobUrl,
            IsActive = false,
            Description = description
        };
    }

    public ReceiptTemplate Activate()
    {
        IsActive = true;
        ActivatedAt = DateTime.UtcNow;
        return this;
    }

    public ReceiptTemplate Deactivate()
    {
        IsActive = false;
        return this;
    }

    public ReceiptTemplate UpdateVersion(string newBlobUrl, int newVersion)
    {
        BlobUrl = newBlobUrl;
        Version = newVersion;
        return this;
    }

    public ReceiptTemplate UpdateDescription(string? description)
    {
        Description = description;
        return this;
    }

    public ReceiptTemplate UpdateTemplateName(string templateName)
    {
        TemplateName = templateName;
        return this;
    }
}
