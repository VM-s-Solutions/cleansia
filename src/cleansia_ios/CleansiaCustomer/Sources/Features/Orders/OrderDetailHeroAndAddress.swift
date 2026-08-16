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
            Text(address.street ?? "—")
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurface)
            if !cityZip.isBlank {
                Text(cityZip)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            if let country = address.country, !country.isBlank {
                Text(country)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
    }
}
