namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IGoogleConfig
{
    /// <summary>
    /// The Google OAuth client id used as the required audience when verifying Google ID-tokens.
    /// Bound from the <c>Google:ClientId</c> setting; the owner supplies the real value. When empty,
    /// token verification fails closed.
    /// </summary>
    string ClientId { get; set; }
}
