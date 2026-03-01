using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Configuration;

public class FeatureFlag : Auditable
{
    [Required]
    [MaxLength(100)]
    public string Name { get; private set; } = default!;

    [MaxLength(500)]
    public string? Description { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Scope: "global", "country", "tenant". Determines at which level this flag applies.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Scope { get; private set; } = "global";

    /// <summary>
    /// The scope value (e.g., country ISO code or tenant ID). Null for global flags.
    /// </summary>
    [MaxLength(26)]
    public string? ScopeValue { get; private set; }

    public static FeatureFlag Create(string name, bool isEnabled, string scope = "global", string? scopeValue = null, string? description = null)
        => new()
        {
            Name = name,
            IsEnabled = isEnabled,
            Scope = scope,
            ScopeValue = scopeValue,
            Description = description
        };

    public FeatureFlag Toggle()
    {
        IsEnabled = !IsEnabled;
        return this;
    }

    public FeatureFlag Enable()
    {
        IsEnabled = true;
        return this;
    }

    public FeatureFlag Disable()
    {
        IsEnabled = false;
        return this;
    }
}
