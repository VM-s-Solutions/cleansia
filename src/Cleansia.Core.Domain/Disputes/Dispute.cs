using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Disputes;

public class Dispute : Auditable
{
    [Required]
    public string OrderId { get; private set; }
    public Order Order { get; private set; }

    [Required]
    public string UserId { get; private set; }
    public User User { get; private set; }

    [Required]
    public DisputeReason Reason { get; private set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; private set; }

    [Required]
    public DisputeStatus Status { get; private set; } = DisputeStatus.Pending;

    [MaxLength(2000)]
    public string? ResolutionNotes { get; private set; }

    public decimal? RefundAmount { get; private set; }

    public string? ResolvedBy { get; private set; }

    public DateTimeOffset? ResolvedOn { get; private set; }

    public string? StripeDisputeId { get; private set; }

    private readonly List<DisputeMessage> _messages = new();
    public IReadOnlyCollection<DisputeMessage> Messages => _messages.AsReadOnly();

    private readonly List<DisputeEvidence> _evidence = new();
    public IReadOnlyCollection<DisputeEvidence> Evidence => _evidence.AsReadOnly();

    // Private constructor for EF Core
    private Dispute() { }

    public Dispute(
        string orderId,
        string userId,
        DisputeReason reason,
        string description,
        string createdBy)
    {
        OrderId = orderId;
        UserId = userId;
        Reason = reason;
        Description = description;
        Status = DisputeStatus.Pending;
        Created(createdBy, DateTimeOffset.UtcNow);
    }

    public void UpdateStatus(DisputeStatus newStatus, string updatedBy)
    {
        Status = newStatus;
        Updated(updatedBy, DateTimeOffset.UtcNow);
    }

    public void AddMessage(string message, string authorId, bool isStaff)
    {
        var disputeMessage = new DisputeMessage(Id, message, authorId, isStaff);
        _messages.Add(disputeMessage);
    }

    public void AddEvidence(string fileName, string filePath, string uploadedBy)
    {
        var evidence = new DisputeEvidence(Id, fileName, filePath, uploadedBy);
        _evidence.Add(evidence);
    }

    public void Resolve(string resolvedBy, decimal? refundAmount, string resolutionNotes)
    {
        Status = DisputeStatus.Resolved;
        ResolvedBy = resolvedBy;
        ResolvedOn = DateTimeOffset.UtcNow;
        RefundAmount = refundAmount;
        ResolutionNotes = resolutionNotes;
        Updated(resolvedBy, DateTimeOffset.UtcNow);
    }

    public void Close(string closedBy)
    {
        Status = DisputeStatus.Closed;
        Updated(closedBy, DateTimeOffset.UtcNow);
    }

    public void Escalate(string escalatedBy)
    {
        Status = DisputeStatus.Escalated;
        Updated(escalatedBy, DateTimeOffset.UtcNow);
    }

    public void LinkStripeDispute(string stripeDisputeId, string updatedBy)
    {
        StripeDisputeId = stripeDisputeId;
        Updated(updatedBy, DateTimeOffset.UtcNow);
    }
}
