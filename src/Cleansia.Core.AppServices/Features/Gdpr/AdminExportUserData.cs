using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminExportUserData
{
    public record Query(string UserId) : IQuery<GdprExportDto>;

    internal class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(q => q.UserId)
                .NotEmpty()
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId);
        }
    }

    internal class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprExportService gdprExportService,
        IGdprRequestRepository gdprRequestRepository)
        : IQueryHandler<Query, GdprExportDto>
    {
        public async Task<BusinessResult<GdprExportDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail() ?? "admin";
            var exportedBy = $"admin:{adminEmail}";

            // Same audit-row-first pattern as ExportUserData — the request must
            // be logged even if the build throws (GDPR Article 30).
            var auditEntry = Core.Domain.Users.GdprRequest.Create(request.UserId, "Export");
            gdprRequestRepository.Add(auditEntry);

            try
            {
                var export = await gdprExportService.BuildAsync(request.UserId, exportedBy, cancellationToken);
                auditEntry.MarkCompleted(adminEmail);
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
