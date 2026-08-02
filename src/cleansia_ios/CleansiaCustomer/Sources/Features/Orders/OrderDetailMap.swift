import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// The backdrop's one decision, matching the partner screen's `canShowMap`: a
/// Completed order keeps its map (the cleaning happened there), a Cancelled one
/// loses it because the visit never happened.
enum OrderDetailMap {
    static func coordinate(for order: OrderItem) -> Coordinate? {
        guard order.status != ._6,
              let latitude = order.address?.latitude,
              let longitude = order.address?.longitude
        else { return nil }
        return Coordinate(latitude: latitude, longitude: longitude)
    }
}

struct OrderDetailMapBackdrop: View {
    let order: OrderItem
    let mapProvider: MapProvider

    var body: some View {
        if let coordinate = OrderDetailMap.coordinate(for: order) {
            mapProvider.fullBleedMap(coordinate: coordinate)
                .accessibilityHidden(true)
        } else {
            BrandGradient.blue.linearGradient
        }
    }
}
