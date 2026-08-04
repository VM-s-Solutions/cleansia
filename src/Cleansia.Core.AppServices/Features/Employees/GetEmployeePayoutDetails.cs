using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// The admin's default view of a cleaner's payout destination: <b>masked</b> (ADR-0034 D8.2). The
/// unmasked value is reachable only through <see cref="RevealEmployeePayoutDetails"/>, which is audited
/// and rate-limited — the audit trail is the compensating control for storing these in plaintext.
/// </summary>
public class GetEmployeePayoutDetails
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.EmployeeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public record Query(string EmployeeId) : IQuery<MaskedPayoutDetails>;

    public class Handler(
        IEmployeePayoutDetailsRepository payoutDetailsRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, MaskedPayoutDetails>
    {
        public async Task<BusinessResult<MaskedPayoutDetails>> Handle(Query query, CancellationToken cancellationToken)
        {
            // Role arm first, owner-id equality otherwise, NotFound on mismatch — the two-arm shape from
            // DownloadInvoice.Handler. UpdateBankDetails' ownership check has no admin arm and copying it
            // here would ship a read no admin can call.
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString())
            {
                var callerEmployeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (string.IsNullOrEmpty(callerEmployeeId) || callerEmployeeId != query.EmployeeId)
                {
                    return NotFound();
                }
            }

            var details = await payoutDetailsRepository.GetByEmployeeIdAsync(query.EmployeeId, cancellationToken);

            return details is null
                ? NotFound()
                : BusinessResult.Success(details.MapToMaskedDto());
        }

        private static BusinessResult<MaskedPayoutDetails> NotFound() =>
            BusinessResult.Failure<MaskedPayoutDetails>(
                new Error(nameof(Query.EmployeeId), BusinessErrorMessage.PayoutDetailsNotFound));
    }
}
