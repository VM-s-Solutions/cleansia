import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

enum OrderStatusPresentation {
    /// Localized label for a status `Code`, falling back to the wire `name`
    /// then "—" for an unknown value (`orderStatusLabelRes` parity — keeps a
    /// future backend status renderable without a phantom translation).
    static func label(_ code: Code?) -> String {
        if let status = code?.toOrderStatus() {
            return L10n.Orders.statusLabel(status)
        }
        if let name = code?.name?.nonBlankValue {
            return name
        }
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

private extension String {
    var nonBlankValue: String? {
        isBlank ? nil : self
    }
}
