import CleansiaCore
import SwiftUI

struct MembershipSuccessScreen: View {
    /// The one perk on this list the plan may not carry, so it comes from the server rather than
    /// from the layout.
    let showExpressPerk: Bool
    let onSetupRecurring: () -> Void
    let onBackHome: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.l) {
                AnimatedMascotView(.welcoming, loop: false, fallback: .waving)
                    .frame(width: 200, height: 200)
                    .padding(.top, Spacing.xl)
                Text(L10n.Membership.successTitle)
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                Text(L10n.Membership.successSubtitle)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)

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

                VStack(spacing: Spacing.s) {
                    CleansiaPrimaryButton(
                        L10n.Membership.successCtaSetupRecurring,
                        leadingIcon: "repeat",
                        action: onSetupRecurring
                    )
                    CleansiaOutlinedButton(L10n.Membership.successCtaBackHome, action: onBackHome)
                }
            }
            .padding(.horizontal, Spacing.ml)
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
            MembershipSuccessScreen(showExpressPerk: true, onSetupRecurring: {}, onBackHome: {})
                .background(CleansiaColors.background)
        }
    }
#endif
