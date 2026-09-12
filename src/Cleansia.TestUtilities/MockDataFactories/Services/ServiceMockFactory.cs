using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Services;

namespace Cleansia.TestUtilities.MockDataFactories.Services;

public class ServiceMockFactory
{
    public class ServicePartial
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }


    }

    /// <summary>
    /// The generated entity is PRICELESS. A catalogue entry has no price of its own any more -- it has
    /// a price per currency, and a fixture that needs one adds a price row for the currency it is
    /// pricing in. Baking one in here would put a number on the entity that no production code can
    /// read, which is exactly how a fixture stops resembling the thing it stands in for.
    /// </summary>
    public static Service Generate(ServicePartial? mergeFrom = null, string categoryId = "test-category-id")
    {
        var service = Service.Create(
            categoryId: categoryId,
            name: "Service1",
            description: "There is some service that we provide");
        service.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        return service.Merge(mergeFrom);
    }
}