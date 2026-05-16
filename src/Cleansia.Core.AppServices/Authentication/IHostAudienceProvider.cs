namespace Cleansia.Core.AppServices.Authentication;

/// <summary>
/// Returns the JWT audience identifying the current API host. Each Web host
/// (Customer, Partner, Mobile, Admin) registers its own implementation in DI
/// so token issuance and refresh can bind tokens to a single host. Tokens
/// issued by one host are rejected by the others via JWT audience validation.
/// </summary>
public interface IHostAudienceProvider
{
    string Audience { get; }
}

public class HostAudienceProvider(string audience) : IHostAudienceProvider
{
    public string Audience { get; } = audience;
}
