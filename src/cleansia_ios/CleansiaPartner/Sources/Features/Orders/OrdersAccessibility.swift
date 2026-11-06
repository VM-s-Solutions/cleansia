import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit

/// What VoiceOver says for the Orders board: one sentence per card, row and hero, in the order the eye reads
/// them, and action labels that name their job so a rotor of cards never lists N identical "Take" buttons.
/// The iOS 16 floor rules out `AccessibilityNotification`; announcements go through `UIAccessibility.post`.
enum OrdersAccessibility {
    /// "Tomorrow, 1 November" or "Saturday, 1 November".
    static func dayPhrase(_ date: Date, now: Date, locale: Locale, calendar: Calendar = .current) -> String {
        "\(dayWord(date, now: now, locale: locale, calendar: calendar)), \(OrdersFormat.longDay(date, locale: locale))"
    }

    /// "Today", "Tomorrow", else the wide weekday.
    static func dayWord(_ date: Date, now: Date, locale: Locale, calendar: Calendar = .current) -> String {
        switch OrdersDateTile.emphasis(for: date, now: now, calendar: calendar) {
        case .today: L10n.Orders.dayToday
        case .tomorrow: L10n.Orders.dayTomorrow
        case .plain: OrdersFormat.wideWeekday(date, locale: locale)
        }
    }

    /// `{day}, {window}[, Under way], {pay caption} {pay}. {area}, {band}. {scope}. {flags…}.` — Under way on a job
    /// already running, as the row draws it beside the window; a reserved card leads with its header.
    static func cardSentence(
        _ row: OrderListItem,
        flags: [OrdersBoardRules.Flag],
        reservedUntil: String? = nil,
        now: Date,
        locale: Locale
    ) -> String {
        var first: [String] = []
        if let start = row.cleaningDateTime { first.append(dayPhrase(start, now: now, locale: locale)) }
        if let window = OrdersBoardRules.window(of: row, locale: locale) { first.append(window) }
        if case .underWay? = OrdersBoardRules.liveFlag(row, now: now) { first.append(L10n.Orders.underWayShort) }
        if let pay = OrdersBoardRules.pay(row) { first.append("\(payCaption(row)) \(pay)") }
        let second = [
            OrdersBoardRules.areaLine(row),
            OrdersBoardRules.bandText(row.homeDistanceBand)
        ].compactMap { $0 }
        let scope = OrdersBoardRules.scopeLine(row, locale: locale)?
            .replacingOccurrences(of: OrdersBoardRules.dot, with: ", ")
        var clauses: [String] = []
        if let reservedUntil { clauses += [L10n.Offers.cardTitle, reservedUntil] }
        clauses += [first.joined(separator: ", "), second.joined(separator: ", "), scope ?? ""]
        clauses += flags.map(\.text)
        return sentence(clauses)
    }

    /// An open row's one button: its card sentence, then that it opens the job.
    static func openRowLabel(
        _ row: OrderListItem,
        flags: [OrdersBoardRules.Flag],
        now: Date,
        locale: Locale
    ) -> String {
        sentence([cardSentence(row, flags: flags, now: now, locale: locale), L10n.Orders.opensTheJob])
    }

    /// "Take, tomorrow, 08:00–10:00".
    static func actionLabel(_ action: String, row: OrderListItem, now: Date, locale: Locale) -> String {
        var parts = [action]
        if let start = row.cleaningDateTime {
            parts.append(dayWord(start, now: now, locale: locale).lowercased(with: locale))
        }
        if let window = OrdersBoardRules.window(of: row, locale: locale) { parts.append(window) }
        return parts.joined(separator: ", ")
    }

