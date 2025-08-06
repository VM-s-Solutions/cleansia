using Cleansia.Core.Domain.Common;
using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Users;

public class CartServiceItem : BaseEntity
{
    [Required]
    [Range(1, 1000)]
    public int Quantity { get; private set; }

    [Required]
    public string CartId { get; private set; }

    public virtual Cart? Cart { get; private set; }

    [Required]
    public string ServiceId { get; private set; }

    [Required]
    public virtual Service? Service { get; private set; }

    public static CartServiceItem Create(string cartId, Service service, int quantity) => new()
    {
        Quantity = quantity,
        CartId = cartId,
        Service = service,
        ServiceId = service.Id
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