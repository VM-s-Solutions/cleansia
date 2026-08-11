import CleansiaCore
import SwiftUI

/// The reservation on the customer's own order: who was asked, or that the ask has ended. It never says
/// what the cleaner did, and the one forward-looking sentence it carries is conditional and about the
/// platform.
struct PreferredOfferCard: View {
    @Environment(\.locale) private var locale
    let disclosure: PreferredOfferDisclosure

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(
                title: L10n.PreferredOffer.sectionTitle,
                systemImage: disclosure.systemImage
            )
            Text(disclosure.headline)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            if let detail = disclosure.detail(locale: locale) {
                Text(detail)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }
}

#if DEBUG
    struct PreferredOfferCard_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.m) {
                PreferredOfferCard(disclosure: .asked(
                    cleanerName: "Jana",
                    respondBy: Date(timeIntervalSinceNow: 5400)
                ))
                PreferredOfferCard(disclosure: .accepted(cleanerName: "Jana"))
                PreferredOfferCard(disclosure: .closed)
            }
            .padding()
            .background(CleansiaColors.background)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