    /// `{status}, {time}. {day word}, {date}, {window}. {street}, {area}, {customer}. {scope}, {team}. you earn {pay}.
    /// {cash}. {set-off reason}, {when it opens}.` — the cash on a cash job, whose drawn flag VoiceOver skips, and,
    /// while the slide is locked, the locked slide's own words, which the slide then says again.
    static func heroSentence(
        _ row: OrderListItem,
        eyebrow: String,
        rightText: String?,
        now: Date,
        locale: Locale
    ) -> String {
        let when = [
            row.cleaningDateTime.map { dayPhrase($0, now: now, locale: locale) },
            OrdersBoardRules.window(of: row, locale: locale)
        ]
        let place = [
            OrdersBoardRules.streetLine(row.customerAddress),
            OrdersBoardRules.areaLine(row),
            row.customerName?.isBlank == false ? row.customerName : nil
        ]
        let scope = OrdersBoardRules.scopeParts(row, locale: locale)
            + [OrdersBoardRules.heroTeam(row)].compactMap { $0 }
        var setOff = ""
        if case let .setOff(opensAt?, false)? = OrdersBoardRules.heroControl(row, now: now) {
            setOff = "\(L10n.Orders.setOffFrom(OrdersFormat.timeOnly(opensAt, locale: locale))), "
                + OrdersBoardRules.setOffWhen(opensAt, now: now, locale: locale)
        }
        return sentence([
            [eyebrow, rightText].compactMap { $0 }.joined(separator: ", "),
            when.compactMap { $0 }.joined(separator: ", "),
            place.compactMap { $0 }.joined(separator: ", "),
            scope.joined(separator: ", "),
            OrdersBoardRules.pay(row).map { "\(L10n.Orders.youEarn) \($0)" } ?? "",
            OrdersBoardRules.cashFlag(row) ?? "",
            setOff
        ])
    }

    /// `{weekday date}, {window}. {street}. {scope lead}. you earn {pay}. {cash flag}.` — History names the area.
    /// The row's subtitle draws the place and the scope together; the sentence says each once.
    static func rowSentence(_ row: OrderListItem, pane: OrdersPane, now: Date, locale: Locale) -> String {
        let when = [
            row.cleaningDateTime.map { dayPhrase($0, now: now, locale: locale) },
            OrdersBoardRules.window(of: row, locale: locale)
        ]
        return sentence([
            when.compactMap { $0 }.joined(separator: ", "),
            OrdersBoardRules.rowPlace(row, pane: pane) ?? "",
            OrdersBoardRules.leadName(row, locale: locale) ?? "",
            OrdersBoardRules.pay(row).map { "\(L10n.Orders.youEarn) \($0)" } ?? "",
            pane == .history
                ? OrdersBoardRules.historyCash(row)?.items.joined(separator: ", ") ?? ""
                : OrdersBoardRules.cashFlag(row) ?? ""
        ])
    }

    /// A slide's spoken label: what it does, or what it is doing.
    static func slideLabel(idle: String, busy: String, isBusy: Bool) -> String {
        isBusy ? busy : idle
    }

    static func payCaption(_ row: OrderListItem) -> String {
        (row.requiredEmployees ?? 0) > 1 ? L10n.Orders.yourShare : L10n.Orders.youEarn
    }

    // MARK: - Announcements

    static func announce(_ text: String) {
        UIAccessibility.post(notification: .announcement, argument: text)
    }

    static func layoutChanged(_ text: String) {
        UIAccessibility.post(notification: .layoutChanged, argument: text)
    }

    static func screenChanged(_ text: String) {
        UIAccessibility.post(notification: .screenChanged, argument: text)
    }

    private static func sentence(_ clauses: [String]) -> String {
        clauses
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
            .map { $0.hasSuffix(".") ? $0 : $0 + "." }
            .joined(separator: " ")
    }
}

extension View {
    /// Core's slide commits only from a drag, so VoiceOver could never use it. At this build's own call sites
    /// (the take sheet and the hero) it becomes one button whose default action commits; a disabled slide says
    /// why in its hint. The detail's slides are not changed here (→ the Core follow-up).
    func slideAccessibility(
        label: String,
        hint: String? = nil,
        enabled: Bool,
        onActivate: @escaping () -> Void
    ) -> some View {
        accessibilityElement(children: .ignore)
            .accessibilityLabel(label)
            .accessibilityHint(hint ?? "")
            .accessibilityAddTraits(.isButton)
            .disabled(!enabled)
            .accessibilityAction {
                if enabled { onActivate() }
            }
    }
}
