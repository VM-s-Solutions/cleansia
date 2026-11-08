import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// What VoiceOver hears on the board (spec §6.6, §7.4, §9.6): one sentence per card in the order the eye reads
/// it, actions that name their job, and slides that say what they do or why they cannot yet.
final class OrdersAccessibilityTests: XCTestCase {
    private let english = Locale(identifier: "en")
    private typealias Speech = OrdersAccessibility

    func testACardIsOneSentenceInTheVisualOrder() {
        let now = at(10, 0)
        var row = OrderListItem.sample(id: "o1", pay: 780, cleaningDateTime: at(8, 0, dayOffset: 1))
        row.estimatedTime = 120
        row.customerAddressApproximate = "Praha 3"
        row.homeDistanceBand = ._2
        row.selectedServices = [ServiceListItem(id: "s", name: "Standard clean")]
        row.rooms = 2
        row.bathrooms = 1
        row.extras = ["inside-oven": true]
        row.paymentType = Code(value: 1)
        row.amountDueOnCard = 1290
        let flags = OrdersBoardRules.flags(row, clash: nil, now: now, locale: english)

        let sentence = Speech.cardSentence(row, flags: flags, now: now, locale: english)

        let pay = OrdersFormat.money(780, symbol: nil)
        let day = "\(L10n.Orders.dayTomorrow), \(OrdersFormat.longDay(at(8, 0, dayOffset: 1), locale: english))"
        let expected = [
            "\(day), 08:00–10:00, \(L10n.Orders.youEarn) \(pay).",
            "Praha 3, \(L10n.Orders.distanceBand(._2)).",
            "Standard clean, \(L10n.Orders.rooms(2)), \(L10n.Orders.baths(1)), + \(L10n.Orders.extraShortOven).",
            "\(L10n.Orders.flagCashCollect(OrdersFormat.money(1290, symbol: nil)))."
        ].joined(separator: " ")
        XCTAssertEqual(sentence, expected)
    }

    func testATeamCardSaysYourShareAndNoLength() {
        var row = OrderListItem.sample(id: "t", cleaningDateTime: at(9, 0, dayOffset: 2))
        row.estimatedTime = 240
        row.requiredEmployees = 2
        let sentence = Speech.cardSentence(row, flags: [], now: at(8, 0), locale: english)
        XCTAssertTrue(sentence.contains(L10n.Orders.yourShare), sentence)
        XCTAssertFalse(sentence.contains(L10n.Orders.durationH(4)), "the window says how long: \(sentence)")
    }

    func testAReservedCardLeadsWithItsHeader() {
        let row = OrderListItem.sample(id: "r", cleaningDateTime: at(14, 0, dayOffset: 2))
        let sentence = Speech.cardSentence(
            row,
            flags: [],
            reservedUntil: "Yours until 14:30",
            now: at(8, 0),
            locale: english
        )
        XCTAssertTrue(sentence.hasPrefix("\(L10n.Offers.cardTitle). Yours until 14:30."), sentence)
    }

    func testAnActionNamesItsJob() {
        var row = OrderListItem.sample(id: "o1", cleaningDateTime: at(8, 0, dayOffset: 1))
        row.estimatedTime = 120
        let label = Speech.actionLabel(L10n.Offers.confirm, row: row, now: at(10, 0), locale: english)
        let tomorrow = L10n.Orders.dayTomorrow.lowercased(with: english)
        XCTAssertEqual(label, "\(L10n.Offers.confirm), \(tomorrow), 08:00–10:00")
    }

    /// The locked slide is Core's disabled button, named by its reason and countdown.
    func testALockedSlideSaysWhyItWaits() {
        XCTAssertEqual(
            SlideToConfirm.lockedLabel(reason: L10n.Orders.setOffFrom("14:00"), detail: L10n.Orders.heroIn("1 h")),
            "\(L10n.Orders.setOffFrom("14:00")), \(L10n.Orders.heroIn("1 h"))"
        )
    }

