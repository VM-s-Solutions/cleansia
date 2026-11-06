import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

enum OrderStatusPresentation {
    /// Localized label for a status `Code`. An unmapped status renders "—" in
    /// production so a non-localized backend wire name ("New", "Cancelled")
    /// never leaks into a translated build; the raw name surfaces in DEBUG only
    /// as a diagnostic for a future backend status.
    static func label(_ code: Code?) -> String {
        if let status = code?.toOrderStatus() {
            return L10n.Orders.statusLabel(status)
        }
        #if DEBUG
            if let name = code?.name?.nonBlankValue {
                return name
            }
        #endif
        return "—"
    }

    /// The tint and the group are the table both apps share, read by the status's wire value.
    static func color(_ code: Code?) -> Color {
        OrderStatusStyle.tint(wireValue: code?.toOrderStatus()?.rawValue)
    }

    static func tone(_ code: Code?) -> OrderStatusTone {
        OrderStatusStyle.tone(wireValue: code?.toOrderStatus()?.rawValue)
    }
}

#if DEBUG
    private extension String {
        var nonBlankValue: String? {
            isBlank ? nil : self
        }
    }
#endif
