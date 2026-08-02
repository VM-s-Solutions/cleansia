import CleansiaCore
import SwiftUI

struct MembershipManagementCard: View {
    @ObservedObject var vm: MembershipViewModel
    @Environment(\.snackbarController) private var snackbar
    let onSubscribeClick: () -> Void

    @State private var showCancelDialog = false
    @State private var showSwitchDialog = false

    private var yearlyPlan: MembershipPlan? {
        vm.plans.first { $0.isAnnual }
    }

    private var showSwitchCta: Bool {
        guard let m = vm.current, m.hasMembership, !m.cancelRequested, m.billingInterval == 1 else { return false }
        return yearlyPlan != nil
    }

    var body: some View {
        content
            .overlay { dialogs }
    }

    @ViewBuilder
    private var content: some View {
        if let membership = vm.current {
            if !membership.hasMembership {
                InactiveCard(onClick: onSubscribeClick)
            } else {
                ActiveCard(
                    membership: membership,
                    cancelEnabled: !vm.submitState.isSubmitting && !membership.cancelRequested,
                    switchSavings: showSwitchCta ? Int(yearlyPlan?.savingsPercentVsMonthly ?? 0) : nil,
                    onCancel: { showCancelDialog = true },
                    onSwitch: { showSwitchDialog = true }
                )
            }
        }
    }

    @ViewBuilder
    private var dialogs: some View {
        if showCancelDialog {
            CleansiaDialog(
                title: L10n.Membership.cancelDialogTitle,
                confirmLabel: L10n.Membership.cancelDialogConfirm,
                onConfirm: confirmCancel,
                onDismiss: { showCancelDialog = false },
                message: L10n.Membership.cancelDialogMessage,
                dismissLabel: L10n.Membership.back,
                destructive: true
            )
        }
        if showSwitchDialog, let yearlyPlan {
            CleansiaDialog(
                title: L10n.Membership.switchDialogTitle,
                confirmLabel: L10n.Membership.switchDialogConfirm,
                onConfirm: { confirmSwitch(yearlyPlan) },
                onDismiss: { showSwitchDialog = false },
                message: L10n.Membership.switchDialogMessage(MembershipFormat.price(yearlyPlan.price)),
                dismissLabel: L10n.Membership.back
            )
        }
    }

    private func confirmCancel() {
        showCancelDialog = false
        Task {
            if let date = await vm.cancel() {
                snackbar.showSuccess(L10n.Membership.cancelledUntil(MembershipFormat.periodEnd(date)))
            }
        }
    }

    private func confirmSwitch(_ plan: MembershipPlan) {
        showSwitchDialog = false
        Task {
            if await vm.swapPlan(newPlanCode: plan.code) {
                snackbar.showSuccess(L10n.Membership.switchSuccess)
            }
        }
    }
}

private struct InactiveCard: View {
    let onClick: () -> Void

    var body: some View {
        Button(action: onClick) {
            VStack(alignment: .leading, spacing: Spacing.m) {
                HStack(alignment: .top, spacing: Spacing.xs) {
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        HStack(spacing: Spacing.xs) {
                            Image(systemName: "crown.fill")
                                .font(.system(size: 12))
                                .foregroundColor(CleansiaColors.onPrimary)
                            Text(L10n.Membership.inactiveBadge)
                                .font(CleansiaTypography.labelSmall)
                                .foregroundColor(CleansiaColors.onPrimary)
                        }
                        .padding(.horizontal, Spacing.s)
                        .padding(.vertical, 4)
                        .background(CleansiaColors.primary, in: RoundedRectangle(cornerRadius: CornerRadius.extraSmall))

                        Text(L10n.Membership.inactiveTitle)
                            .font(CleansiaTypography.titleLarge)
                            .foregroundColor(CleansiaColors.onSurface)
                        Text(L10n.Membership.inactivePerksSummary)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    Mascot.ready.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 72, height: 72)
                }

                HStack {
                    Text(L10n.Membership.inactiveCta)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onPrimary)
                    Spacer()
                    Image(systemName: "arrow.right")
                        .foregroundColor(CleansiaColors.onPrimary)
                }
                .padding(Spacing.m)
                .background(CleansiaColors.primary, in: RoundedRectangle(cornerRadius: CornerRadius.small))
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                CleansiaColors.primaryContainer.opacity(0.4),
                in: RoundedRectangle(cornerRadius: CornerRadius.large)
            )
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.primary.opacity(0.35), lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

private struct ActiveCard: View {
    let membership: MyMembership
    let cancelEnabled: Bool
    let switchSavings: Int?
    let onCancel: () -> Void
    let onSwitch: () -> Void

