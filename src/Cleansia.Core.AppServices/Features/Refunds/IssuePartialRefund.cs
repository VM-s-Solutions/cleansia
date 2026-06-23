using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Refunds;

[AuditAction("order.refund.partial", Sensitive = true, ResourceType = "Order")]
public class IssuePartialRefund
{
    /// <summary>
    /// One line the admin chose to refund. A standalone service line sets <see cref="PackageId"/> null;
    /// a bundled line names both the package and the included service (its gross is derived as that
    /// service's weight-share of the package line, ADR-0231 / <see cref="PackagePricing"/>).
    /// </summary>
    public record RefundLineSelection(string ServiceId, string? PackageId);

    public record Command(
        string OrderId,
        IReadOnlyList<RefundLineSelection> Lines,
        RefundReason Reason,
        string? OverrideReason) : ICommand<Response>;

    public record Response(
        string OrderId,
        decimal RefundAmount,
        decimal RefundVat,
        PaymentStatus PaymentStatus,
        bool RefundInitiated,
        bool WindowOverridden);

    public record PartialRefundSnapshot(
        string OrderId,
        decimal OrderTotal,
        decimal ConsumedRefund,
        decimal RefundAmount,
        decimal RefundVat,
        PaymentStatus PaymentStatus);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.Lines)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.RefundLinesRequired)
                .Must(lines => lines.All(l => !string.IsNullOrEmpty(l.ServiceId)))
                .WithMessage(BusinessErrorMessage.RefundLineInvalid);

            RuleFor(x => x.Reason)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.OverrideReason)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IRefundRepository refundRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        IRefundService refundService,
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var consumedBefore = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);

            var windowOpen = RefundPolicy.IsWithinWindow(order.CompletedAt, DateTime.UtcNow);
            var windowOverridden = false;
            if (!windowOpen)
            {
                // ADR-0009 D1 — the window is SOFT: a closed window does not block the refund, it
                // requires a persisted non-empty override reason justifying the out-of-window decision.
                if (string.IsNullOrWhiteSpace(command.OverrideReason))
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.OverrideReason), BusinessErrorMessage.RefundOverrideReasonRequired));
                }

                windowOverridden = true;
            }

            var lineGrosses = BuildOrderLineGrosses(order);
            var selection = ResolveSelection(lineGrosses, command.Lines);
            if (selection is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.Lines), BusinessErrorMessage.RefundLineInvalid));
            }

            var allocation = RefundAllocator.Allocate(
                selection.Select(l => new RefundAllocationLine(l.Gross, l.Selected)).ToList(),
                order.TotalPrice,
                order.AppliedVatRate);

            var refundAmount = allocation.Where(a => a.Selected).Sum(a => a.RefundAmount);
            if (refundAmount <= 0m)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.Lines), BusinessErrorMessage.RefundNothingRefundable));
            }

            var feeAmount = await ResolveFeeAsync(order, refundAmount, command.Reason, cancellationToken);
            var sentAmount = refundAmount - feeAmount;
            if (sentAmount < 0m)
            {
                sentAmount = 0m;
            }

            // Deterministic admin RefundKey purpose (ADR-0006 D3): the identity of the selected lines, so a
            // retry / double-submit of the SAME selection collapses on the one key (never a Guid/timestamp).
            var refundRequestId = BuildSelectionIdentity(command);
            var refund = await refundService.IssueRefundAsync(
                new RefundRequest(
                    order.Id,
                    sentAmount,
                    command.Reason,
                    actorId,
                    RefundRequestId: refundRequestId,
                    WindowOverrideReason: windowOverridden ? command.OverrideReason : null),
                cancellationToken);

            if (refund.IsFailure)
            {
                return BusinessResult.Failure<Response>(refund.Error!);
            }

            // ADR-0009 D2/D3 — VAT and the loyalty net both derive from the seam-CONFIRMED amount
            // (result.Amount, clamped to the refundable ceiling), never the pre-fee gross. Apportioned
            // once off the confirmed amount with the same rate/(100+rate) shape RefundAllocator uses.
            var result = refund.Value!;
            var refundVat = ApportionVat(result.Amount, order.AppliedVatRate);
            var refundNet = result.Amount - refundVat;
            await loyaltyService.RevokeForPartialRefundAsync(
                order.Id, refundNet < 0m ? 0m : refundNet, result.RefundKey, actorId, cancellationToken);

            var consumedAfter = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);
            var paymentStatus = consumedAfter >= order.TotalPrice
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded;

            auditContext.RecordChange(
                "Order",
                order.Id,
                new PartialRefundSnapshot(
                    order.Id, order.TotalPrice, consumedBefore, RefundAmount: 0m, RefundVat: 0m, order.PaymentStatus),
                new PartialRefundSnapshot(
                    order.Id, order.TotalPrice, consumedAfter, result.Amount, refundVat, paymentStatus),
                command.OverrideReason);

            logger.LogInformation(
                "Admin partial refund issued for order {OrderId}: {Amount} {Currency} ({Reason}); windowOverridden={WindowOverridden}.",
                order.Id, result.Amount, order.Currency.Code, command.Reason, windowOverridden);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                RefundAmount: result.Amount,
                RefundVat: refundVat,
                PaymentStatus: paymentStatus,
                RefundInitiated: true,
                WindowOverridden: windowOverridden));
        }

        // ADR-0009 D3 — fee bearer. The platform absorbs the non-refundable Stripe fee on
        // ServiceNotRendered/DisputeResolution; goodwill AdminDiscretion deducts it. The fee AMOUNT is the
        // order country's CountryConfiguration figure; a null country / null config / either-null figure
        // means fee 0 (fail-open for the customer — never throw, never deduct a guess).
        private async Task<decimal> ResolveFeeAsync(
            Order order, decimal refundAmount, RefundReason reason, CancellationToken cancellationToken)
        {
            if (RefundPolicy.PlatformAbsorbsStripeFee(reason))
            {
                return 0m;
            }

            var countryId = order.CustomerAddress?.CountryId;
            var config = countryId is null
                ? null
                : await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);

            if (config?.RefundStripeFeeRate is not { } rate || config.RefundStripeFixedFee is not { } fixedFee)
            {
                return 0m;
            }

            return Math.Round(refundAmount * (rate / 100m) + fixedFee, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal ApportionVat(decimal amount, decimal? appliedVatRate)
        {
            if (appliedVatRate is not { } rate || rate <= 0m)
            {
                return 0m;
            }

            return Math.Round(amount * rate / (100m + rate), 2, MidpointRounding.AwayFromZero);
        }
    }

    private sealed record LineGross(string Key, decimal Gross, string ServiceId, string? PackageId);

    private static List<LineGross> BuildOrderLineGrosses(Order order)
    {
        var lines = new List<LineGross>();

        foreach (var service in order.SelectedServices)
        {
            // ADR-0009 D5.1 — the canonical quote basis (matches OrderPricingCalculator): a standalone
            // service's ratio weight is BasePrice + PerRoomPrice × (rooms + bathrooms). This is a weight
            // only; the allocator multiplies the line's share by frozen TotalPrice, so discount/surcharge
            // stay embedded (D2 — never re-applied).
            var gross = (service.Service?.BasePrice ?? 0m)
                + (service.Service?.PerRoomPrice ?? 0m) * (order.Rooms + order.Bathrooms);
            lines.Add(new LineGross($"svc:{service.ServiceId}", gross, service.ServiceId, PackageId: null));
        }

        foreach (var orderPackage in order.SelectedPackages)
        {
            var package = orderPackage.Package;
            if (package is null)
            {
                continue;
            }

            var included = package.IncludedServices.ToList();
            if (included.Count == 0)
            {
                lines.Add(new LineGross($"pkg:{orderPackage.PackageId}", package.Price, ServiceId: string.Empty, orderPackage.PackageId));
                continue;
            }

            var grosses = PackagePricing.DeriveIncludedServiceGrosses(
                included.Select(s => s.PriceWeight).ToList(), package.Price);
            for (var i = 0; i < included.Count; i++)
            {
                lines.Add(new LineGross(
                    $"pkg:{orderPackage.PackageId}:svc:{included[i].ServiceId}",
                    grosses[i],
                    included[i].ServiceId,
                    orderPackage.PackageId));
            }
        }

        return lines;
    }

    private sealed record SelectedLine(decimal Gross, bool Selected);

    private static List<SelectedLine>? ResolveSelection(
        List<LineGross> lineGrosses, IReadOnlyList<RefundLineSelection> requested)
    {
        var selectedKeys = new HashSet<string>();
        foreach (var line in requested)
        {
            var match = lineGrosses.FirstOrDefault(l =>
                l.ServiceId == line.ServiceId && l.PackageId == line.PackageId);
            if (match is null)
            {
                return null;
            }

            selectedKeys.Add(match.Key);
        }

        return lineGrosses
            .Select(l => new SelectedLine(l.Gross, selectedKeys.Contains(l.Key)))
            .ToList();
    }

    private static string BuildSelectionIdentity(Command command)
    {
        var keys = command.Lines
            .Select(l => $"{l.PackageId}|{l.ServiceId}")
            .OrderBy(k => k, StringComparer.Ordinal);
        return $"{command.OrderId}:{string.Join(",", keys)}";
    }
}