    func testTheHeroSentenceReadsEyebrowWhenWhereWhatAndPay() {
        var row = OrderListItem.sample(id: "h", status: ._2, pay: 1250, cleaningDateTime: at(15, 0))
        row.estimatedTime = 180
        row.customerAddress = "Vinohradská 46, Praha, 12000"
        row.customerAddressApproximate = "Praha 2"
        row.customerName = "Jana Nováková"
        let sentence = Speech.heroSentence(
            row,
            eyebrow: "Next up",
            rightText: "in 4 h 40 min",
            now: at(10, 20),
            locale: english
        )
        XCTAssertTrue(sentence.hasPrefix("Next up, in 4 h 40 min. "), sentence)
        XCTAssertTrue(sentence.contains("15:00–18:00."), sentence)
        XCTAssertTrue(sentence.contains("Vinohradská 46, Praha 2, Jana Nováková."), sentence)
        XCTAssertTrue(sentence.contains("\(L10n.Orders.youEarn) \(OrdersFormat.money(1250, symbol: nil))."), sentence)
    }

    /// `{status}. {day word}, {date}, {window}. {place}. {scope as drawn, team of N}. you earn {pay}. {cash}. {set-off
    /// reason}, {the locked slide's second line}.` — the cash is said because its drawn flag is hidden from
    /// VoiceOver; the lock is said here and again by the locked slide.
    func testTheHeroSentenceSaysTheTeamTheCashAndWhenSheCanSetOff() {
        var row = OrderListItem.sample(id: "h", status: ._2, pay: 762, cleaningDateTime: at(14, 0, dayOffset: 1))
        row.estimatedTime = 180
        row.requiredEmployees = 2
        row.customerAddress = "Vinohradská 46, Praha, 12000"
        row.customerAddressApproximate = "Praha 2"
        row.customerName = "Jana Nováková"
        row.selectedServices = [ServiceListItem(id: "s", name: "Standard clean")]
        row.rooms = 2
        row.paymentType = Code(value: 1)
        row.paymentStatus = Code(value: 1)
        row.amountDueOnCard = 1240
        let now = at(9, 20)
        let opensAt = at(13, 0, dayOffset: 1)
        let pay = OrdersFormat.money(762, symbol: nil)
        let cash = L10n.Orders.flagCashCollect(OrdersBoardRules.nonBreaking(OrdersFormat.money(1240, symbol: nil)))
        let when = "\(OrdersFormat.wideWeekday(opensAt, locale: english))\(OrdersBoardRules.dot)"
            + OrdersFormat.dayMonth(opensAt, locale: english)

        let sentence = Speech.heroSentence(
            row,
            eyebrow: L10n.Orders.runStepSetOff,
            rightText: L10n.Orders.dayTomorrow,
            now: now,
            locale: english
        )

        XCTAssertEqual(sentence, [
            "\(L10n.Orders.runStepSetOff), \(L10n.Orders.dayTomorrow).",
            "\(L10n.Orders.dayTomorrow), \(OrdersFormat.longDay(opensAt, locale: english)), 14:00–17:00.",
            "Vinohradská 46, Praha 2, Jana Nováková.",
            "Standard clean, \(L10n.Orders.rooms(2)), \(L10n.Orders.heroTeam(2)).",
            "\(L10n.Orders.youEarn) \(pay).",
            "\(cash).",
            "\(L10n.Orders.setOffFrom("13:00")), \(when)."
        ].joined(separator: " "))

        row.cleaningDateTime = at(14, 0)
        XCTAssertTrue(
            Speech.heroSentence(row, eyebrow: "", rightText: nil, now: now, locale: english)
                .hasSuffix("\(L10n.Orders.setOffFrom("13:00")), \(L10n.Orders.heroIn("3\u{00A0}h 40\u{00A0}min")).")
        )
        row.paymentStatus = Code(value: 2)
        let collected = L10n.Orders.historyCashCollected(
            OrdersBoardRules.nonBreaking(OrdersFormat.money(1240, symbol: nil))
        )
        let paid = Speech.heroSentence(row, eyebrow: "", rightText: nil, now: at(13, 10), locale: english)
        XCTAssertTrue(paid.hasSuffix("\(collected)."), paid)
    }

