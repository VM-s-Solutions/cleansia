using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Legal;

/// <summary>
/// One cleaner's acceptance of one of their own documents — the framework contract, the self-billing
/// agreement, the data-processing agreement: which text row they read (hence the document, its version
/// and language by one join), when, from where. The consent row of that type is the "now" the gates
/// read and is moved in place by the next version; this row is the history, so the version a cleaner
/// worked and was self-billed under stays provable after they accept the next one.
///
/// <para>Append-only, like <see cref="Contracts.WorkContractAcceptance"/>: private setters, one factory,
/// and one sanctioned mutator, <see cref="Pseudonymise"/>, for the cleaner's erasure and the metadata
/// retention window. The cleaner is a bare scalar because the row outlives their anonymisation; the
/// text row is a foreign key (Restrict) because the row is meaningless without it.</para>
/// </summary>
public class CleanerLegalDocumentAcceptance : TenantAuditable
{
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

    private CleanerLegalDocumentAcceptance()
    {
    }

    public static CleanerLegalDocumentAcceptance Create(
        string employeeId,
        LegalDocumentText text,
        string documentVersion,
        string clientAudience,
        string? ipAddress,
        string? deviceLabel,
        string? deviceId) =>
        new()
        {
            EmployeeId = employeeId,
            LegalDocumentTextId = text.Id,
            DocumentVersion = documentVersion,
            AcceptedOn = DateTimeOffset.UtcNow,
            ClientAudience = clientAudience,
            IpAddress = ipAddress,
            DeviceLabel = deviceLabel,
            DeviceId = deviceId,
        };

    /// <summary>
    /// The erasure's and the retention sweep's write: the IP address, device label and device id are
    /// personal data; the act, the text and the version are the evidence and stay.
    /// </summary>
    public void Pseudonymise()
    {
        IpAddress = null;
        DeviceLabel = null;
        DeviceId = null;
    }
}
