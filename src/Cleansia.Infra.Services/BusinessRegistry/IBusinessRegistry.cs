namespace Cleansia.Infra.Services.BusinessRegistry;

public enum BusinessRegistryAnswer
{
    /// <summary>No register was asked: none is wired for the country, or lookups are switched off.</summary>
    NotConsulted,
    Registered,
    NotRegistered,
    /// <summary>The register did not answer: an outage, a timeout, a rate limit or a reply that could not be read.</summary>
    Unavailable,
}

/// <summary>
/// What a public business register says about one registration number. <see cref="Ceased"/> and
/// <see cref="TradeLicenceActive"/> mean something only when the number is <see cref="BusinessRegistryAnswer.Registered"/>.
/// </summary>
public sealed record BusinessRegistryRecord(
    BusinessRegistryAnswer Answer,
    bool Ceased = false,
    bool TradeLicenceActive = false)
{
    public static readonly BusinessRegistryRecord NotConsulted = new(BusinessRegistryAnswer.NotConsulted);
    public static readonly BusinessRegistryRecord NotRegistered = new(BusinessRegistryAnswer.NotRegistered);
    public static readonly BusinessRegistryRecord Unavailable = new(BusinessRegistryAnswer.Unavailable);
}

public interface IBusinessRegistry
{
    /// <param name="countryIsoCode">The country whose register holds the number; the register is chosen by it.</param>
    Task<BusinessRegistryRecord> LookupAsync(
        string countryIsoCode,
        string registrationNumber,
        CancellationToken cancellationToken);
}
