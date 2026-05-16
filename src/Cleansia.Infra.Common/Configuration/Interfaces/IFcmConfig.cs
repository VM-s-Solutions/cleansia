namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IFcmConfig
{
    /// <summary>
    /// Firebase service-account credentials, base64-encoded. See
    /// docs/development/push-notifications.md for the encode command.
    /// Empty string disables push dispatch.
    /// </summary>
    string ServiceAccountJson { get; set; }
}
