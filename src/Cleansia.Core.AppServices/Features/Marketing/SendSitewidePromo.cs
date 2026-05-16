using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Marketing;

/// <summary>
/// Admin-triggered "send sitewide promo" action. Synchronously enqueues
/// exactly one fan-out message and returns. The actual per-user dispatch
/// (paging through users with <c>Promo = true</c>, enqueueing the per-user
/// notification message) runs inside
/// <c>SendSitewidePromoFanoutFunction</c>.
///
/// Phase B's only event whose body is NOT a fixed mobile-side template.
/// Admin types per-locale title+body in the UI; mobile receives the
/// already-localized strings via the FCM data payload.
///
/// Locale coverage is intentionally strict (all 5: en/cs/sk/uk/ru must be
/// non-empty) so users always see body text in their language. We considered
/// fall-back-to-English, but a Czech user receiving an English marketing
/// notification looks broken — better to force the admin to fill in every
/// locale than to ship a half-translated campaign.
/// </summary>
public static class SendSitewidePromo
{
    public record Command(
        string TitleEn,
        string TitleCs,
        string TitleSk,
        string TitleUk,
        string TitleRu,
        string BodyEn,
        string BodyCs,
        string BodySk,
        string BodyUk,
        string BodyRu) : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(120);
            RuleFor(x => x.TitleCs).NotEmpty().MaximumLength(120);
            RuleFor(x => x.TitleSk).NotEmpty().MaximumLength(120);
            RuleFor(x => x.TitleUk).NotEmpty().MaximumLength(120);
            RuleFor(x => x.TitleRu).NotEmpty().MaximumLength(120);
            // Push body limit kept generous; FCM caps the *payload* at 4KB and
            // a single notification's bigText style truncates around ~3 lines
            // on most devices. 500 chars per locale leaves room for the title
            // + the 5x body fanout in the queue message without overrunning.
            RuleFor(x => x.BodyEn).NotEmpty().MaximumLength(500);
            RuleFor(x => x.BodyCs).NotEmpty().MaximumLength(500);
            RuleFor(x => x.BodySk).NotEmpty().MaximumLength(500);
            RuleFor(x => x.BodyUk).NotEmpty().MaximumLength(500);
            RuleFor(x => x.BodyRu).NotEmpty().MaximumLength(500);
        }
    }

    public class Handler(
        IQueueClient queueClient,
        ITenantProvider tenantProvider) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var message = new SendSitewidePromoMessage(
                TitleByLocale: new Dictionary<string, string>
                {
                    ["en"] = command.TitleEn,
                    ["cs"] = command.TitleCs,
                    ["sk"] = command.TitleSk,
                    ["uk"] = command.TitleUk,
                    ["ru"] = command.TitleRu,
                },
                BodyByLocale: new Dictionary<string, string>
                {
                    ["en"] = command.BodyEn,
                    ["cs"] = command.BodyCs,
                    ["sk"] = command.BodySk,
                    ["uk"] = command.BodyUk,
                    ["ru"] = command.BodyRu,
                },
                TenantId: tenantProvider.GetCurrentTenantId());

            await queueClient.SendAsync(
                QueueNames.SitewidePromoFanout, message, cancellationToken);

            return BusinessResult.Success();
        }
    }
}
