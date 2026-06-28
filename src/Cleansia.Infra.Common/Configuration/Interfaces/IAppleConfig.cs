namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IAppleConfig
{
    /// <summary>
    /// The native app bundle id used as the required audience when verifying Apple identity tokens
    /// (the <c>cz.cleansia.customer</c> App ID — NOT a Services ID, which is a web-only construct).
    /// Bound from the <c>Apple:BundleId</c> setting; the owner supplies the real value (T-0344). When
    /// empty, token verification fails closed.
    /// </summary>
    string BundleId { get; set; }
}
