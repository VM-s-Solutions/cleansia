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
            footer {
                CleansiaPrimaryButton(
                    L10n.Orders.markCashCollected,
                    loading: isBusy(.markCashCollected),
                    action: { onConfirm(.collectCash) }
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
        case .completeBlocked:
            footer { CompleteBlockedHint() }
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
/// yet — surfaces the server's after-photos guard early. Same copy as the
/// backend's after-photos error.
private struct CompleteBlockedHint: View {
    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "camera")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Orders.afterPhotosRequired)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surfaceVariant, in: Capsule())
    }
}
