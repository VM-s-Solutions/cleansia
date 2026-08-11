import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderHeroCard: View {
    @Environment(\.locale) private var locale
    let order: OrderItem

    private var facts: OrderHeroFacts {
        OrderHeroFacts.resolve(order)
    }

    var body: some View {
        OrderCardSurface {
            if let code = facts.confirmationCode {
                HStack(alignment: .top) {
                    Text(L10n.OrderDetail.codeLabel)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    Text(code)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
            Text(OrdersFormat.dateRange(
                order.cleaningDateTime,
                estimatedMinutes: order.estimatedTime ?? 0,
                locale: locale
            ))
            .font(CleansiaTypography.headlineSmall)
            .foregroundColor(CleansiaColors.onBackground)

            HStack(alignment: .lastTextBaseline, spacing: Spacing.xs) {
                Text(OrdersFormat.price(facts.total, currencyCode: facts.currencyCode))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.primary)
                if let struck = facts.struckSubtotal {
                    Text(OrdersFormat.price(struck, currencyCode: facts.currencyCode))
                        .font(CleansiaTypography.titleMedium)
                        .strikethrough()
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }

            if facts.struckSubtotal != nil {
                HStack(spacing: Spacing.xs) {
                    ForEach(facts.discountChips, id: \.self) { source in
                        DiscountChip(label: L10n.OrderDetail.discountLabel(source))
                    }
                }
            }
        }
    }
}

private struct DiscountChip: View {
    let label: String

    var body: some View {
        Text(label)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.onSecondaryContainer)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, Spacing.xxs)
            .background(CleansiaColors.secondaryContainer, in: Capsule())
    }
}

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
