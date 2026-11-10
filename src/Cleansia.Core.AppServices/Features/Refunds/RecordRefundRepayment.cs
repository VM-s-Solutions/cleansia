using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Refunds.Mappers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>
/// Support records how a refund Stripe failed or bounced was paid back by hand: by bank transfer with its
/// reference, or as credit for a customer with an account who chose it. The console never sends the transfer.
/// → /flows/cancellation-refund-dispute#refund
/// </summary>
[AuditAction("order.refund.repaid", Sensitive = true, ResourceType = "Order")]
public class RecordRefundRepayment
{
    public record Command(string OrderId, string RefundId, RefundRepaymentMethod Method, string? Reference) : ICommand<Response>;

    public record Response(string RefundId, Code State);

    public class Validator : AbstractValidator<Command>
    {
        public const int ReferenceMaxLength = 100;

        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.RefundId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Method)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.Reference)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .When(x => x.Method == RefundRepaymentMethod.BankTransfer);

            RuleFor(x => x.Reference)
                .MaximumLength(ReferenceMaxLength)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IRefundRepository refundRepository,
        IOrderRepository orderRepository,
        ICreditAccountRepository creditAccountRepository,
        IRefundService refundService,
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminId = userSessionProvider.GetUserId()!;
            var refund = await refundRepository.GetByIdAsync(command.RefundId, cancellationToken);
            var order = refund?.OrderId == command.OrderId
                ? await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
                : null;
            if (refund is null || order is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RefundId), BusinessErrorMessage.RefundNotFound));
            }

            var state = refund.MapToState();
            if (state is not (RefundState.Failed or RefundState.Bounced))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RefundId), BusinessErrorMessage.RefundNotOwed));
            }

            CreditAccount? account = null;
            if (command.Method == RefundRepaymentMethod.Credit)
            {
                account = string.IsNullOrEmpty(order.UserId)
                          || await creditAccountRepository.IsOnFrozenCompanyBooksIgnoringTenantAsync(
                              order.UserId, order.CurrencyId, cancellationToken)
                    ? null
                    : await creditAccountRepository.EnsureForUserIgnoringTenantAsync(
                        order.UserId, order.CurrencyId, cancellationToken);
                if (account is null)
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.Method), BusinessErrorMessage.RefundCreditUnavailable));
                }
            }

            // A bounced refund already counts as given back, so it is only stamped. → /flows/cancellation-refund-dispute#refund
            RefundResult? recorded = null;
            if (state == RefundState.Bounced)
            {
                refund.MarkRepaid(command.Method, command.Reference, DateTimeOffset.UtcNow);
            }
            else
            {
                var result = await refundService.RecordRepaymentAsync(
                    refund, command.Method, command.Reference, adminId, cancellationToken);
                if (result.IsFailure)
                {
                    return BusinessResult.Failure<Response>(result.Error!);
                }

                recorded = result.Value!;
            }

            // Goodwill, not a returned payment: the refund itself already counts the money as given back. Re-locked
            // first, which reloads the account: the seam's credit share went onto its balance in raw SQL.
            if (account is not null)
            {
                await creditAccountRepository.LockForUserAsync(order.UserId!, cancellationToken);
            }

            account?.Issue(
                amount: refund.Amount,
                reason: CreditTransactionReason.Goodwill,
                idempotencyKey: $"{refund.RefundKey}:credit",
                issuedBy: adminId,
                orderId: order.Id);

            // Last: the clawback flushes the unit of work. A cancellation's points went with the cancel itself.
            var origin = refund.MapToOrigin();
            if (recorded is not null && origin != RefundOrigin.Cancellation)
            {
                await loyaltyService.RevokeForRefundAsync(
                    order.Id,
                    origin == RefundOrigin.FullRefund ? order.TotalPrice : recorded.Amount + recorded.CreditReturned,
                    recorded.RefundKey,
                    adminId,
                    cancellationToken);
            }

            return BusinessResult.Success(new Response(refund.Id, refund.MapToState().MapToCode()));
        }
    }
}
