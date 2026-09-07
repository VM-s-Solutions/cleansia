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
/// Issues a first-order discount code to an address that has no account yet, and
/// queues the e-mail carrying it.
/// </summary>
/// <remarks>
/// Anonymous and therefore abusable in two directions, both closed here:
///
/// <para><b>Flooding the table.</b> The code is derived from the address rather
/// than random, so asking twice returns the same code instead of minting a
/// second row. A visitor who lost the e-mail gets the same code back.</para>
///
/// <para><b>Mailing somebody else repeatedly.</b> Because the code is stable, the
/// queue key <c>MessageKeys.Email(PromoCode, code, hash(code))</c> is stable too,
/// so the consumer's idempotency claim lets exactly one promo e-mail reach an
/// address ever. Rate limiting bounds the request rate on top of that.</para>
///
/// The derivation is one-way: a code cannot be turned back into the address, so
/// nothing here puts an e-mail into a queue key or a log line (S6).
/// → /architecture/security-rules
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

            if (existing is null)
            {
                var promo = PromoCode.CreatePercent(
                    code,
                    FirstOrderDiscountPercent,
                    maxRedemptionsPerUser: 1,
                    globalMaxRedemptions: 1,
                    validFrom: DateTimeOffset.UtcNow,
                    validUntil: DateTimeOffset.UtcNow.AddDays(ValidForDays),
                    description: "First-order code issued on request from the public site");

                promoCodeRepository.Add(promo);

                // The code is derived from the e-mail, so two simultaneous requests for one address
                // both read null and both insert the SAME code. (TenantId, Code) UNIQUE arbitrates
                // that — and only started doing so once it was declared NULLS NOT DISTINCT.
                //
                // The loser does not get an error, because the loser's desired outcome ALREADY
                // HAPPENED: the winner created exactly the code this request would have created.
                // Detaching the duplicate (Remove on an Added entity untracks it) lets the pipeline
                // commit carry the e-mail through, so the visitor gets their code either way. The
                // alternative — flushing and failing — would answer a public form with an error for
                // a race the visitor cannot see and did not cause.
                try
                {
                    await promoCodeRepository.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                    when (DbConstraintViolation.IsUniqueViolation(ex))
                {
                    promoCodeRepository.Remove(promo);
                }
            }

            // Post-commit: the row and the message are written in one transaction by
            // the UnitOfWork behaviour, so an e-mail can never advertise a code that
            // failed to save. → /flows/cross-cutting
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
