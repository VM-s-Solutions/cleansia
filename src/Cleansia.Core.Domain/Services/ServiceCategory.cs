using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Services;

public class ServiceCategory : Auditable, ITenantEntity
{
    // Stable identifier for clients (mobile icon/color map, analytics, deep-links).
    // Immutable after creation — renaming Name is fine, renaming Slug breaks clients.
    [Required]
    [MaxLength(50)]
    public string Slug { get; private set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; private set; }

    [MaxLength(500)]
    public string Description { get; private set; }

    public int DisplayOrder { get; private set; }

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<Service> _services = [];
    public IReadOnlyCollection<Service> Services => _services.ToList().AsReadOnly();

    public static ServiceCategory Create(string slug, string name, string description, int displayOrder = 0) => new()
    {
        Slug = slug,
        Name = name,
        Description = description,
        DisplayOrder = displayOrder
    };

    public ServiceCategory Update(string name, string description, int displayOrder)
    {
        Name = name;
        Description = description;
        DisplayOrder = displayOrder;
        return this;
    }

    public ServiceCategory SetTranslation(string languageCode, string name, string description)
    {
        _translations[languageCode] = new Translation { Name = name, Description = description };
        return this;
    }

    public ServiceCategory RemoveTranslation(string languageCode)
    {
        _translations.Remove(languageCode);
        return this;
    }

    public ServiceCategory ClearTranslations()
    {
        _translations = new Dictionary<string, Translation>();
        return this;
    }
}
