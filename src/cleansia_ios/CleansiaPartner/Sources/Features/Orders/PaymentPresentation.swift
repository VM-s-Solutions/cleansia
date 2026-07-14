import Foundation

/// Maps the backend's non-localized payment wire values (`paymentType.name` =
/// "Cash"/"Card", `paymentStatus.name` = "Pending"/"Paid"/…) to localized keys,
/// mirroring the customer `OrderStatusPresentation`. Unknown values render "—" in
/// production so a wire name never leaks into a translated build; the raw value
/// surfaces in DEBUG only as a diagnostic for a future backend method/status.
enum PaymentPresentation {
    static func methodLabel(_ raw: String?) -> String {
        switch raw?.trimmingCharacters(in: .whitespaces).lowercased() {
        case "cash":
            L10n.Orders.paymentMethodCash
        case "card", "creditcard", "credit_card", "credit card", "stripe":
            L10n.Orders.paymentMethodCard
        default:
            rawDiagnostic(raw)
        }
    }

    static func statusLabel(_ raw: String?) -> String {
        let key = raw?.lowercased() ?? ""
        if key.contains("paid") || key.contains("captured") || key.contains("succeed") {
            return L10n.Orders.paymentStatusPaid
        }
        if key.contains("pending") || key.contains("processing") {
            return L10n.Orders.paymentStatusPending
        }
        if key.contains("failed") || key.contains("refund") || key.contains("declined") {
            return L10n.Orders.paymentStatusFailed
        }
        return rawDiagnostic(raw)
    }

    private static func rawDiagnostic(_ raw: String?) -> String {
        #if DEBUG
            if let raw = raw?.trimmingCharacters(in: .whitespaces), !raw.isEmpty {
                return raw
            }
        #endif
        return "—"
    }
}
