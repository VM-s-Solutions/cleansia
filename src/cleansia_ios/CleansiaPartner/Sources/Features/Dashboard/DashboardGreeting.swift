import Foundation

enum DashboardGreeting {
    static func text(firstName: String?, now: Date = Date()) -> String {
        let hour = Calendar.current.component(.hour, from: now)
        let name = firstName?.trimmingCharacters(in: .whitespacesAndNewlines)
        if let name, !name.isEmpty {
            switch hour {
            case ..<12: return L10n.Dashboard.goodMorningName(name)
            case ..<18: return L10n.Dashboard.goodAfternoonName(name)
            default: return L10n.Dashboard.goodEveningName(name)
            }
        }
        switch hour {
        case ..<12: return L10n.Dashboard.goodMorning
        case ..<18: return L10n.Dashboard.goodAfternoon
        default: return L10n.Dashboard.goodEvening
        }
    }

    static func dateLine(now: Date = Date(), locale: Locale = .current) -> String {
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.setLocalizedDateFormatFromTemplate("EEEE d MMM")
        return formatter.string(from: now)
    }
}