    /// A job already running reads Under way right after its window, where the row draws it.
    func testAnOpenJobAlreadyUnderWaySaysSoAfterItsWindow() {
        var row = OrderListItem.sample(id: "v", status: ._4, pay: 762, cleaningDateTime: at(8, 0))
        row.estimatedTime = 240
        row.requiredEmployees = 2
        let day = "\(L10n.Orders.dayToday), \(OrdersFormat.longDay(at(8, 0), locale: english))"
        let pay = "\(L10n.Orders.yourShare) \(OrdersFormat.money(762, symbol: nil))"

        let running = Speech.cardSentence(row, flags: [], now: at(10, 0), locale: english)
        XCTAssertTrue(running.hasPrefix("\(day), 08:00–12:00, \(L10n.Orders.underWayShort), \(pay)."), running)

        row.orderStatus = Code(value: 0)
        let ahead = Speech.cardSentence(row, flags: [], now: at(7, 0), locale: english)
        XCTAssertTrue(ahead.hasPrefix("\(day), 08:00–12:00, \(pay)."), ahead)
    }

    /// `{weekday date}, {window}. {street}. {scope}. you earn {pay}. {cash}.` — the scope once, though the
    /// row's subtitle draws it beside the street (Active) or the area (History). History says what became of the cash.
    func testARowSentenceNamesItsPlaceThenItsScopeOnce() {
        var row = OrderListItem.sample(id: "r", status: ._2, pay: 880, cleaningDateTime: at(10, 0, dayOffset: 2))
        row.estimatedTime = 120
        row.customerAddress = "Korunní 12, Praha, 12000"
        row.customerAddressApproximate = "Praha 2"
        row.selectedServices = [ServiceListItem(id: "s", name: "Standard clean")]
        let pay = "\(L10n.Orders.youEarn) \(OrdersFormat.money(880, symbol: nil))."
        let day = "\(OrdersFormat.wideWeekday(at(10, 0, dayOffset: 2), locale: english)), "
            + OrdersFormat.longDay(at(10, 0, dayOffset: 2), locale: english)

        let active = Speech.rowSentence(row, pane: .active, now: at(8, 0), locale: english)
        XCTAssertEqual(active, "\(day), 10:00–12:00. Korunní 12. Standard clean. \(pay)")

        let history = Speech.rowSentence(row, pane: .history, now: at(8, 0), locale: english)
        XCTAssertEqual(history, "\(day), 10:00–12:00. Praha 2. Standard clean. \(pay)")

        row.paymentType = Code(value: 1)
        row.amountDueOnCard = 1240
        row.paymentStatus = Code(value: 2)
        let amount = OrdersBoardRules.nonBreaking(OrdersFormat.money(1240, symbol: nil))
        XCTAssertTrue(
            Speech.rowSentence(row, pane: .history, now: at(8, 0), locale: english)
                .hasSuffix("\(L10n.Orders.historyCashCollected(amount)).")
        )
        XCTAssertTrue(
            Speech.rowSentence(row, pane: .active, now: at(8, 0), locale: english)
                .hasSuffix("\(L10n.Orders.historyCashCollected(amount)).")
        )
        row.paymentStatus = Code(value: 1)
        XCTAssertTrue(
            Speech.rowSentence(row, pane: .active, now: at(8, 0), locale: english)
                .hasSuffix("\(L10n.Orders.flagCashCollect(amount)).")
        )
    }

    private func at(_ hour: Int, _ minute: Int, dayOffset: Int = 0) -> Date {
        let calendar = Calendar.current
        let base = calendar.date(from: DateComponents(year: 2026, month: 10, day: 31, hour: 12)) ?? Date()
        let day = calendar.date(byAdding: .day, value: dayOffset, to: base) ?? base
        return calendar.date(bySettingHour: hour, minute: minute, second: 0, of: day) ?? day
    }
}
