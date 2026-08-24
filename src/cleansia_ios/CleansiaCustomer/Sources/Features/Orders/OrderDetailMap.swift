import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// The backdrop's one decision, matching the partner screen's `canShowMap`: a
/// Completed order keeps its map (the cleaning happened there), a Cancelled one
/// loses it because the visit never happened.
enum OrderDetailMap {
    static func coordinate(for order: CustomerOrderDetail) -> Coordinate? {
        guard order.status != ._6,
              let latitude = order.address?.latitude,
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
            // Withholding the map is deliberate (see above), but a bare full-bleed gradient is
            // indistinguishable from a map that failed to load — it was reported as exactly that. The
            // status says what the empty space MEANS, so the absence reads as a decision.
            //
            // No new copy: L10n.Orders.statusLabel is the same localised string the sheet already
            // shows below, in all five languages.
            //
            // Called directly rather than through OrderStatusPresentation.label, which takes the
            // generated `Code` wrapper — `CustomerOrderDetail.status` is already an `OrderStatus?`,
            // so routing through it would mean wrapping a value that is one conditional away from
            // being exactly what the localiser wants.
            ZStack {
                BrandGradient.blue.linearGradient
                VStack(spacing: Spacing.xs) {
                    Image(systemName: "mappin.slash")
                        .font(.system(size: 28, weight: .light))
                    if let status = order.status {
                        Text(L10n.Orders.statusLabel(status))
                            .font(CleansiaTypography.labelLarge)
                    }
                }
                .foregroundColor(CleansiaColors.onPrimary.opacity(0.75))
            }
            .accessibilityElement(children: .combine)
        }
    }
}
