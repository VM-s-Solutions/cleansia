using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.Domain.Contracts;

/// <summary>
/// One cleaner's acceptance of the contract for work for one seat of one job: which text row they read
/// (hence the document, its version, the language and the content hash by one join), when, from where,
/// and the job facts as shown to them at that instant. Append-only and bound to the SEAT: a take, a drop
/// and a re-take are two seats and two rows; an admin re-add of the same cleaner is a new seat with no
/// row, which is exactly the case the start and complete gates refuse.
///
/// <para>The seat and the cleaner are bare scalars with no foreign key because the row outlives both —
/// the seat is hard-deleted by the next drop, the cleaner is anonymised on erasure. The order and the
/// text row are foreign keys (Restrict) because the row is meaningless without either.</para>
///
/// <para>Private setters, one factory, one sanctioned mutator: <see cref="Pseudonymise"/> blanks the
/// three request-metadata columns on the cleaner's erasure and after the per-company retention window,
/// and nothing else. The facts never carry the street or a name — <c>WorkContractFactsPiiGuardTests</c>
/// walks the facts record so the row can outlive the person it names.</para>
/// </summary>
public class WorkContractAcceptance : TenantAuditable
{
    [Required]
    [MaxLength(26)]
    public string OrderId { get; private set; } = default!;

    [Required]
    [MaxLength(26)]
    public string OrderEmployeeId { get; private set; } = default!;

    [Required]
    [MaxLength(26)]
    public string EmployeeId { get; private set; } = default!;

    [Required]
    [MaxLength(26)]
    public string LegalDocumentTextId { get; private set; } = default!;

    [Required]
    [MaxLength(32)]
    public string DocumentVersion { get; private set; } = default!;

    public DateTimeOffset AcceptedOn { get; private set; }

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
    public string FactsJson { get; private set; } = default!;

    private WorkContractAcceptance()
    {
    }

    /// <summary>
    /// The only way to make one. <c>CreatedBy</c> and <c>CreatedOn</c> are stamped at commit;
    /// <c>AcceptedOn</c> is the legal instant, read from the server clock here so the row carries it
    /// whatever the commit's own timing is.
    /// </summary>
    public static WorkContractAcceptance Create(
        string orderId,
        string orderEmployeeId,
        string employeeId,
        LegalDocumentText text,
        string documentVersion,
        string clientAudience,
        string? ipAddress,
        string? deviceLabel,
        string? deviceId,
        string factsJson) =>
        new()
        {
            OrderId = orderId,
            OrderEmployeeId = orderEmployeeId,
            EmployeeId = employeeId,
            LegalDocumentTextId = text.Id,
            DocumentVersion = documentVersion,
            AcceptedOn = DateTimeOffset.UtcNow,
            ClientAudience = clientAudience,
            IpAddress = ipAddress,
            DeviceLabel = deviceLabel,
            DeviceId = deviceId,
            FactsJson = factsJson,
        };

    /// <summary>
    /// The erasure's and the retention sweep's write: the IP address, device label and device id are
    /// personal data; the act, the seat, the text and the facts are the evidence and stay.
    /// </summary>
    public void Pseudonymise()
    {
        IpAddress = null;
        DeviceLabel = null;
        DeviceId = null;
    }
}
