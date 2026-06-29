import Foundation

enum StripeConfig {
    static var publishableKey: String {
        let value = Bundle.main.object(forInfoDictionaryKey: "STRIPE_PUBLISHABLE_KEY") as? String ?? ""
        return value.hasPrefix("$(") ? "" : value
    }

    static var isCardPaymentAvailable: Bool {
        isConfigured(publishableKey: publishableKey)
    }

    static func isConfigured(publishableKey: String) -> Bool {
        let trimmed = publishableKey.trimmingCharacters(in: .whitespacesAndNewlines)
        return !trimmed.isEmpty && !trimmed.hasPrefix("$(")
    }
}
