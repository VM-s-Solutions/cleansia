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

        [Required]
        public decimal BasePrice { get; set; }

        public decimal PerRoomPrice { get; set; }
    }

    public static Service Generate(ServicePartial? mergeFrom = null)
    {
        var service = Service.Create(
            name: "Service1",
            description: "There is some service that we provide",
            basePrice: 1000.0M,
            perRoomPrice: 200.0M);
        service.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        return service.Merge(mergeFrom);
    }
}