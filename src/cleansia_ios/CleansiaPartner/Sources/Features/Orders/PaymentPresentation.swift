import Foundation

enum PaymentStatusSeverity {
    case success
    case warning
    case error
    case neutral
}

/// Labels and tints key off the `Code` envelope's numeric value, never its `name`:
/// the name is the backend's English enum text, so matching on it breaks silently
/// when the wording changes and would leak untranslated copy into the UI. An ordinal
/// we don't know yet (a backend enum addition) reads as a neutral placeholder for the
/// cleaner and as the raw ordinal in DEBUG so the gap surfaces to us instead.
enum PaymentPresentation {
    static func methodLabel(_ code: Int?) -> String {
        switch code.flatMap(PaymentTypeCode.init(rawValue:)) {
        case .cash: L10n.Orders.paymentMethodCash
        case .card: L10n.Orders.paymentMethodCard
        case .none: rawDiagnostic(code)
        }
    }

    static func statusLabel(_ code: Int?) -> String {
        switch code.flatMap(PaymentStatusCode.init(rawValue:)) {
        case .pending: L10n.Orders.paymentStatusPending
        case .paid: L10n.Orders.paymentStatusPaid
        case .failed: L10n.Orders.paymentStatusFailed
        case .refunded: L10n.Orders.paymentStatusRefunded
        case .disputed: L10n.Orders.paymentStatusDisputed
        case .partiallyRefunded: L10n.Orders.paymentStatusPartiallyRefunded
        case .none: rawDiagnostic(code)
        }
    }

    static func statusSeverity(_ code: Int?) -> PaymentStatusSeverity {
        switch code.flatMap(PaymentStatusCode.init(rawValue:)) {
        case .paid: .success
        case .pending: .warning
        case .failed, .disputed: .error
        case .refunded, .partiallyRefunded, .none: .neutral
        }
    }

    private static func rawDiagnostic(_ code: Int?) -> String {
        #if DEBUG
            if let code {
                return "#\(code)"
            }
        #endif
        return "—"
    }
}
