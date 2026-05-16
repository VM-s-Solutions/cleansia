using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Disputes;

public class DisputeMessage : BaseEntity
{
    [Required]
    public string DisputeId { get; private set; }
    public Dispute Dispute { get; private set; }

    [Required]
    [MaxLength(2000)]
    public string Message { get; private set; }

    [Required]
    public string AuthorId { get; private set; }
    public User? Author { get; private set; }

    public bool IsStaffMessage { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;

    // Private constructor for EF Core
    private DisputeMessage() { }

    public DisputeMessage(string disputeId, string message, string authorId, bool isStaffMessage)
    {
        DisputeId = disputeId;
        Message = message;
        AuthorId = authorId;
        IsStaffMessage = isStaffMessage;
        CreatedOn = DateTimeOffset.UtcNow;
    }

    public DisputeMessage Anonymize()
    {
        Message = AnonymizationMarker.Value;
        return this;
    }
}
