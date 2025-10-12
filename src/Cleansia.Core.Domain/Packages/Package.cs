using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Packages;

public class Package : Auditable
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(500)]
    public string Description { get; set; }

    [Required]
    public decimal Price { get; set; }

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
}