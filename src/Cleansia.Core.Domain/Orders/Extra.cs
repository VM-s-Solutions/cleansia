using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// Add-on cleaning task on top of the base selection. Stable platform-wide slug for client lookups,
/// translations dictionary, soft-delete via <c>IsActive</c>.
///
/// <para><b>Flat price per selection regardless of quantity</b>, and authored in the default currency —
/// conversion happens in the pricing calculator alongside services and packages.
/// → /product/business-rules</para>
/// </summary>
public class Extra : Auditable
{
    // Stable identifier for clients (mobile icon/label map, analytics, deep-links).
    // Immutable after creation — renaming Name is fine, renaming Slug breaks clients.
    [Required]
    [MaxLength(50)]
    public string Slug { get; private set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; private set; }

    [Required]
    public decimal Price { get; private set; }

    public int DisplayOrder { get; private set; }

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    public static Extra Create(string slug, string name, string? description, decimal price, int displayOrder = 0) => new()
    {
        Slug = slug,
        Name = name,
        Description = description,
        Price = price,
        DisplayOrder = displayOrder,
    };

    public Extra Update(string name, string? description, decimal price, int displayOrder)
    {
        Name = name;
        Description = description;
        Price = price;
        DisplayOrder = displayOrder;
        return this;
    }

    public Extra SetTranslation(string languageCode, string name, string? description)
    {
        _translations[languageCode] = new Translation { Name = name, Description = description ?? string.Empty };
        return this;
    }

    public Extra RemoveTranslation(string languageCode)
    {
        _translations.Remove(languageCode);
        return this;
    }

    public Extra ClearTranslations()
    {
        _translations = new Dictionary<string, Translation>();
        return this;
    }
}
