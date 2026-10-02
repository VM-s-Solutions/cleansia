import CleansiaCore
import SwiftUI

struct MembershipSuccessScreen: View {
    /// The one perk on this list the plan may not carry, so it comes from the server rather than
    /// from the layout.
    let showExpressPerk: Bool
    let copy: MembershipCopy
    let onSetupRecurring: () -> Void
    let onBackHome: () -> Void

    /// The booking confirmation's treatment (C16): no 200 pt mascot but a 48 pt check, in Android's compact
    /// rhythm, centred when it fits — as Android's screen is — and scrolling when it does not.
    var body: some View {
        CenteredAuthScroll {
            VStack(spacing: Spacing.s) {
                // Decorative: the title under it says the same thing.
                Image(systemName: "checkmark.circle.fill")
                    .font(.system(size: 48))
                    .foregroundColor(CleansiaColors.successText)
                    .cleansiaBounceOnAppear()
                    .accessibilityHidden(true)
                VStack(spacing: Spacing.xxs) {
                    Text(L10n.Membership.successTitle)
                        .cleansiaFont(CleansiaTypography.headlineMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                        .multilineTextAlignment(.center)
                    Text(copy.successSubtitle)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)
                }

                VStack(alignment: .leading, spacing: Spacing.s) {
                    Text(L10n.Membership.successPerksHeader)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    PerkRow(text: L10n.Membership.perkDiscountTitle)
                    PerkRow(text: L10n.Membership.perkCancellationTitle)
                    PerkRow(text: L10n.Membership.perkFavoriteCleanerTitle)
                    PerkRow(text: L10n.Membership.perkRecurringTitle)
                    if showExpressPerk {
                        PerkRow(text: L10n.Membership.successPerkExpress)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(Spacing.m)
                .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))

                VStack(spacing: Spacing.xs) {
                    CleansiaPrimaryButton(
                        L10n.Membership.successCtaSetupRecurring,
                        leadingIcon: "repeat",
                        action: onSetupRecurring
                    )
                    CleansiaOutlinedButton(L10n.Membership.successCtaBackHome, action: onBackHome)
                }
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.vertical, Spacing.m)
        }
        .navigationBarBackButtonHidden(true)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

private struct PerkRow: View {
    let text: String

    var body: some View {
        HStack(spacing: Spacing.s) {
            Image(systemName: "checkmark.circle.fill")
                .foregroundColor(CleansiaColors.primary)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }
}

#if DEBUG
    struct MembershipSuccessScreen_Previews: PreviewProvider {
        static var previews: some View {
            MembershipSuccessScreen(
                showExpressPerk: true,
                copy: MembershipCopy(nil),
                onSetupRecurring: {},
                onBackHome: {}
            )
            .background(CleansiaColors.background)
        }
    }
#endif
