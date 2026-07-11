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

    /// Status accent color (`orderStatusColor`, `OrderFormatters.kt:29-36`):
    /// New/Pending → warning, Confirmed → primary, OnTheWay/InProgress →
    /// secondary, Completed → success, Cancelled → error.
    static func color(_ code: Code?) -> Color {
        switch code?.toOrderStatus() {
        case ._0, ._1: CleansiaColors.warningStar
        case ._2: CleansiaColors.primary
        case ._3, ._4: CleansiaColors.secondary
        case ._5: CleansiaColors.successText
        case ._6: CleansiaColors.error
        case nil: CleansiaColors.outlineVariant
        }
    }
}

#if DEBUG
    private extension String {
        var nonBlankValue: String? {
            isBlank ? nil : self
        }
    }
#endif
