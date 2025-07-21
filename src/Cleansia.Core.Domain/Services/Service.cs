using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Services;

public class Service : Auditable
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(500)]
    public string Description { get; set; }

    [Required]
    public decimal BasePrice { get; set; }

    public decimal PerRoomPrice { get; set; }

    public Dictionary<string, Translation> Translations { get; set; } = new();

    private ICollection<Package> _packages = [];
    public IReadOnlyCollection<Package> Packages => _packages.ToList().AsReadOnly();
}