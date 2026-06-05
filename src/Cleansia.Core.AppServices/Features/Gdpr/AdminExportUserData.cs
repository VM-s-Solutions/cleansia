using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminExportUserData
{
    public record Query(string UserId) : IQuery<GdprExportDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            // The GDPR export tool is for customer/employee data-subject
            // requests only — it must never target an administrator. Cascade.Stop so the existence
            // check runs before the Profile guard and BuildAsync (which marks a completed export
            // row) is never reached on a reject.
            RuleFor(q => q.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId)
                .MustAsync(async (id, ct) =>
                    !await userRepository.GetAll()
                        .AnyAsync(u => u.Id == id && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.CannotTargetAdminViaGdprTool);
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
