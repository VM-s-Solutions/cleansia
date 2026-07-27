import CleansiaPartnerApi
import Foundation

/// Human-readable label for a timeline status entry. The backend ships status
/// names as non-localized enum strings ("OnTheWay", "InProgress", …); we resolve
/// the numeric value to the localized label so a wire name never leaks into a
/// translated build (mirrors the customer `OrderStatusPresentation`). An unknown
/// status renders "—" in production; the prettified raw name surfaces in DEBUG
/// only as a diagnostic for a future backend status.
enum OrderStatusLabel {
    /// Resolves the entry through the one sanctioned `Code → OrderStatus` mapping
    /// (`OrderStatus+Code.swift`) rather than re-reading `.value` inline.
    static func label(_ track: OrderStatusTrackDto) -> String {
        label(status: track.statusEnum, name: track.status?.name)
    }

    static func label(status: OrderStatus?, name: String?) -> String {
        if let status {
            return L10n.Orders.statusLabel(status)
        }
        #if DEBUG
            if let name = name?.trimmingCharacters(in: .whitespaces), !name.isEmpty {
                return prettify(name)
            }
        #endif
        return "—"
    }

    /// Insert a space between a lower→upper camel boundary and upper-case the
    /// first letter: "OnTheWay" → "On the way".
    static func prettify(_ raw: String) -> String {
        var result = ""
        let chars = Array(raw)
        for (index, char) in chars.enumerated() {
            if index > 0, char.isUppercase, chars[index - 1].isLowercase {
                result.append(" ")
                result.append(Character(char.lowercased()))
            } else {
                result.append(char)
            }
        }
        guard let first = result.first else { return result }
        return first.uppercased() + result.dropFirst()
    }
}
