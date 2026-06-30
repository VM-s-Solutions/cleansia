import CleansiaCustomerApi
import Foundation

enum OrdersFormat {
    /// Price + currency suffix, mirroring `OrderFormatters.kt:98-116` (grouped,
    /// no fraction digits; CZK/EUR/USD/GBP get their symbol, others the raw
    /// code — never crashes on an unknown currency).
    static func price(_ amount: Double, currencyCode: String?) -> String {
        let code = currencyCode?.nonBlank ?? "CZK"
        let formatter = NumberFormatter()
        formatter.numberStyle = .decimal
        formatter.locale = .current
        formatter.maximumFractionDigits = 0
        formatter.minimumFractionDigits = 0
        let number = formatter.string(from: NSNumber(value: amount)) ?? "\(Int(amount))"
        switch code.uppercased() {
        case "CZK": return "\(number) Kč"
        case "EUR": return "\(number) €"
        case "USD": return "$\(number)"
        case "GBP": return "£\(number)"
        default: return "\(number) \(code)"
        }
    }

    /// "Mon 1 Jul · 10:00–12:00" — date + start–end window (`estimatedMinutes`
    /// drives the end). No estimate → just the start date-time
    /// (`OrderFormatters.kt:69-91`).
    static func dateRange(_ date: Date?, estimatedMinutes: Int) -> String {
        guard let date else { return "—" }
        if estimatedMinutes <= 0 {
            return shortDateTime.string(from: date)
        }
        let end = date.addingTimeInterval(Double(estimatedMinutes) * 60)
        let datePart = mediumDate.string(from: date)
        return "\(datePart) · \(timeOnly.string(from: date))–\(timeOnly.string(from: end))"
    }

    static func dateTime(_ date: Date?) -> String {
        guard let date else { return "—" }
        return shortDateTime.string(from: date)
    }

    /// "Eco Products" from "eco_products"/"ecoProducts" (`prettifyExtraKey`).
    static func prettifyExtraKey(_ key: String) -> String {
        guard !key.isBlank else { return key }
        var spaced = key.replacingOccurrences(of: "_", with: " ")
            .replacingOccurrences(of: "-", with: " ")
        spaced = insertCamelSpaces(spaced)
        return spaced.split(separator: " ").map { word in
            word.prefix(1).uppercased() + word.dropFirst().lowercased()
        }.joined(separator: " ")
    }

    /// Up to 2 package names then service names, "+ N more" suffix
    /// (`OrdersTab.kt:520-534`).
    static func servicesSummary(_ order: OrderListItem) -> String {
        let packages = (order.selectedPackages ?? []).compactMap { $0.name?.nonBlank }
        let services = (order.selectedServices ?? []).compactMap { $0.name?.nonBlank }
        let combined = packages + services
        guard !combined.isEmpty else { return "—" }
        let shown = Array(combined.prefix(2))
        let remaining = combined.count - shown.count
        let base = shown.joined(separator: ", ")
        return remaining > 0 ? "\(base) \(L10n.Orders.servicesMore(remaining))" : base
    }

    private static func insertCamelSpaces(_ text: String) -> String {
        var result = ""
        let chars = Array(text)
        for (index, char) in chars.enumerated() {
            if index > 0, char.isUppercase, chars[index - 1].isLowercase {
                result.append(" ")
            }
            result.append(char)
        }
        return result
    }

    private static let shortDateTime: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = .current
        formatter.setLocalizedDateFormatFromTemplate("EEE d MMM HH:mm")
        return formatter
    }()

    private static let mediumDate: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = .current
        formatter.setLocalizedDateFormatFromTemplate("EEE d MMM")
        return formatter
    }()

    private static let timeOnly: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = .current
        formatter.setLocalizedDateFormatFromTemplate("HH:mm")
        return formatter
    }()
}

private extension String {
    var nonBlank: String? {
        isBlank ? nil : self
    }
}
