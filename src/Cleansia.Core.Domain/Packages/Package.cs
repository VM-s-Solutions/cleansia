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

    [Required]
    public decimal Price { get; private set; }

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<PackageService> _includedServices = [];
    public IReadOnlyCollection<PackageService> IncludedServices => _includedServices.ToList().AsReadOnly();

    public static Package Create(string name, string description, decimal price) => new()
    {
        Name = name,
        Description = description,
        Price = price
    };

    public Package Update(string name, string description, decimal price)
    {
        Name = name;
        Description = description;
        Price = price;
        return this;
    }

    public Package SetTranslation(string languageCode, string name, string description)
    {
        _translations[languageCode] = new Translation { Name = name, Description = description };
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