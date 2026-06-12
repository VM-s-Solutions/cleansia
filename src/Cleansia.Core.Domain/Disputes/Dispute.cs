using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Disputes;

public class Dispute : Auditable, ITenantEntity
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
    [MaxLength(DisputeLimits.DescriptionMax)]
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

    // Natural lifecycle for the in-process transition guard (Resolved is owned exclusively by Resolve):
    //   Pending ↔ UnderReview ↔ WaitingForResponse → { Closed, Escalated }
    //   Escalated → Closed
    //   Resolved and Closed are terminal (no re-open).
    private static readonly IReadOnlyDictionary<DisputeStatus, DisputeStatus[]> AllowedTransitions =
        new Dictionary<DisputeStatus, DisputeStatus[]>
        {
            [DisputeStatus.Pending] = [DisputeStatus.UnderReview, DisputeStatus.WaitingForResponse, DisputeStatus.Closed, DisputeStatus.Escalated],
            [DisputeStatus.UnderReview] = [DisputeStatus.Pending, DisputeStatus.WaitingForResponse, DisputeStatus.Closed, DisputeStatus.Escalated],
            [DisputeStatus.WaitingForResponse] = [DisputeStatus.Pending, DisputeStatus.UnderReview, DisputeStatus.Closed, DisputeStatus.Escalated],
            [DisputeStatus.Escalated] = [DisputeStatus.Closed],
            [DisputeStatus.Resolved] = [],
            [DisputeStatus.Closed] = [],
        };

    public bool CanTransitionTo(DisputeStatus newStatus) =>
        AllowedTransitions.TryGetValue(Status, out var targets) && targets.Contains(newStatus);

    // The terminal set the transition table also encodes (no outgoing edges). Named here so a second
    // writer on the same legal graph (the chargeback webhook's Resolve special, which lives OUTSIDE
    // CanTransitionTo) can ask the domain "is this dispute already settled?" rather than hand-rolling
    // the status comparison.
    public bool IsTerminal => Status is DisputeStatus.Resolved or DisputeStatus.Closed;

    public bool UpdateStatus(DisputeStatus newStatus, string updatedBy)
    {
        if (!CanTransitionTo(newStatus))
        {
            return false;
        }

        switch (newStatus)
        {
            case DisputeStatus.Closed:
                Close(updatedBy);
                break;
            case DisputeStatus.Escalated:
                Escalate(updatedBy);
                break;
            default:
                Status = newStatus;
                Updated(updatedBy, DateTimeOffset.UtcNow);
                break;
        }

        return true;
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

    public Dispute Anonymize()
    {
        Description = AnonymizationMarker.Value;
        ResolutionNotes = ResolutionNotes is null ? null : AnonymizationMarker.Value;
        foreach (var message in _messages)
        {
            message.Anonymize();
        }
        foreach (var evidence in _evidence)
        {
            evidence.Anonymize();
        }
        return this;
    }
}
