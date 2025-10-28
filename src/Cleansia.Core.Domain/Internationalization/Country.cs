using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Internationalization;

public class Country : Auditable
{
    [Required]
    [MaxLength(50)]
    public string Name { get; private set; }

    [Required]
    [MaxLength(3)]
    public string IsoCode { get; private set; }

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<Employee> _employees = [];
    public IReadOnlyCollection<Employee> Employees => _employees.ToList().AsReadOnly();

    public static Country Create(string name, string isoCode) => new()
    {
        Name = name,
        IsoCode = isoCode
    };
}