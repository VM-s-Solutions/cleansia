import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The detail screen's primary action area — renders the resolved
/// `OrderPrimaryAction` (the shared machine) as the matching native confirm
/// control, with per-action busy state and the after-photos-blocked hint
/// (the `StickyActionFooter`/`OrderPrimaryAction.kt` parity). Renders nothing
/// when there is no action (terminal / not mine).
struct StickyActionFooter: View {
    let action: OrderPrimaryAction
    let inFlightAction: OrderAction?
    let onConfirm: (OrderPrimaryAction) -> Void
    /// Raised instead of confirming inline: the screen root (`OrderDetailContent`) owns the cash-collected
    /// confirmation, a native `.alert`, so the footer only asks for it.
    var onCashConfirmRequested: () -> Void = {}
    /// Shown under the cash collection only; its confirmation is the screen root's too.
    var offersCashNotPaid = false
    var onCashNotPaidRequested: () -> Void = {}
    /// The reservation held for this cleaner on this order. Present only where a hold exists, which is
    /// why the screen degrades to an ordinary job in the short-lead band the push also reaches.
    var preferredOffer: PendingOfferItem?
    var onDeclineOffer: () -> Void = {}

    private func isBusy(_ orderAction: OrderAction) -> Bool {
        inFlightAction == orderAction
    }

    var body: some View {
        switch action {
        case .take:
            footer {
                VStack(spacing: Spacing.xs) {
                    if let preferredOffer {
                        ReservedForYouRow(respondByUtc: preferredOffer.respondByUtc)
                    }
                    // Opens the contract sheet; the deliberate gesture sits under the text it
                    // accepts, and a slide that opened a second slide would ask twice. On a job
                    // reserved for them by name the sheet runs the same command with a different
                    // word: confirming IS taking. Spins while the host reconciles the order after
                    // the sheet's verdict, so it cannot reopen the sheet on a job already taken.
                    CleansiaPrimaryButton(
                        preferredOffer == nil ? L10n.Orders.takeOrder : L10n.Offers.confirm,
                        loading: isBusy(.take),
                        enabled: inFlightAction == nil,
                        action: { onConfirm(.take) }
                    )
                    if preferredOffer != nil {
                        CleansiaTextLink(L10n.Offers.decline, action: onDeclineOffer)
                            .disabled(inFlightAction != nil)
                    }
                }
            }
        case .notifyOnTheWay:
            footer {
                CleansiaPrimaryButton(
                    L10n.Orders.notifyOnTheWay,
                    loading: isBusy(.notifyOnTheWay),
                    action: { onConfirm(.notifyOnTheWay) }
                )
            }
        case .start:
            footer {
                SlideToConfirm(
                    idleLabel: L10n.Orders.slideToStart,
                    busyLabel: L10n.Orders.startingOrder,
                    isBusy: isBusy(.start),
                    onConfirm: { onConfirm(.start) }
                )
            }
        case .collectCash:
            // The two irreversible money answers in the app: the server rejects a second
            // call and neither can be taken back from here, so each asks first.
            footer {
                VStack(spacing: Spacing.xs) {
                    CleansiaPrimaryButton(
                        L10n.Orders.markCashCollected,
                        loading: isBusy(.markCashCollected),
                        enabled: inFlightAction == nil,
                        action: onCashConfirmRequested
                    )
                    if offersCashNotPaid {
                        CashNotPaidLink(
                            isReporting: isBusy(.reportCashNotPaid),
                            enabled: inFlightAction == nil,
                            onTap: onCashNotPaidRequested
                        )
                    }
                }
            }
        case .complete:
            footer {
                SlideToConfirm(
                    idleLabel: L10n.Orders.slideToComplete,
                    busyLabel: L10n.Orders.completingOrder,
                    isBusy: isBusy(.complete),
                    onConfirm: { onConfirm(.complete) }
                )
            }
        case let .completeBlocked(cashPending):
            footer { CompleteBlockedHint(cashPending: cashPending) }
        case .none:
            EmptyView()
        }
    }

    private func footer(@ViewBuilder _ control: () -> some View) -> some View {
        VStack(spacing: 0) {
            control()
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface)
    }
}

private struct CashNotPaidLink: View {
    let isReporting: Bool
    let enabled: Bool
    let onTap: () -> Void

    var body: some View {
        if isReporting {
            ProgressView()
                .progressViewStyle(.circular)
                .padding(Spacing.xxs)
        } else {
            CleansiaTextLink(L10n.Orders.cashNotPaidAction, action: onTap)
                .disabled(!enabled)
        }
    }
}

/// Disabled-state stand-in for the Complete slide when no "after" photo exists
/// yet — surfaces the server's after-photos guard early. With cash still owed the
/// photo is only the first of three steps, so the hint spells the sequence out
/// rather than letting the cleaner discover the cash button after uploading.
private struct CompleteBlockedHint: View {
    let cashPending: Bool

    private var text: String {
        cashPending ? L10n.Orders.completeBlockedCashSequence : L10n.Orders.afterPhotosRequired
    }

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "camera")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(text)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.leading)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.s)
        .padding(.horizontal, Spacing.m)
        .background(CleansiaColors.surfaceVariant, in: Capsule())
    }
}
