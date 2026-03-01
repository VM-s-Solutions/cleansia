using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Configuration;

public class TenantConfiguration : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Key { get; private set; } = default!;

    [Required]
    [MaxLength(4000)]
    public string Value { get; private set; } = default!;

    [MaxLength(200)]
    public string? Description { get; private set; }

    [MaxLength(50)]
    public string? Category { get; private set; }

    public static TenantConfiguration Create(string key, string value, string? description = null, string? category = null)
        => new()
        {
            Key = key,
            Value = value,
            Description = description,
            Category = category
        };

    public TenantConfiguration UpdateValue(string value)
    {
        Value = value;
        return this;
    }
}
