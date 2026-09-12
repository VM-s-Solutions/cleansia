using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.AppServices.Services;
using Cleansia.Infra.Common.Validations;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
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
        IExtraRepository extraRepository,
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

            var lineGrosses = await BuildOrderLineGrossesAsync(order, extraRepository, cancellationToken);
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
            // Against what the CARD was charged, not the sale — see AdminRefundOrder for why.
            var paymentStatus = consumedAfter >= RefundService.CardChargedAmount(order)
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

            // The FIXED part is a number in the COUNTRY's currency (6 on the CZE row means 6 CZK). The
            // caller names the order's currency and the address names the country, and nothing ties the
            // two — so on a CZ-address order priced in EUR it would be deducted as 6 EUR. The rate is
            // unit-free and still applies; the fixed part is absorbed, the same fail-open direction a
            // null figure already takes. → /product/business-rules#money-constants
            var fixedPart = string.Equals(
                order.Currency?.Code, config.DefaultCurrencyCode, StringComparison.OrdinalIgnoreCase)
                ? fixedFee
                : 0m;

            return Math.Round(refundAmount * (rate / 100m) + fixedPart, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal ApportionVat(decimal amount, decimal? appliedVatRate)
        {
            if (appliedVatRate is not { } rate || rate <= 0m)
            {
                return 0m;
            }

            // Fraction, not percent — Order.AppliedVatRate is copied from
            // CountryConfiguration.StandardVatRate, a numeric(5,4) column that cannot hold 21.
            // See VatCalculator for the full note; the two must agree or a credit note declares a
            // different VAT than the invoice it reverses.
            return Math.Round(amount * rate / (1m + rate), 2, MidpointRounding.AwayFromZero);
        }
    }

    private sealed record LineGross(string Key, decimal Gross, string ServiceId, string? PackageId);

    private static async Task<List<LineGross>> BuildOrderLineGrossesAsync(
        Order order, IExtraRepository extraRepository, CancellationToken cancellationToken)
    {
        var lines = new List<LineGross>();

        foreach (var service in order.SelectedServices)
        {
            // ADR-0009 D5.1 — the canonical quote basis: a standalone service's ratio weight is
            // BasePrice + PerRoomPrice × (rooms + bathrooms). That arithmetic now happens ONCE, at order
            // creation, and is frozen in LineTotal. This used to recompute it from the LIVE catalogue,
            // so an admin price edit moved the denominator of a refund on an order placed months
            // earlier. The `?? 0m` is gone with it: a fail-open zero silently shrank the denominator and
            // over-paid every other line.
            //
            // It remains a weight only; the allocator multiplies the line's share by frozen TotalPrice,
            // so discount and surcharge stay embedded (D2 — never re-applied).
            lines.Add(new LineGross(
                $"svc:{service.ServiceId}", service.LineTotal, service.ServiceId, PackageId: null));
        }

        foreach (var orderPackage in order.SelectedPackages)
        {
            // The order's own split, not a fresh one derived from live weights. Both the weights and
            // the package's composition are editable through the admin package form, so re-deriving
            // here moved a historical order's bundled shares whenever either changed.
            var included = orderPackage.IncludedServiceLines.ToList();
            if (included.Count == 0)
            {
                lines.Add(new LineGross(
                    $"pkg:{orderPackage.PackageId}",
                    orderPackage.LineTotal,
                    ServiceId: string.Empty,
                    orderPackage.PackageId));
                continue;
            }

            foreach (var line in included)
            {
                lines.Add(new LineGross(
                    $"pkg:{orderPackage.PackageId}:svc:{line.ServiceId}",
                    line.LineGross,
                    line.ServiceId,
                    orderPackage.PackageId));
            }
        }

        // EXTRAS BELONG IN THE DENOMINATOR. OrderPricingCalculator sums packages + services + EXTRAS
        // into the subtotal that becomes TotalPrice, and the allocator multiplies each line's share of
        // this list by that frozen TotalPrice. Omitting extras made the denominator smaller than the
        // numerator it divides into, so every share was inflated: on a 1000 service + 200 extras
        // order, refunding the one service paid back 1000/1000 × 1200 = the whole 1200. Every partial
        // refund on an order carrying an extra over-paid, in proportion to the extras.
        //
        // They are NOT selectable, and that is deliberate rather than an omission. RefundLineSelection
        // is (ServiceId, PackageId?) and has no way to name an extra, so adding one would change the
        // wire contract and the admin picker — and it would buy nothing today, because a FULL refund
        // does not come through here. AdminRefundOrder takes a plain amount, and the terminal
        // PaymentStatus is computed from GetSucceededRefundTotalForOrderAsync, which is cumulative
        // across every refund path. So "refund everything" still works and still lands on Refunded.
        //
        // Weights only, like the service lines above — but read from the ORDER'S OWN ROWS, not the live
        // catalogue. This used to query Extras by slug at refund time, which meant an admin price edit
        // moved the denominator of a refund on an order placed months earlier. It also queried WITHOUT
        // an IsActive filter while the pricing calculator applied one, so an extra deactivated after
        // ordering was excluded from TotalPrice and still counted here — inflating every other line's
        // share on any order carrying one. Both are gone: there is one list, and the order owns it.
        foreach (var extra in order.SelectedExtras)
        {
            lines.Add(new LineGross(
                $"extra:{extra.Slug}", extra.UnitPrice, ServiceId: string.Empty, PackageId: null));
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

    /// <summary>
    /// A short, stable fingerprint of the chosen lines — the thing that makes two different partial
    /// refunds on one order two different refunds.
    /// <para>
    /// It used to be the raw selection: the order id followed by every "packageId|serviceId" pair.
    /// With 26-character ULIDs that is 94 characters for one standalone line, exactly 120 for one
    /// bundled line, and 122 or more for ANY two — against a <c>RefundKey</c> column of
    /// <c>varchar(120)</c>. Picking a second line failed on the insert with a Postgres 22001, before
    /// Stripe was ever called, as a 500 with no explanation. "Refund the two rooms that were skipped"
    /// is the first thing anyone asks of this screen.
    /// </para>
    /// <para>
    /// SHA-256 over the same ordered material, truncated to 16 hex characters. Still deterministic on
    /// the domain inputs and still never a Guid or a timestamp, so the retry and double-issue
    /// collapse that <c>RefundService.BuildRefundKey</c> depends on is unchanged — only the length
    /// is. Truncation is safe here because this is not a security boundary: it distinguishes
    /// selections within ONE order, where a collision needs two different subsets of the same order's
    /// handful of lines to share 64 bits.
    /// </para>
    /// </summary>
    private static string BuildSelectionIdentity(Command command)
    {
        var keys = command.Lines
            .Select(l => $"{l.PackageId}|{l.ServiceId}")
            .OrderBy(k => k, StringComparer.Ordinal);
        var material = $"{command.OrderId}:{string.Join(",", keys)}";

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(digest, 0, 8).ToLowerInvariant();
    }
}
