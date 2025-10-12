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

        [Required]
        public decimal Price { get; set; }
    }

    public static Package Generate(PackagePartial? mergeFrom = null)
    {
        var package = Package.Create(
            "Package 1",
            "There is some description about the package",
            decimal.One);
        package.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        return package.Merge(mergeFrom);
    }
}