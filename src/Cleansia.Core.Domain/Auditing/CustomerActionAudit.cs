using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Auditing;

/// <summary>
/// What a customer did that money or an entitlement turns on, kept so a dispute, chargeback or
/// complaint can be answered from the record (ADR-0062).
///
/// <para><b>Its own table, never <see cref="AdminActionAudit"/>.</b> Owner ruling 2026-09-06 keeps actor
/// kinds apart; that table's erasure verdict says "its ActorEmail is the ADMIN who acted" and must stay
/// true. Written by the same ADR-0012 pipeline through the customer arm of <c>AuditGate</c>: a success
/// row rides the action's commit, a refusal is written out-of-band.</para>
///
/// <para><b>What a row may hold.</b> Identifiers, money, enums, versions and request context — never a
/// name, contact detail, address text, free text the customer typed, card data or a token. No actor
/// email: here the actor IS the subject, and a copy would be the second uncontrolled copy ADR-0012 D4.1
/// forbids. <see cref="UserId"/> is a bare scalar with no navigation and no FK because the row must
/// outlive everything it names; <see cref="ClientAudience"/> is the JWT audience of the host that served
/// the request, filled for an anonymous act too — and the customer web and the customer app share one, so
/// it names the client family, not the host; <see cref="DeviceLabel"/> is what tells those two apart.
/// <see cref="DeviceId"/> on a signed-in row is the session's signed claim, on an anonymous row the
/// client's own header.</para>
///
/// <para><b>Append-only, with one sanctioned mutator.</b> <see cref="Pseudonymise"/> blanks the three
/// request-metadata columns on erasure and nothing else; the row itself stays for defence of claims and
/// is deleted by the retention sweep three years after its own <see cref="OccurredOn"/>.
/// <c>IRepository</c> still exposes <c>Remove</c>/<c>Deactivate</c> and <c>BaseEntity.IsActive</c> is a
/// public setter — the discipline is a test, not a type.</para>
/// </summary>
public sealed class CustomerActionAudit : BaseEntity, ITenantEntity
{
    public const int ResourceIdMaxLength = 26;
    public const int ErrorCodeMaxLength = 100;

    public string? TenantId { get; set; }

    [MaxLength(26)]
    public string? UserId { get; private set; }

    [Required]
    [MaxLength(40)]
    public string ClientAudience { get; private set; } = default!;

    [MaxLength(45)]
    public string? IpAddress { get; private set; }

    [MaxLength(120)]
    public string? DeviceLabel { get; private set; }

    [MaxLength(64)]
    public string? DeviceId { get; private set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; private set; } = default!;

    [MaxLength(50)]
    public string? ResourceType { get; private set; }

    [MaxLength(ResourceIdMaxLength)]
    public string? ResourceId { get; private set; }

    public bool Success { get; private set; }

    [MaxLength(ErrorCodeMaxLength)]
    public string? ErrorCode { get; private set; }

    public DateTimeOffset OccurredOn { get; private set; }

    public string? PayloadJson { get; private set; }

    [MaxLength(64)]
    public string? CorrelationId { get; private set; }

    private CustomerActionAudit()
    {
    }

    /// <summary>
    /// The only way to make one. <c>TenantId</c> is NOT set here — the writer and the failure sink
    /// stamp it from the ambient provider, the way they do for the admin row.
    /// </summary>
    public static CustomerActionAudit Create(
        string? userId,
        string clientAudience,
        string? ipAddress,
        string? deviceLabel,
        string? deviceId,
        string action,
        string? resourceType,
        string? resourceId,
        bool success,
        string? errorCode,
        string? payloadJson,
        string? correlationId) =>
        new()
        {
            UserId = userId,
            ClientAudience = clientAudience,
            IpAddress = ipAddress,
            DeviceLabel = deviceLabel,
            DeviceId = deviceId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Success = success,
            ErrorCode = errorCode,
            OccurredOn = DateTimeOffset.UtcNow,
            PayloadJson = payloadJson,
            CorrelationId = correlationId
        };

    /// <summary>
    /// The erasure's write: the IP address, device label and device id are personal data; the act, its
    /// outcome, the subject id and the resource id are the evidence and stay.
    /// </summary>
    public void Pseudonymise()
    {
        IpAddress = null;
        DeviceLabel = null;
        DeviceId = null;
    }
}
