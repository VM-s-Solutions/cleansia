import CleansiaPartnerApi
import Foundation

/// Which calendar day, in the rendering zone, the reservation ends on.
enum RespondByDay {
    case today
    case tomorrow
    case later
    case ended
}

struct RespondBy: Equatable {
    let day: RespondByDay
    let time: String
    let date: String
}

/// The hold's real expiry lives on the server, so a client-side countdown drifts into a lie the moment
/// the screen sits idle. This resolves the server's instant to a wall-clock label plus the day it falls
/// on, and the view turns that into a sentence.
enum PendingOfferPresentation {
    static func respondBy(
        _ respondByUtc: Date?,
        now: Date,
        calendar: Calendar = .current,
        locale: Locale = .current
    ) -> RespondBy? {
        guard let respondByUtc else { return nil }

        let time = formatted(respondByUtc, pattern: "HH:mm", calendar: calendar, locale: locale)
        let date = formatted(respondByUtc, template: "MMMd", calendar: calendar, locale: locale)
        guard respondByUtc > now else { return RespondBy(day: .ended, time: time, date: date) }

        return RespondBy(day: day(of: respondByUtc, now: now, calendar: calendar), time: time, date: date)
    }

    /// The offer whose reservation ends first — the one the cleaner has least time to answer.
    static func soonestOffer(_ offers: [PendingOfferItem]) -> PendingOfferItem? {
        offers
            .compactMap { offer in offer.respondByUtc.map { (offer: offer, deadline: $0) } }
            .min { $0.deadline < $1.deadline }?
            .offer
    }

    private static func day(of deadline: Date, now: Date, calendar: Calendar) -> RespondByDay {
        if calendar.isDate(deadline, inSameDayAs: now) { return .today }
        let tomorrow = calendar.date(byAdding: .day, value: 1, to: now)
        if let tomorrow, calendar.isDate(deadline, inSameDayAs: tomorrow) { return .tomorrow }
        return .later
    }

    private static func formatted(
        _ date: Date,
        pattern: String,
        calendar: Calendar,
        locale: Locale
    ) -> String {
        let formatter = baseFormatter(calendar: calendar, locale: locale)
        formatter.dateFormat = pattern
        return formatter.string(from: date)
    }

    private static func formatted(
        _ date: Date,
        template: String,
        calendar: Calendar,
        locale: Locale
    ) -> String {
        let formatter = baseFormatter(calendar: calendar, locale: locale)
        formatter.setLocalizedDateFormatFromTemplate(template)
        return formatter.string(from: date)
    }

    private static func baseFormatter(calendar: Calendar, locale: Locale) -> DateFormatter {
        let formatter = DateFormatter()
        formatter.locale = locale
        formatter.calendar = calendar
        formatter.timeZone = calendar.timeZone
        return formatter
    }
}
