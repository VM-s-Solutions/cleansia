import CleansiaPartnerApi
import Foundation

/// Human-readable label for a timeline status entry. The backend ships status
/// names as enum-friendly strings ("OnTheWay", "InProgress", …); we prettify
/// them ("OnTheWay" → "On the way", "InProgress" → "In progress") and fall back
/// to the numeric value → localized label when the name is missing (the
/// `labelForStatusName` parity, StatusTimeline.kt:149-164).
enum OrderStatusLabel {
    static func label(name: String?, value: Int?) -> String {
        if let name = name?.trimmingCharacters(in: .whitespaces), !name.isEmpty {
            return prettify(name)
        }
        return L10n.Orders.statusLabel(value.flatMap(OrderStatus.init(rawValue:)))
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
