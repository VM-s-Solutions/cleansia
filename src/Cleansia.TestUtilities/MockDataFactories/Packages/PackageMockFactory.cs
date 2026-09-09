using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.TestUtilities.MockDataFactories.Packages;

public class PackageMockFactory
{
    public class PackagePartial
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
    public static Package Generate(PackagePartial? mergeFrom = null)
    {
        var package = Package.Create(
            "Package 1",
            "There is some description about the package");
        package.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        return package.Merge(mergeFrom);
    }
}