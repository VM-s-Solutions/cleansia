namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IFcmConfig
{
    /// <summary>
    /// Firebase service-account credentials, base64-encoded. See
    /// docs/development/push-notifications.md for the encode command.
    /// Empty string disables push dispatch unless [ProjectId] is set and
    /// Application Default Credentials are available (run `gcloud auth
    /// application-default login` once on the host).
    /// </summary>
    string ServiceAccountJson { get; set; }

    /// <summary>
    /// GCP/Firebase project id (e.g. "cleansia-cz"). Optional when
    /// [ServiceAccountJson] is set (the project id is read from the JSON).
    /// REQUIRED when falling back to ADC — user credentials don't carry a
    /// project association so the Firebase Admin SDK needs it explicitly.
    /// </summary>
    string ProjectId { get; set; }
}
