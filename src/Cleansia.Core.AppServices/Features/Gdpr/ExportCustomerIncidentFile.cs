using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Services.Pdf;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// The customer incident file (owner ruling on Q-AUD-L6: a PDF). The one document that prints a
/// subject's identity on purpose — so, like the admin export it sits beside, it is a Command and an
/// audited admin act whose snapshot holds ids, counts and the file's own hash, never the content.
/// A build or render that throws is recorded out-of-band by the pipeline.
/// </summary>
[AuditAction("gdpr.user.incident_file", Sensitive = true, ResourceType = "User")]
public static class ExportCustomerIncidentFile
{
    public record Command(string UserId, string? OrderId) : ICommand<Response>;

    public record Response(byte[] PdfBytes, string FileName);

    public record IncidentFileSnapshot(
        string SubjectUserId,
        string? OrderId,
        int OrderCount,
        int DisputeCount,
        int ConsentCount,
        int TrailEntryCount,
        string DataSha256);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository, IIncidentFileService incidentFileService)
        {
            RuleFor(c => c.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId)
                .MustAsync(async (id, ct) =>
                    !await userRepository.GetAll()
                        .AnyAsync(u => u.Id == id && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.CannotTargetAdminViaGdprTool);

            // A stranger's order is refused as not found, never as "not yours" (S3): the endpoint must
            // not confirm that an order id exists under another customer.
            RuleFor(c => c.OrderId)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (command, orderId, ct) => await incidentFileService.IsSubjectOrderAsync(command.UserId, orderId!, ct))
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .When(c => !string.IsNullOrWhiteSpace(c.OrderId) && !string.IsNullOrWhiteSpace(c.UserId));
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IIncidentFileService incidentFileService,
        IPdfService pdfService,
        IAuditContext auditContext)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail() ?? GdprAuditReasons.FallbackAdminActor;
            var orderId = string.IsNullOrWhiteSpace(request.OrderId) ? null : request.OrderId;

            var data = await incidentFileService.BuildAsync(request.UserId, orderId, adminEmail, cancellationToken);
            var pdf = pdfService.GenerateIncidentFilePdf(data);

            var snapshot = new IncidentFileSnapshot(
                request.UserId, orderId, data.Orders.Count, data.Disputes.Count, data.Consents.Count, data.Trail.Count, pdf.DataSha256);
            auditContext.RecordChange("User", request.UserId, snapshot, snapshot);

            return BusinessResult.Success(new Response(pdf.Bytes, FileName(request.UserId, data.GeneratedAt)));
        }

        public static string FileName(string userId, DateTimeOffset generatedAt) =>
            $"incident-{userId}-{generatedAt.ToUniversalTime():yyyyMMdd}.pdf";
    }
}
