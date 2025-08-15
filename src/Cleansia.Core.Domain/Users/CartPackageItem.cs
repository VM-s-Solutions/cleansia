using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Packages;
using System.ComponentModel.DataAnnotations;

namespace Cleansia.Core.Domain.Users;

public class CartPackageItem : BaseEntity
{
    [Required]
    [Range(1, 1000)]
    public int Quantity { get; private set; }

    [Required]
    public string CartId { get; private set; }

    public virtual Cart? Cart { get; private set; }

    [Required]
    public string PackageId { get; private set; }

    [Required]
    public virtual Package? Package { get; private set; }

    public static CartPackageItem Create(string cartId, Package package, int quantity) => new()
    {
        Quantity = quantity,
        CartId = cartId,
        Package = package,
        PackageId = package.Id
    };

    public void Update(int quantity)
    {
        Quantity = quantity;
    }

    public void Add(int quantity)
    {
        Quantity += quantity;
    }
}