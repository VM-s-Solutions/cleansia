import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// Which pot a discount came out of. The token, not the resolved sentence, so
/// both the hero chips and the breakdown lines name the same thing and a test
/// can assert the mapping without hunting a literal in a view.
enum OrderDiscountSource: Hashable {
    case tier
    case membership
    case promo

    /// The chip row under the hero price, keyed on the order's single
    /// `AppliedDiscountSource` code (Android `HeroCard`). Distinct from the
    /// breakdown lines below, which are keyed on the per-source *amounts*.
    static func chips(for source: AppliedDiscountSource?) -> [OrderDiscountSource] {
        switch source {
        case ._1: [.tier]
        case ._2: [.membership]
        case ._3: [.promo]
        case ._4: [.membership, .tier]
        default: []
        }
    }
}

/// Backend `PaymentType`: Cash = 1, Card = 2.
enum OrderPaymentMethod: Equatable {
    case cash
    case card
    case named(String)
}

/// Backend `PaymentStatus`: Pending = 1 … Disputed = 5. PartiallyRefunded (6)
/// has no label on either platform and falls through to the wire name.
enum OrderPaymentStatus: Equatable {
    case pending
    case paid
    case failed
    case refunded
    case disputed
    case named(String)
}

/// What the customer is charged and how (Android `PriceBreakdownCard`).
///
/// The per-source discount amounts ride the wire on every order but were
/// rendered nowhere, so a Plus member on a loyalty tier saw a struck-through
/// subtotal and no account of where the difference went. Rendering only the
/// non-zero sources is the point: at the top tier Plus adds nothing on top, and
/// a missing membership line is the only way the app says so.
struct OrderPriceBreakdown: Equatable {
    struct DiscountLine: Equatable {
        let source: OrderDiscountSource
        let amount: Double
    }

    let subtotal: Double?
    let discounts: [DiscountLine]
    let total: Double
    let paymentMethod: OrderPaymentMethod?
    let paymentStatus: OrderPaymentStatus?
    let currencyCode: String?

    static func resolve(_ order: CustomerOrderDetail) -> OrderPriceBreakdown {
        let original = order.originalSubtotal
        return OrderPriceBreakdown(
            subtotal: original > 0 && original != order.total ? original : nil,
            discounts: [
                line(.tier, order.tierDiscountAmount),
                line(.membership, order.membershipDiscountAmount),
                line(.promo, order.promoDiscountAmount)
            ].compactMap { $0 },
            total: order.total,
            paymentMethod: paymentMethod(order.paymentType),
            paymentStatus: paymentStatus(order.paymentStatus),
            currencyCode: order.currencyCode
        )
    }

    private static func line(_ source: OrderDiscountSource, _ amount: Double?) -> DiscountLine? {
        guard let amount, amount > 0 else { return nil }
        return DiscountLine(source: source, amount: amount)
    }

    private static func paymentMethod(_ code: Code?) -> OrderPaymentMethod? {
        switch code?.value {
        case 1: .cash
        case 2: .card
        default: code?.name.flatMap { $0.isBlank ? nil : .named($0) }
        }
    }

    private static func paymentStatus(_ code: Code?) -> OrderPaymentStatus? {
        switch code?.value {
        case 1: .pending
        case 2: .paid
        case 3: .failed
        case 4: .refunded
        case 5: .disputed
        default: code?.name.flatMap { $0.isBlank ? nil : .named($0) }
        }
    }
}

/// The headline facts about an order: the code the cleaner asks for at the
/// door, the price, and the struck-through subtotal when something came off it.
/// Read by both heroes — the static card renders them inline, the live progress
/// card renders none of them and `OrderHeroFactsStrip` carries them instead.
struct OrderHeroFacts: Equatable {
    let confirmationCode: String?
    let total: Double
    let struckSubtotal: Double?
    let discountChips: [OrderDiscountSource]
    let currencyCode: String?

    static func resolve(_ order: CustomerOrderDetail) -> OrderHeroFacts {
        let original = order.originalSubtotal
        let discounted = order.appliedDiscountSource.map { $0 != ._0 } ?? false
        return OrderHeroFacts(
            confirmationCode: order.confirmationCode.flatMap { $0.isBlank ? nil : $0 },
            total: order.total,
            struckSubtotal: discounted && original > order.total ? original : nil,
            discountChips: OrderDiscountSource.chips(for: order.appliedDiscountSource),
            currencyCode: order.currencyCode
        )
    }
}

