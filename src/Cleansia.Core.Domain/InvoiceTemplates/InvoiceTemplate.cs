using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.InvoiceTemplates;

public class InvoiceTemplate : Auditable
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; private set; }

    [Required]
    public string CountryId { get; private set; }
    public Country? Country { get; private set; }

    [Required]
    public string LanguageId { get; private set; }
    public Language? Language { get; private set; }

    [Required]
    public int Version { get; private set; }

    [Required]
    [MaxLength(500)]
    public string BlobUrl { get; private set; }

    [Required]
    public bool IsActive { get; private set; }

    public DateTime? ActivatedAt { get; private set; }

    [MaxLength(1000)]
    public string? Description { get; private set; }

    public static InvoiceTemplate Create(
        string templateName,
        string countryId,
        string languageId,
        int version,
        string blobUrl,
        string? description = null)
    {
        return new InvoiceTemplate
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

    public InvoiceTemplate Activate()
    {
        IsActive = true;
        ActivatedAt = DateTime.UtcNow;
        return this;
    }

    public InvoiceTemplate Deactivate()
    {
        IsActive = false;
        return this;
    }

    public InvoiceTemplate UpdateVersion(string newBlobUrl, int newVersion)
    {
        BlobUrl = newBlobUrl;
        Version = newVersion;
        return this;
    }

    public InvoiceTemplate UpdateDescription(string? description)
    {
        Description = description;
        return this;
    }
}
