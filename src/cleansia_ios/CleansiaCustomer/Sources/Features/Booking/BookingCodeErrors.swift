import Foundation

enum PromoCodeError: String, CaseIterable, Equatable {
    case notFound = "NotFound"
    case inactive = "Inactive"
    case expired = "Expired"
    case notYetValid = "NotYetValid"
    case globalLimitReached = "GlobalLimitReached"
    case perUserLimitReached = "PerUserLimitReached"
    case belowMinimumOrderAmount = "BelowMinimumOrderAmount"
    case currencyMismatch = "CurrencyMismatch"

    static func from(_ raw: String?) -> PromoCodeError? {
        guard let raw else { return nil }
        return allCases.first { $0.rawValue.caseInsensitiveCompare(raw) == .orderedSame }
    }
}

enum ReferralValidationError: String, CaseIterable, Equatable {
    case notFound = "NotFound"
    case selfReferral = "SelfReferral"
    case alreadyReferred = "AlreadyReferred"
    case inactive = "Inactive"

    static func from(_ raw: String?) -> ReferralValidationError? {
        guard let raw else { return nil }
        return allCases.first { $0.rawValue.caseInsensitiveCompare(raw) == .orderedSame }
    }
}
