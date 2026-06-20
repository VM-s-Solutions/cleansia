#nullable enable
using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin.Filters;

public record ReferralFilter(
    ReferralStatus? Status = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null);
