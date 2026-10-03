import CleansiaCore
import SwiftUI

/// The customer's credit balance, one row per currency held. At zero it collapses to one line that says
/// where credit would come from, so the card is never a big "0 Kč" that reads as a lost reward. The share
/// and the expiry are the server's numbers, never copy of their own.
struct CreditCard: View {
    @Environment(\.locale) private var locale
    let credit: CustomerCredit
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            RewardsCard {
                HStack(alignment: held.isEmpty ? .center : .top, spacing: Spacing.s) {
                    CreditGlyph(systemImage: "wallet.pass")
                    VStack(alignment: .leading, spacing: Spacing.xxs) {
                        if held.isEmpty {
                            Text(L10n.Credit.none)
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        } else {
                            Text(L10n.Credit.yourCredit)
                                .font(CleansiaTypography.titleMedium)
                                .foregroundColor(CleansiaColors.onBackground)
                            ForEach(held, id: \.currencyCode) { balance in
                                Text(OrdersFormat.price(balance.amount, currencyCode: balance.currencyCode))
                                    .cleansiaFont(CleansiaTypography.headlineSmall)
                                    .fontWeight(.bold)
                                    .foregroundColor(CleansiaColors.onSurface)
                                if let line = CreditExpiry.line(balance, locale: locale) {
                                    Text(line)
                                        .font(CleansiaTypography.labelMedium)
                                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                                }
                            }
                            Text(L10n.Credit.autoAppliedShare(credit.maxShareOfOrder))
                                .font(CleansiaTypography.labelMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .padding(.top, Spacing.xxs)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    Image(systemName: "chevron.right")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .padding(.top, held.isEmpty ? 0 : Spacing.xs)
                }
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private var held: [CustomerCredit.Balance] {
        credit.heldBalances
    }
}

/// What credit is, where it comes from and how it is spent — opened from the Rewards card and from the
/// Profile row, so both explain it in the same words. Ends on the largest balance's expiry, which the
/// Profile row does not show.
///
/// Sized to what it holds, not to the medium detent: on iOS 16–18 a medium sheet left "Got it" under the
/// home indicator, cut off. The button is pinned below the scrolling text, so at a large text size the
/// text scrolls and the button stays in view; `.large` is there for when the text outgrows the screen.
struct CreditExplainerSheet: View {
    @Environment(\.locale) private var locale
    let credit: CustomerCredit
    let onDismiss: () -> Void

    @State private var contentHeight: CGFloat = 320
    @State private var buttonHeight: CGFloat = 80

    var body: some View {
        ScrollView {
            content
                .background(GeometryReader { proxy in
                    Color.clear
                        .onAppear { contentHeight = proxy.size.height }
                        .onChange(of: proxy.size.height) { contentHeight = $0 }
                })
        }
        .safeAreaInset(edge: .bottom, spacing: 0) {
            CleansiaPrimaryButton(L10n.Credit.gotIt, action: onDismiss)
                .padding(.horizontal, Spacing.l)
                .padding(.top, Spacing.xs)
                .padding(.bottom, Spacing.s)
                .background(CleansiaColors.surface)
                .background(GeometryReader { proxy in
                    Color.clear
                        .onAppear { buttonHeight = proxy.size.height }
                        .onChange(of: proxy.size.height) { buttonHeight = $0 }
                })
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .presentationDetents([.height(contentHeight + buttonHeight), .large])
        .presentationDragIndicator(.visible)
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            Text(L10n.Credit.explainerTitle)
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onSurface)
            row(
                systemImage: "heart",
                title: L10n.Credit.explainerSourceTitle,
                body: L10n.Credit.explainerSourceBody
            )
            row(
                systemImage: "creditcard",
                title: L10n.Credit.explainerSpendTitle,
                body: L10n.Credit.explainerSpendBody(credit.maxShareOfOrder)
            )
            row(
                systemImage: "star",
                title: L10n.Credit.explainerPointsTitle,
                body: L10n.Credit.explainerPointsBody
            )
            if let line = CreditExpiry.line(credit.primary, locale: locale) {
                Text(line)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .padding([.horizontal, .top], Spacing.l)
        .padding(.bottom, Spacing.xs)
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func row(systemImage: String, title: String, body: String) -> some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            CreditGlyph(systemImage: systemImage)
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(body)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }
}

private struct CreditGlyph: View {
    let systemImage: String

    var body: some View {
        Image(systemName: systemImage)
            .font(.system(size: 17))
            .foregroundColor(CleansiaColors.primary)
            .frame(width: 36, height: 36)
            .background(CleansiaColors.primary.opacity(0.14), in: Circle())
            .accessibilityHidden(true)
    }
}

enum CreditExpiry {
    /// "Expires 12 Dec 2026. Every booking pushes that back." — nil when nothing is held to expire.
    static func line(_ balance: CustomerCredit.Balance, locale: Locale) -> String? {
        guard balance.amount > 0, let expiresOn = balance.expiresOn else { return nil }
        return L10n.Credit.expiresOn(expiresOn, locale: locale)
    }
}

#if DEBUG
    struct CreditViews_Previews: PreviewProvider {
        private static let held = CustomerCredit(
            primary: .init(amount: 250, currencyCode: "CZK", expiresOn: Date().addingTimeInterval(86400 * 300)),
            balances: [.init(amount: 250, currencyCode: "CZK", expiresOn: Date().addingTimeInterval(86400 * 300))],
            maxShareOfOrder: 0.7
        )

        static var previews: some View {
            VStack(spacing: Spacing.m) {
                CreditCard(credit: held, onTap: {})
                CreditCard(
                    credit: CustomerCredit(
                        primary: .init(amount: 0, currencyCode: "CZK", expiresOn: nil),
                        balances: [],
                        maxShareOfOrder: 0.7
                    ),
                    onTap: {}
                )
                CreditExplainerSheet(credit: held, onDismiss: {})
            }
            .padding(Spacing.ml)
            .background(CleansiaColors.background)
        }
    }
#endif
