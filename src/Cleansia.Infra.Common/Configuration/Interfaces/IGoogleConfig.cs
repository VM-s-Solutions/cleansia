namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IGoogleConfig
{
    /// <summary>
    /// The Google OAuth client id used as the required audience when verifying Google ID-tokens
    /// (T-0105 / IDA-SEC-01). Bound from the <c>Google:ClientId</c> setting; the owner supplies the
    /// real value (IMP-1). When empty, token verification fails closed.
    /// </summary>
    string ClientId { get; set; }
}
