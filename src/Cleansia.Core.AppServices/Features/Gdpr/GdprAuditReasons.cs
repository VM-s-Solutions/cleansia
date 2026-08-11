namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GdprAuditReasons
{
    public const string SelfDeletion = "GDPR_DELETION";
    public const string AdminDeletion = "GDPR_ADMIN_DELETION";
    public const string FallbackAdminActor = "admin";

    /// <summary>
    /// The <c>RefreshToken.RevokedReason</c> an erasure stamps. Deliberately NOT "password_reset": that
    /// string alone drives the ADR-0027 revoked-user poll, and an erasure needs no accelerated session cut
    /// (the refresh path already refuses a deactivated user). What it needs is for the revoked-or-expired
    /// retention clock to start.
    /// </summary>
    public const string RefreshTokenRevocation = "gdpr_erasure";
}
