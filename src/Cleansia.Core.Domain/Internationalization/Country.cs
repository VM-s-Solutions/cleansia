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

    /// <summary>
    /// ISO 3166-1 alpha-2, the two letters a customer surface prints beside a currency code
    /// ("CZ · CZK"). <see cref="IsoCode"/> stays alpha-3 because clients persist it and the seed
    /// references it; this is the display form, nothing keys on it.
    /// </summary>
    [Required]
    [MaxLength(2)]
    public string IsoAlpha2 { get; private set; }

    /// <summary>
    /// True when the company actually operates in this country (i.e. customers
    /// can book here). Separate from <see cref="BaseEntity.IsActive"/>, which
    /// is the admin-catalog flag (whether the country shows up in any admin
    /// picker at all). Customer/partner-facing pickers filter on IsServiced;
    /// admin pickers use IsActive only.
    /// </summary>
    public bool IsServiced { get; private set; } = false;

    private IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    private ICollection<Employee> _employees = [];
    public IReadOnlyCollection<Employee> Employees => _employees.ToList().AsReadOnly();

    public static Country Create(string name, string isoCode, string isoAlpha2, bool isServiced = false) => new()
    {
        Name = name,
        IsoCode = isoCode,
        IsoAlpha2 = isoAlpha2,
        IsServiced = isServiced,
    };

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void SetIsoAlpha2(string isoAlpha2)
    {
        IsoAlpha2 = isoAlpha2;
    }

    public Country SetServiced(bool isServiced)
    {
        IsServiced = isServiced;
        return this;
    }
}