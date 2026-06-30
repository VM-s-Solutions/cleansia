import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct OrderHeroCard: View {
    let order: OrderItem

    private var hasDiscount: Bool {
        guard let source = order.appliedDiscountSource, source != ._0 else { return false }
        return (order.originalSubtotal ?? 0) > (order.totalPrice ?? 0)
    }

    var body: some View {
        OrderCardSurface {
            HStack(alignment: .top) {
                OrderStatusPill(
                    label: OrderStatusPresentation.label(order.orderStatus),
                    color: OrderStatusPresentation.color(order.orderStatus)
                )
                Spacer()
                if let code = order.confirmationCode, !code.isBlank {
                    VStack(alignment: .trailing, spacing: 0) {
                        Text(L10n.OrderDetail.codeLabel)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        Text(code)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                    }
                }
            }
            Text(OrdersFormat.dateRange(order.cleaningDateTime, estimatedMinutes: order.estimatedTime ?? 0))
                .font(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onBackground)

            HStack(alignment: .lastTextBaseline, spacing: Spacing.xs) {
                Text(OrdersFormat.price(order.totalPrice ?? 0, currencyCode: order.currency?.code))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.primary)
                if hasDiscount {
                    Text(OrdersFormat.price(order.originalSubtotal ?? 0, currencyCode: order.currency?.code))
                        .font(CleansiaTypography.titleMedium)
                        .strikethrough()
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }

            if hasDiscount {
                discountChips
            }
        }
    }

    private var discountChips: some View {
        HStack(spacing: Spacing.xs) {
            switch order.appliedDiscountSource {
            case ._1:
                DiscountChip(label: L10n.OrderDetail.discountTier)
            case ._2:
                DiscountChip(label: L10n.OrderDetail.discountMembership)
            case ._3:
                DiscountChip(label: L10n.OrderDetail.discountPromo)
            case ._4:
                DiscountChip(label: L10n.OrderDetail.discountMembership)
                DiscountChip(label: L10n.OrderDetail.discountTier)
            default:
                EmptyView()
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
