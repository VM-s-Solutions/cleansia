using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// A Command, not a Query, so the <c>GdprRequest("Export")</c> row it adds is committed — as a Query it
/// never was (ADR-0062 D6, Q-AUD-O3). No audit marker: a self-export is the subject reading their own
/// record, not an act on somebody else.
/// </summary>
public static class ExportUserData
{
    public record Command : ICommand<GdprExportDto>;

    // Required even though the command is parameterless: the validation pipeline rejects any *Command
    // with no registered validator. The export operates on the session user, so there is no input.
    public class Validator : AbstractValidator<Command>;

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IGdprExportService gdprExportService,
        IGdprRequestRepository gdprRequestRepository)
        : ICommandHandler<Command, GdprExportDto>
    {
        public async Task<BusinessResult<GdprExportDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return BusinessResult.Failure<GdprExportDto>(new Error(
                    nameof(userId), BusinessErrorMessage.UserNotFound));

            var auditEntry = Core.Domain.Users.GdprRequest.Create(user.Id, GdprAuditReasons.ExportRequestType);
            gdprRequestRepository.Add(auditEntry);

            var export = await gdprExportService.BuildAsync(user.Id, user.Email, cancellationToken);
            auditEntry.MarkCompleted(user.Email);

            return BusinessResult.Success(export);
        }
    }
}
