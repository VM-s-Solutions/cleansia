using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class ExportUserData
{
    public record Query : IQuery<GdprExportDto>;

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IGdprExportService gdprExportService,
        IGdprRequestRepository gdprRequestRepository)
        : IQueryHandler<Query, GdprExportDto>
    {
        public async Task<BusinessResult<GdprExportDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return BusinessResult.Failure<GdprExportDto>(new Error(
                    BusinessErrorMessage.UserNotFound, "User not found"));

            // Audit-row-first pattern. The row is added in Pending state and
            // transitions to Completed on success or Failed on exception.
            // GDPR Article 30 requires logging the REQUEST, not just the
            // successful response — so we must persist a row even when the
            // build throws.
            var auditEntry = Core.Domain.Users.GdprRequest.Create(user.Id, "Export");
            gdprRequestRepository.Add(auditEntry);

            try
            {
                var export = await gdprExportService.BuildAsync(user.Id, user.Email, cancellationToken);
                auditEntry.MarkCompleted(user.Email);
                return BusinessResult.Success(export);
            }
            catch
            {
                auditEntry.MarkFailed("Export build threw — see logs.");
                throw;
            }
        }
    }
}