    private var accent: Color {
        membership.cancelRequested ? MembershipPalette.endingAccent : MembershipPalette.premiumGold
    }

    private var perks: [MembershipPerk] {
        MembershipPerks.resolve(membership)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            HStack(alignment: .top, spacing: Spacing.xs) {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    HStack(spacing: Spacing.xs) {
                        Image(systemName: "crown.fill").foregroundColor(accent)
                        Text(membership.cancelRequested
                            ? L10n.Membership.statusEndingBadge
                            : L10n.Membership.statusActiveBadge)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(accent)
                            .padding(.horizontal, Spacing.s)
                            .padding(.vertical, 3)
                            .background(
                                accent.opacity(0.15),
                                in: RoundedRectangle(cornerRadius: CornerRadius.extraSmall)
                            )
                    }
                    Text(membership.planName ?? L10n.Membership.plusTitle)
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                Mascot.ready.image
                    .resizable()
                    .scaledToFit()
                    .frame(width: 64, height: 64)
            }

            if !perks.isEmpty {
                ChipFlow(spacing: Spacing.xs) {
                    ForEach(perks) { perk in
                        PerkPill(perk: perk, accent: accent)
                    }
                }
                Divider().background(CleansiaColors.outlineVariant)
            }

            if let periodEnd = membership.currentPeriodEnd {
                let dateText = MembershipFormat.periodEnd(periodEnd)
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(membership.cancelRequested
                        ? L10n.Membership.activeUntil(dateText)
                        : L10n.Membership.renewsOn(dateText))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(membership.cancelRequested
                        ? L10n.Membership.thenEndsHint
                        : L10n.Membership.autoRenewHint)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }

            if let switchSavings {
                CleansiaOutlinedButton(L10n.Membership.switchToAnnualCta(switchSavings), action: onSwitch)
            }

            if cancelEnabled {
                Button(action: onCancel) {
                    Text(L10n.Membership.cancelAction)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.error)
                }
                .frame(maxWidth: .infinity)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(accent.opacity(0.35), lineWidth: 1)
        )
    }
}

private struct PerkPill: View {
    let perk: MembershipPerk
    let accent: Color

    var body: some View {
        HStack(spacing: 4) {
            Image(systemName: perk.systemImage)
                .font(.system(size: 12))
                .foregroundColor(accent)
            Text(perk.label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .padding(.horizontal, Spacing.s)
        .padding(.vertical, 6)
        .background(accent.opacity(0.10), in: Capsule())
    }
}

#if DEBUG
    struct MembershipActiveCard_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                ActiveCard(
                    membership: sample(cancelRequested: false),
                    cancelEnabled: true,
                    switchSavings: 15,
                    onCancel: {},
                    onSwitch: {}
                )
                .previewDisplayName("Active")
                ActiveCard(
                    membership: sample(cancelRequested: true),
                    cancelEnabled: false,
                    switchSavings: nil,
                    onCancel: {},
                    onSwitch: {}
                )
                .previewDisplayName("Ending")
            }
            .padding()
            .background(CleansiaColors.background)
        }

        private static func sample(cancelRequested: Bool) -> MyMembership {
            MyMembership(
                hasMembership: true,
                planCode: "plus_monthly",
                planName: "Cleansia Plus",
                discountPercentage: 5,
                freeCancellationWindowHours: 4,
                allowsExpressUpgrade: true,
                currentPeriodEnd: Date(timeIntervalSince1970: 1_780_000_000),
                cancelRequested: cancelRequested,
                billingInterval: 1
            )
        }
    }
#endif
