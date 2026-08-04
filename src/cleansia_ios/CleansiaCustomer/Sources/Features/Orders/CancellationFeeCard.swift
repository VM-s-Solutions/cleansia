import CleansiaCore
import SwiftUI

/// How loudly the fee card is drawn. Carried as a token so a test can name it.
enum CancellationFeeSeverity: Equatable {
    case free
    case fee
    case lastMinute
}

/// Everything the fee card says, resolved from the server's tier and the server's amounts. `amounts` are
/// raw and unformatted — the view owns the currency. Nothing here reads `feeRate` or a clock: the ladder
/// that used to live in this feature disagreed with the one the customer was actually charged against,
/// and the tier exists so no client rebuilds it.
struct CancellationFeeCallout: Equatable {
    let titleKey: String
    let amountKey: String
    let amounts: [Double]
    let severity: CancellationFeeSeverity
    let warnsExpressWaiverForfeited: Bool
}

enum CancellationFeeCardModel: Equatable {
    case checking
    case unavailable
    case quoted(CancellationFeeCallout)

    init(_ state: UiState<CancellationQuote>) {
        switch state {
        case .loading: self = .checking
        case .error: self = .unavailable
        case let .loaded(quote): self = .quoted(CancellationFeeCallout(quote))
        }
    }
}

extension CancellationFeeCallout {
    init(_ quote: CancellationQuote) {
        switch quote.tier {
        case .freeNotAccepted:
            self.init(free: "order_cancel_fee_not_accepted", quote)
        case .freeOopsWindow:
            self.init(free: "order_cancel_fee_oops", quote)
        case .freeOutsideWindow:
            self.init(free: "order_cancel_fee_outside_window", quote)
        case .partial:
            self.init(charged: "order_cancel_fee_partial", severity: .fee, quote)
        case .lastMinute:
            self.init(charged: "order_cancel_fee_last_minute", severity: .lastMinute, quote)
        }
    }

    private init(free titleKey: String, _ quote: CancellationQuote) {
        self.init(
            titleKey: titleKey,
            amountKey: "order_cancel_fee_none",
            amounts: [],
            severity: .free,
            warnsExpressWaiverForfeited: quote.forfeitsExpressWaiver
        )
    }

    private init(charged titleKey: String, severity: CancellationFeeSeverity, _ quote: CancellationQuote) {
        self.init(
            titleKey: titleKey,
            amountKey: "order_cancel_fee_split",
            amounts: [quote.feeAmount, quote.refundAmount],
            severity: severity,
            warnsExpressWaiverForfeited: quote.forfeitsExpressWaiver
        )
    }
}

extension CancellationFeeSeverity {
    var tint: Color {
        switch self {
        case .free: CleansiaColors.primary
        case .fee: CleansiaColors.warningStar
        case .lastMinute: CleansiaColors.error
        }
    }

    var symbol: String {
        switch self {
        case .free: "checkmark.circle"
        case .fee, .lastMinute: "exclamationmark.triangle"
        }
    }
}

struct CancellationFeeCard: View {
    let model: CancellationFeeCardModel
    let currencyCode: String?
    let onRetry: () -> Void

    var body: some View {
        switch model {
        case .checking:
            FeeCardRow(tint: CleansiaColors.onSurfaceVariant, title: L10n.OrderCancel.feeChecking)
        case .unavailable:
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                FeeCardRow(
                    tint: CleansiaColors.onSurfaceVariant,
                    symbol: "exclamationmark.triangle",
                    title: L10n.OrderCancel.feeNeutral,
                    subtitle: L10n.OrderCancel.feeUnavailable
                )
                CleansiaTextLink(L10n.OrderCancel.feeRetry, action: onRetry)
            }
        case let .quoted(callout):
            VStack(alignment: .leading, spacing: Spacing.xs) {
                FeeCardRow(
                    tint: callout.severity.tint,
                    symbol: callout.severity.symbol,
                    title: L10n.localized(callout.titleKey),
                    subtitle: amountText(callout)
                )
                if callout.warnsExpressWaiverForfeited {
                    ExpressWaiverWarning()
                }
                Text(L10n.OrderCancel.feeRecheckNote)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
    }

    private func amountText(_ callout: CancellationFeeCallout) -> String {
        let formatted = callout.amounts.map { OrdersFormat.price($0, currencyCode: currencyCode) }
        return formatted.isEmpty
            ? L10n.localized(callout.amountKey)
            : L10n.format(callout.amountKey, arguments: formatted)
    }
}

/// ADR-0035 AM-13 — the forfeiture is invisible exactly where the fee is zero, so it is disclosed before
/// the customer confirms rather than after.
private struct ExpressWaiverWarning: View {
    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: "info.circle")
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.warningStar)
            Text(L10n.OrderCancel.expressWaiverForfeit)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Spacer(minLength: 0)
        }
    }
}

private struct FeeCardRow: View {
    let tint: Color
    var symbol: String?
    let title: String
    var subtitle: String?

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Group {
                if let symbol {
                    Image(systemName: symbol)
                        .font(.system(size: 20))
                        .foregroundColor(tint)
                } else {
                    ProgressView().tint(tint)
                }
            }
            .frame(width: 24, height: 24)

            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let subtitle {
                    Text(subtitle)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(Spacing.s)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(tint.opacity(0.10), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(tint.opacity(0.35), lineWidth: 1)
        )
    }
}

#if DEBUG
    struct CancellationFeeCard_Previews: PreviewProvider {
        private static let quote = CancellationQuote(
            tier: .partial,
            feeAmount: 250,
            refundAmount: 750,
            currencyCode: "CZK",
            forfeitsExpressWaiver: true
        )

        static var previews: some View {
            VStack(spacing: Spacing.s) {
                CancellationFeeCard(model: .checking, currencyCode: "CZK") {}
                CancellationFeeCard(model: .unavailable, currencyCode: "CZK") {}
                CancellationFeeCard(model: CancellationFeeCardModel(.loaded(quote)), currencyCode: "CZK") {}
            }
            .padding(Spacing.l)
        }
    }
#endif
