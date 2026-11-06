import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderAddressCard: View {
    let address: OrderAddress

    private var cityZip: String {
        [address.zipCode, address.city]
            .compactMap { $0?.isBlank == false ? $0 : nil }
            .joined(separator: " ")
    }

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.address, systemImage: "mappin.and.ellipse")
            let locality = [cityZip, address.country ?? ""].filter { !$0.isBlank }.joined(separator: " · ")
            if let street = address.street, !street.isBlank {
                (Text(street)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface) + Text(locality.isBlank ? "" : " · " + locality)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant))
                    .fixedSize(horizontal: false, vertical: true)
            } else {
                Text(locality.isBlank ? "—" : locality)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
    }
}
