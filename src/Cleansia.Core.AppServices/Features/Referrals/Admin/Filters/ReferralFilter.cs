#nullable enable
using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin.Filters;

/// <param name="Held">True: only referrals held for review (Accepted with hold reasons); false: none of them.</param>
public record ReferralFilter(
    ReferralStatus? Status = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    bool? Held = null);
