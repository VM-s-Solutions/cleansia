namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GdprAuditReasons
{
    public const string SelfDeletion = "GDPR_DELETION";
    public const string AdminDeletion = "GDPR_ADMIN_DELETION";
    public const string FallbackAdminActor = "admin";
}
