using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Disputes;

public class DisputeEvidence : BaseEntity
{
    [Required]
    public string DisputeId { get; private set; }
    public Dispute Dispute { get; private set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; private set; }

    [Required]
    [MaxLength(500)]
    public string FilePath { get; private set; }

    [Required]
    public string UploadedBy { get; private set; }

    public DateTimeOffset UploadedOn { get; private set; } = DateTimeOffset.UtcNow;

    // Private constructor for EF Core
    private DisputeEvidence() { }

    public DisputeEvidence(string disputeId, string fileName, string filePath, string uploadedBy)
    {
        DisputeId = disputeId;
        FileName = fileName;
        FilePath = filePath;
        UploadedBy = uploadedBy;
        UploadedOn = DateTimeOffset.UtcNow;
    }
}
