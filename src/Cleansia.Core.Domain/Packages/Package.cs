using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Packages;

public class Package : Auditable
{
    [Required]
    [MaxLength(100)]
    public string Name { get; private set; }

    [MaxLength(500)]
    public string Description { get; private set; }

    /// <summary>
    /// The short line the package card leads with — "For a well-kept flat", not a sentence.
    /// Separate from <see cref="Description"/>, which is prose and too long for that slot.
    /// </summary>
    [MaxLength(60)]
    public string? Tagline { get; private set; }

    /// <summary>
    /// Draws the card as the featured one. No invariant caps this at a single package: the flag
    /// is the admin's editorial choice and the catalogue features every package carrying it, so
    /// flagging three highlights three. A cross-row "only one" rule would mean writing other
    /// packages' rows on every save, which is a concurrency problem in exchange for a policy
    /// nobody has asked for.
    /// </summary>
    public bool IsPopular { get; private set; }

    [Required]

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<PackageService> _includedServices = [];
    public IReadOnlyCollection<PackageService> IncludedServices => _includedServices.ToList().AsReadOnly();

    public static Package Create(
        string name,
        string description,
        string? tagline = null,
        bool isPopular = false) => new()
    {
        Name = name,
        Description = description,
        Tagline = tagline,
        IsPopular = isPopular
    };

    public Package Update(
        string name,
        string description,
        string? tagline = null,
        bool isPopular = false)
    {
        Name = name;
        Description = description;
        Tagline = tagline;
        IsPopular = isPopular;
        return this;
    }

    public Package SetTranslation(string languageCode, string name, string description, string? tagline = null)
    {
        _translations[languageCode] = new Translation
        {
            Name = name,
            Description = description,
            Tagline = tagline
        };
        return this;
    }

    public Package RemoveTranslation(string languageCode)
    {
        _translations.Remove(languageCode);
        return this;
    }

    public Package ClearTranslations()
    {
        _translations = new Dictionary<string, Translation>();
        return this;
    }

    public Package AddService(Service service)
    {
        if (!_includedServices.Any(ps => ps.ServiceId == service.Id))
        {
            _includedServices.Add(PackageService.Create(this, service));
        }
        return this;
    }

    public Package RemoveService(string serviceId)
    {
        var packageService = _includedServices.FirstOrDefault(ps => ps.ServiceId == serviceId);
        if (packageService != null)
        {
            _includedServices.Remove(packageService);
        }
        return this;
    }

    public Package ClearServices()
    {
        _includedServices = new List<PackageService>();
        return this;
    }
}