using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PromoCodes;

/// <summary>
/// Issues a first-order discount code once per address and queues the e-mail carrying it.
/// </summary>
/// <remarks>
/// → /flows/loyalty-and-memberships#public-promo-code-requests
/// </remarks>
public class RequestPromoCode
{
    /// <summary>
    /// Discount on a first order.
    /// </summary>
    /// <remarks>
    /// NOT yet pinned in <c>docs/product/business-rules.md</c> — this is a
    /// placeholder awaiting an owner ruling, and it is the one number on this
    /// feature that is invented. Everything else derives from it.
    /// </remarks>
    public const decimal FirstOrderDiscountPercent = 0.10m;

    /// <summary>How long an issued code stays usable. Also awaiting a ruling.</summary>
    public const int ValidForDays = 30;

    private const string CodePrefix = "VITEJTE";

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .EmailAddress()
                .WithMessage(BusinessErrorMessage.InvalidEmailFormat);
        }
    }

    public record Command(string Email, string LanguageCode = Constants.Language.English) : ICommand<Response>;

    /// <summary>
    /// Deliberately carries no code. The code reaches the visitor by e-mail and
    /// nowhere else — returning it in the HTTP response would hand anyone who can
    /// guess an address a discount without ever proving they hold the mailbox.
    /// </summary>
    public record Response(bool Accepted);

    public class Handler(
        IPromoCodeRepository promoCodeRepository,
        IPendingDispatch pendingDispatch) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var email = command.Email.Trim().ToLowerInvariant();
            var code = DeriveCode(email);

            var existing = await promoCodeRepository.GetByCodeAsync(code, cancellationToken);

            if (existing is not null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.Email), BusinessErrorMessage.PromoCodeAlreadySent));
            }

            var promo = PromoCode.CreatePercent(
                code,
                FirstOrderDiscountPercent,
                maxRedemptionsPerUser: 1,
                globalMaxRedemptions: 1,
                validFrom: DateTimeOffset.UtcNow,
                validUntil: DateTimeOffset.UtcNow.AddDays(ValidForDays),
                description: "First-order code issued on request from the public site");

            promoCodeRepository.Add(promo);

            pendingDispatch.Enqueue(
                QueueNames.SendEmail,
                new QueueEnvelope<SendEmailMessage>(
                    MessageKeys.Email(EmailType.PromoCode, code, MessageKeys.HashCode(code)),
                    null,
                    new SendEmailMessage(
                        EmailType.PromoCode,
                        email,
                        UserName: string.Empty,
                        Code: code,
                        LanguageCode: command.LanguageCode,
                        UserId: code)),
                MessageKeys.Email(EmailType.PromoCode, code, MessageKeys.HashCode(code)));

            // Flush both rows together so a concurrent duplicate is mapped before the pipeline returns.
            // → /flows/loyalty-and-memberships#public-promo-code-requests
            try
            {
                await promoCodeRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.Email), BusinessErrorMessage.PromoCodeAlreadySent));
            }

            return BusinessResult.Success(new Response(true));
        }

        /// <summary>
        /// A stable, one-way code for an address: <c>VITEJTE-XXXXXX</c>.
        /// </summary>
        /// <remarks>
        /// Base32-ish over an unambiguous alphabet — no <c>0/O</c> or <c>1/I</c>,
        /// because this gets read off a screen and typed into a form.
        /// </remarks>
        private static string DeriveCode(string normalisedEmail)
        {
            const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalisedEmail));
            var builder = new StringBuilder(CodePrefix.Length + 7).Append(CodePrefix).Append('-');

            for (var i = 0; i < 6; i++)
            {
                builder.Append(Alphabet[hash[i] % Alphabet.Length]);
            }

            return builder.ToString();
        }
    }
}
