import Foundation

enum BookingPricing {
    static let expressLeadHours = 2.0
    static let standardLeadHours = 4.0
    static let expressSurchargeRate = 0.20

    static func requiresExpressSurcharge(cleaningAt: Date?, now: Date = Date()) -> Bool {
        guard let cleaningAt else { return false }
        let leadHours = cleaningAt.timeIntervalSince(now) / 3600.0
        return leadHours >= expressLeadHours && leadHours < standardLeadHours
    }

    static func expressSurchargeAmount(basePrice: Double, cleaningAt: Date?, now: Date = Date()) -> Double {
        requiresExpressSurcharge(cleaningAt: cleaningAt, now: now) ? basePrice * expressSurchargeRate : 0
    }

    static func finalTotal(basePrice: Double, cleaningAt: Date?, now: Date = Date()) -> Double {
        basePrice + expressSurchargeAmount(basePrice: basePrice, cleaningAt: cleaningAt, now: now)
    }

    static func finalTotal(
        basePrice: Double,
        cleaningAt: Date?,
        tierDiscount: Double,
        promoDiscount: Double,
        now: Date = Date()
    ) -> Double {
        let bestDiscount = max(tierDiscount, promoDiscount)
        let discounted = max(basePrice - bestDiscount, 0)
        return discounted + expressSurchargeAmount(basePrice: discounted, cleaningAt: cleaningAt, now: now)
    }

    static func currencySymbol(for code: String) -> String {
        switch code.uppercased() {
        case "CZK": "Kč"
        case "EUR": "€"
        case "USD": "$"
        default: code
        }
    }

    static func formatTotal(_ total: Double, currencyCode: String) -> String {
        String(format: "%.0f %@", total, currencySymbol(for: currencyCode))
    }
}
