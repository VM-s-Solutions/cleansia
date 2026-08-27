import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// The backdrop's one decision, matching the partner screen: is there a coordinate to point at?
///
/// Status used to enter into it — a Cancelled order was forced to nil because the visit never
/// happened. The job still had a place, and the placeholder read as a map that had failed to
/// load rather than as a decision. Owner ruling, 2026-08-27.
enum OrderDetailMap {
    static func coordinate(for order: CustomerOrderDetail) -> Coordinate? {
        guard let latitude = order.address?.latitude,
              let longitude = order.address?.longitude
        else { return nil }
        return Coordinate(latitude: latitude, longitude: longitude)
    }
}

struct OrderDetailMapBackdrop: View {
    let order: CustomerOrderDetail
    let mapProvider: MapProvider

    var body: some View {
        if let coordinate = OrderDetailMap.coordinate(for: order) {
            mapProvider.fullBleedMap(coordinate: coordinate)
                .accessibilityHidden(true)
        } else {
            // A bare full-bleed gradient is indistinguishable from a map that failed to load —
            // it was reported as exactly that — so the empty space has to say what it means.
            //
            // It used to say the STATUS, because a cancelled order was the only way to get here
            // and the status was the explanation. Cancelled orders keep their map now (owner
            // ruling 2026-08-27), so the only remaining way here is an order with no usable
            // coordinate, and the status would explain nothing. Same sentence as Android.
            ZStack {
                BrandGradient.blue.linearGradient
                VStack(spacing: Spacing.xs) {
                    Image(systemName: "mappin.slash")
                        .font(.system(size: 28, weight: .light))
                    Text(L10n.Orders.mapUnavailable)
                        .font(CleansiaTypography.labelLarge)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, Spacing.l)
                }
                .foregroundColor(CleansiaColors.onPrimary.opacity(0.75))
            }
            .accessibilityElement(children: .combine)
        }
    }
}
