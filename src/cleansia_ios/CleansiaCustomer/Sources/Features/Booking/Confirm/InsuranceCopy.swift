import Foundation

/// The insurance claim is stated with the ceiling the market authored, formatted on the device in
/// that market's currency, and without a ceiling when none is authored — a locale string never
/// carries a money figure or a currency name of its own.
enum InsuranceCopy {
    static func trustBadge(_ insurance: MarketMoney?) -> String {
        guard let insurance else { return L10n.Booking.trustInsuredNoFigure }
        return L10n.Booking.trustInsured(OrdersFormat.price(insurance.amount, currencyCode: insurance.currencyCode))
    }

    static func faqAnswer(_ insurance: MarketMoney?) -> String {
        guard let insurance else { return L10n.Help.faqA3NoFigure }
        return L10n.Help.faqA3(OrdersFormat.price(insurance.amount, currencyCode: insurance.currencyCode))
    }
}
