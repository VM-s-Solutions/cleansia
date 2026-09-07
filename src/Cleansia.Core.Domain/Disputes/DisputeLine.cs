using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Disputes;

/// <summary>
/// One item of the order that the customer says was not done properly.
///
/// <para><b>A child table, not a jsonb column</b> — owner ruling 2026-09-05, taken on the long-term
/// question rather than the cheapest-today one. <c>OrderReview.Tags</c> is jsonb and rightly so: a
/// tag is a flat closed enum that will never grow fields. A disputed line is not that. It has real
/// foreign keys, so a service cannot be deleted out from under a settled dispute the way a bare id
/// in a JSON array can go dangling with nothing to catch it; it has room to grow (a per-item note is
/// the obvious next ask); and "which service gets disputed most" is a plain GROUP BY rather than a
/// jsonb containment query.</para>
///
/// <para><b>The identity is (ServiceId, PackageId?)</b>, matching <c>RefundLineSelection</c> exactly.
/// The refund allocator already speaks that vocabulary, so a dispute line and a refund line are the
/// same thing named the same way — which is the whole point of letting the customer author the
/// selection an admin would otherwise author alone. A standalone service leaves
/// <see cref="PackageId"/> null; a service inside a bundle names both.</para>
/// </summary>
public class DisputeLine : Auditable
{
    [Required]
    [MaxLength(26)]
    public string DisputeId { get; private set; } = default!;
    public Dispute? Dispute { get; private set; }

    [Required]
    [MaxLength(26)]
    public string ServiceId { get; private set; } = default!;

    /// <summary>Null for a standalone service; set when the service came inside a package.</summary>
    [MaxLength(26)]
    public string? PackageId { get; private set; }

    private DisputeLine() { }

    public static DisputeLine Create(string disputeId, string serviceId, string? packageId, string createdBy)
    {
        var line = new DisputeLine
        {
            DisputeId = disputeId,
            ServiceId = serviceId,
            PackageId = packageId,
        };
        line.Created(createdBy, DateTimeOffset.UtcNow);
        return line;
    }
}
