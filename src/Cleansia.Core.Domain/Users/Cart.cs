using Cleansia.Core.Domain.Common;
using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Users;

public class Cart : Auditable
{
    [Required]
    public string UserId { get; private set; }

    public virtual User? User { get; private set; }

    private ICollection<CartServiceItem> _serviceItems = [];
    public virtual IReadOnlyCollection<CartServiceItem> ServiceItems => _serviceItems.ToList().AsReadOnly();

    private ICollection<CartPackageItem> _packageItems = [];
    public virtual IReadOnlyCollection<CartPackageItem> PackageItems => _packageItems.ToList().AsReadOnly();

    public static Cart CreateWithUser(string userId) => new() { UserId = userId };

    public void AddService(Service service, int quantity)
    {
        var existingItem = ServiceItems.FirstOrDefault(item => item.ServiceId == service.Id);

        if (existingItem is not null)
        {
            existingItem.Add(quantity);
            return;
        }

        _serviceItems.Add(CartServiceItem.Create(Id, service, quantity));
    }

    public void UpdateService(Service service, int quantity)
    {
        var existingItem = ServiceItems.FirstOrDefault(item => item.ServiceId == service.Id);

        if (existingItem is not null)
        {
            if (quantity <= 0)
            {
                _serviceItems.Remove(existingItem);
            }
            else
            {
                existingItem.Update(quantity);
            }
        }
        else if (quantity > 0)
        {
            _serviceItems.Add(CartServiceItem.Create(Id, service, quantity));
        }
    }

    public void AddPackage(Package package, int quantity)
    {
        var existingItem = PackageItems.FirstOrDefault(item => item.PackageId == package.Id);

        if (existingItem is not null)
        {
            existingItem.Add(quantity);
            return;
        }

        _packageItems.Add(CartPackageItem.Create(Id, package, quantity));
    }

    public void UpdatePackage(Package package, int quantity)
    {
        var existingItem = PackageItems.FirstOrDefault(item => item.PackageId == package.Id);

        if (existingItem is not null)
        {
            if (quantity <= 0)
            {
                _packageItems.Remove(existingItem);
            }
            else
            {
                existingItem.Update(quantity);
            }
        }
        else if (quantity > 0)
        {
            _packageItems.Add(CartPackageItem.Create(Id, package, quantity));
        }
    }

    public void Clear()
    {
        _serviceItems = [];
        _packageItems = [];
    }
}