using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internalization;

namespace Cleansia.Core.Domain.Users;

public class Country : Auditable
{
    [Required]
    [MaxLength(50)]
    public string Name { get; private set; }

    public IDictionary<string, Translation> _translations = new Dictionary<string, Translation>();
    public IReadOnlyDictionary<string, Translation> Translations => _translations.AsReadOnly();

    public static Country Create(string name) => new()
    {
        Name = name
    };
}