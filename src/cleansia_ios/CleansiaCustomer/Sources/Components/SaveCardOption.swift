import CleansiaCore
import SwiftUI

/// A ticked card is saved under the card-guarantee consent the server records, so that sentence is shown
/// with the tick.
struct SaveCardOption: View {
    @Binding var saved: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            CleansiaConsentCheckbox(
                checked: $saved,
                markdown: L10n.Booking.saveCard,
                toggleAccessibilityLabel: L10n.Booking.saveCard
            )
            Text(L10n.Booking.cardGuaranteeConsent)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}
