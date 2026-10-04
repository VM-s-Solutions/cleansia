import Foundation

/// Insurance is claimed only with the ceiling the market authored, formatted on the device in that
/// market's currency; a cleaner needs no insurance to be approved, so without a ceiling nothing is
/// claimed at all. A locale string never carries a money figure or a currency name of its own.
enum InsuranceCopy {
    static func trustBadge(_ insurance: MarketMoney?) -> String? {
        insurance.map { L10n.Booking.trustInsured(OrdersFormat.price($0.amount, currencyCode: $0.currencyCode)) }
    }

    static func faqAnswer(_ insurance: MarketMoney?) -> String? {
        insurance.map { L10n.Help.faqA3(OrdersFormat.price($0.amount, currencyCode: $0.currencyCode)) }
    }
}