struct OrderPriceBreakdownCard: View {
    let order: CustomerOrderDetail

    private var breakdown: OrderPriceBreakdown {
        OrderPriceBreakdown.resolve(order)
    }

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.priceBreakdown, systemImage: "creditcard")
            if let subtotal = breakdown.subtotal {
                OrderInfoRow(
                    label: L10n.OrderDetail.subtotal,
                    value: OrdersFormat.price(subtotal, currencyCode: breakdown.currencyCode)
                )
            }
            ForEach(breakdown.discounts, id: \.source) { discount in
                OrderInfoRow(
                    label: L10n.OrderDetail.discountLabel(discount.source),
                    value: "−" + OrdersFormat.price(discount.amount, currencyCode: breakdown.currencyCode)
                )
            }
            Divider().background(CleansiaColors.outlineVariant)
            HStack {
                Text(L10n.OrderDetail.total)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                Text(OrdersFormat.price(breakdown.total, currencyCode: breakdown.currencyCode))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.primary)
            }
            if let method = breakdown.paymentMethod {
                OrderInfoRow(
                    label: L10n.OrderDetail.paymentMethod,
                    value: L10n.OrderDetail.paymentMethodLabel(method)
                )
            }
            if let status = breakdown.paymentStatus {
                OrderInfoRow(
                    label: L10n.OrderDetail.paymentStatus,
                    value: L10n.OrderDetail.paymentStatusLabel(status)
                )
            }
        }
    }
}

/// The facts the live progress hero leaves out. Android fills the same gap with
/// `OrderMetaStrip(showFacts = liveHero)`; here the pinned
/// `OrderDetailCompactHeader` already carries the order number and schedule, so
/// this strip is the remainder — and it is the only route to the confirmation
/// code for the three statuses in which a cleaner is on the way.
struct OrderHeroFactsStrip: View {
    let order: CustomerOrderDetail

    private var facts: OrderHeroFacts {
        OrderHeroFacts.resolve(order)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                if let code = facts.confirmationCode {
                    Text(L10n.OrderDetail.codeLabel)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(code)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
                Spacer(minLength: Spacing.xs)
                Text(OrdersFormat.price(facts.total, currencyCode: facts.currencyCode))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.primary)
                if let struck = facts.struckSubtotal {
                    Text(OrdersFormat.price(struck, currencyCode: facts.currencyCode))
                        .font(CleansiaTypography.labelSmall)
                        .strikethrough()
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }

            // What the struck-through price is explained by. The breakdown card further down names each
            // source with its amount, but that is several screens of scrolling away from the number it
            // accounts for — the chip answers "why is this cheaper" where the question is asked.
            if facts.struckSubtotal != nil, !facts.discountChips.isEmpty {
                HStack(spacing: Spacing.xs) {
                    ForEach(facts.discountChips, id: \.self) { source in
                        OrderDiscountChip(label: L10n.OrderDetail.discountLabel(source))
                    }
                }
            }
        }
    }
}

struct OrderDiscountChip: View {
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

#if DEBUG
    struct OrderDetailSummary_Previews: PreviewProvider {
        private static let order = try? CustomerOrderDetail(OrderItem(
            rooms: 3,
            bathrooms: 1,
            paymentType: Code(value: 2),
            paymentStatus: Code(value: 2),
            totalPrice: 1590,
            originalSubtotal: 2100,
            appliedDiscountSource: ._4,
            tierDiscountAmount: 210,
            membershipDiscountAmount: 300,
            estimatedTime: 180,
            confirmationCode: "CLN-12345",
            currency: CurrencyDetailDto(code: "CZK")
        ))

        static var previews: some View {
            VStack(spacing: Spacing.s) {
                if let order {
                    OrderHeroFactsStrip(order: order)
                    OrderPriceBreakdownCard(order: order)
                }
            }
            .padding(Spacing.ml)
            .background(CleansiaColors.background)
        }
    }
#endif
