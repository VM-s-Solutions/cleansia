using System.Security.Claims;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Disputes;

/// <summary>Tenant-filtered for admins; owner-pinned across operators for customers.</summary>
internal static class DisputeReads
{
    public static IQueryable<Dispute> ForCaller(IDisputeRepository disputeRepository, IUserSessionProvider userSessionProvider)
    {
        var isAdmin = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value == UserProfile.Administrator.ToString();
        var userId = userSessionProvider.GetUserId();
        return isAdmin || string.IsNullOrEmpty(userId)
            ? disputeRepository.GetQueryable()
            : disputeRepository.GetQueryableForOwner(userId);
    }

    public static Task<bool> ExistsForCallerAsync(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        string disputeId,
        CancellationToken cancellationToken)
        => ForCaller(disputeRepository, userSessionProvider).AnyAsync(d => d.Id == disputeId, cancellationToken);
}
