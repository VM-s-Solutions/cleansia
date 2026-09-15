namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// One entry of <see cref="TenantSettingCatalog"/>: what an operating company may configure under a
/// key, in what type and range, and what applies when it has not. The stored form is the canonical
/// one this definition produces, so a row is either a value the catalogue accepts or ignored.
/// </summary>
public abstract class TenantSettingDefinition(string key, string category)
{
    public string Key { get; } = key;

    public string Category { get; } = category;

    public abstract TenantSettingValueType ValueType { get; }

    public abstract string DefaultValue { get; }

    public abstract int? Min { get; }

    public abstract int? Max { get; }

    public abstract string? Canonicalize(string? value);

    public bool IsValid(string? value) => Canonicalize(value) is not null;
}

public abstract class TenantSettingDefinition<T>(string key, string category, T @default) : TenantSettingDefinition(key, category)
{
    public T Default { get; } = @default;

    public override string DefaultValue => Format(Default);

    public abstract bool TryParse(string? value, out T parsed);

    public abstract string Format(T value);

    public override string? Canonicalize(string? value) => TryParse(value, out var parsed) ? Format(parsed) : null;

    public T Resolve(string? value) => TryParse(value, out var parsed) ? parsed : Default;
}
