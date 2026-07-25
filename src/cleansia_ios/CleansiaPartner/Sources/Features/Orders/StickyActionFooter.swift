import CleansiaCore
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
    /// Raised instead of confirming inline: the cash confirmation is a `CleansiaDialog`, which is a
    /// full-screen scrim + card, so it belongs at the screen root — not nested inside this footer's
    /// bottom-pinned, surface-backed strip where it would be laid out inside a ~90pt-tall container.
    var onCashConfirmRequested: () -> Void = {}

    private func isBusy(_ orderAction: OrderAction) -> Bool {
        inFlightAction == orderAction
    }

    var body: some View {
        switch action {
        case .take:
            footer {
                SlideToConfirm(
                    idleLabel: L10n.Orders.slideToTake,
                    busyLabel: L10n.Orders.takingOrder,
                    isBusy: isBusy(.take),
                    onConfirm: { onConfirm(.take) }
                )
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
            // The only irreversible money action in the app: the server rejects a
            // second call and there is no un-collect endpoint, so it asks first.
            footer {
                CleansiaPrimaryButton(
                    L10n.Orders.markCashCollected,
                    loading: isBusy(.markCashCollected),
                    action: onCashConfirmRequested
                )
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
