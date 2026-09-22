namespace Cleansia.Core.AppServices.Auditing;

/// <summary>Which audit table a command's row lands in — resolved per request by <see cref="AuditGate"/>.</summary>
public enum AuditAudience
{
    Admin = 1,
    Customer = 2
}
