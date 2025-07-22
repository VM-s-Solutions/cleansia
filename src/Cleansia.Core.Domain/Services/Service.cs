using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Services;

public class Service : Auditable
{
    [Required]
    [MaxLength(100)]
    public string Name { get; private set; }

    [MaxLength(500)]
    public string Description { get; private set; }

    [Required]
    public decimal BasePrice { get; private set; }

    public decimal PerRoomPrice { get; private set; }

    public IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<Package> _packages = [];
    public IReadOnlyCollection<Package> Packages => _packages.ToList().AsReadOnly();

    private ICollection<OrderService> _includedInOrders = [];
    public IReadOnlyCollection<OrderService> IncludedInOrders => _includedInOrders.ToList().AsReadOnly();

    public static Service Create(string name, string description, decimal basePrice, decimal perRoomPrice) => new()
    {
        Name = name,
        Description = description,
        BasePrice = basePrice,
        PerRoomPrice = perRoomPrice
    };
}