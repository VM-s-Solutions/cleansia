using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Services;

public class Service : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; private set; }

    [MaxLength(500)]
    public string Description { get; private set; }

    [Required]
    public decimal BasePrice { get; private set; }

    public decimal PerRoomPrice { get; private set; }

    public int EstimatedTime { get; private set; }

    [Required]
    public string CategoryId { get; private set; }
    public ServiceCategory? Category { get; private set; }

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<PackageService> _packages = [];
    public IReadOnlyCollection<PackageService> Packages => _packages.ToList().AsReadOnly();

    private ICollection<OrderService> _includedInOrders = [];
    public IReadOnlyCollection<OrderService> IncludedInOrders => _includedInOrders.ToList().AsReadOnly();

    public static Service Create(string categoryId, string name, string description, decimal basePrice, decimal perRoomPrice, int estimatedTime = 0) => new()
    {
        CategoryId = categoryId,
        Name = name,
        Description = description,
        BasePrice = basePrice,
        PerRoomPrice = perRoomPrice,
        EstimatedTime = estimatedTime
    };

    public Service Update(string categoryId, string name, string description, decimal basePrice, decimal perRoomPrice, int estimatedTime)
    {
        CategoryId = categoryId;
        Name = name;
        Description = description;
        BasePrice = basePrice;
        PerRoomPrice = perRoomPrice;
        EstimatedTime = estimatedTime;
        return this;
    }

    public Service SetTranslation(string languageCode, string name, string description)
    {
        _translations[languageCode] = new Translation { Name = name, Description = description };
        return this;
    }

    public Service RemoveTranslation(string languageCode)
    {
        _translations.Remove(languageCode);
        return this;
    }

    public Service ClearTranslations()
    {
        _translations = new Dictionary<string, Translation>();
        return this;
    }
}
