namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GdprAuditReasons
{
    public const string SelfDeletion = "GDPR_DELETION";
    public const string AdminDeletion = "GDPR_ADMIN_DELETION";

    /// <summary>
    /// The deactivation stamp of an erasure that completed on a RETRY of a failed request — an admin's
    /// or the timer's — whichever path filed the request originally.
    /// </summary>
    public const string RetriedDeletion = "GDPR_DELETION_RETRY";

    /// <summary>
    /// Whether a deactivation stamp is an erasure's. The anonymised name and address are markers a
    /// reader could mistake for values; the stamp is the erasure's own signature.
    /// </summary>
    public static bool IsErasure(string? deactivatedBy) =>
        deactivatedBy is SelfDeletion or AdminDeletion or RetriedDeletion;

    public const string FallbackAdminActor = "admin";

    /// <summary>The actor a retry records when no session carries an e-mail — the timer.</summary>
    public const string SystemActor = "system";

    /// <summary>
    /// The actor of a customer's own deletion. Never the subject's address: the request row outlives the
    /// erasure, and a failed attempt is put on record before the walk, while the address is still live.
    /// </summary>
    public const string SelfActor = "self";

    /// <summary>One spelling for the <c>GdprRequest.RequestType</c> both exports file and the admin list filters on.</summary>
    public const string ExportRequestType = "Export";

    /// <summary>
    /// The <c>RefreshToken.RevokedReason</c> an erasure stamps. Deliberately NOT "password_reset": that
    /// string alone drives the ADR-0027 revoked-user poll, and an erasure needs no accelerated session cut
    /// (the refresh path already refuses a deactivated user). What it needs is for the revoked-or-expired
    /// retention clock to start.
    /// </summary>
    public const string RefreshTokenRevocation = "gdpr_erasure";
}
