import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The confirmation ticket's copy and figures: the free-cancellation deadline, the no-cleaner remedy, the
/// crew, the money rows, and the strings behind them in all five languages.
@MainActor
final class BookingSuccessTicketTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]
    private static let removed = [
        "booking_success_confirmation_code", "booking_success_arrival_label", "booking_success_address_label",
        "booking_success_subtitle", "booking_success_whats_next"
    ]

    private var restoreBundle = L10n.bundle
    private var restoreTag = CoreL10n.languageTag

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    private func local(_ day: Int, _ hour: Int, _ minute: Int = 0) throws -> Date {
        try XCTUnwrap(Calendar.current.date(from: DateComponents(
            year: 2026, month: 10, day: day, hour: hour, minute: minute
        )))
    }

    private func plain(_ text: String?) -> String? {
        text?.replacingOccurrences(of: "\u{00A0}", with: " ").replacingOccurrences(of: "\u{202F}", with: " ")
    }

    private func use(_ language: String) throws {
        let hosts = [Bundle(identifier: "cz.cleansia.customer") ?? .main, Bundle.main, Bundle(for: Self.self)]
        let path = try XCTUnwrap(hosts.lazy.compactMap { $0.path(forResource: language, ofType: "lproj") }.first)
        L10n.bundle = try XCTUnwrap(Bundle(path: path))
        CoreL10n.apply(languageTag: language)
    }

    private func booking(cash: Bool = false, credit: Bool = true, crew: Int = 2) -> BookingSuccess {
        var booking = BookingSuccess(orderId: "o-1", confirmationCode: "130AA4")
        booking.paysCash = cash
        booking.appliesCredit = credit && !cash
        booking.requiredEmployees = crew
        return booking
    }

    private func order(total: Double, credit: Double, cash: Bool = false) -> CustomerOrderDetail {
        OrderFixtures.detail(
            displayOrderNumber: "ORD-88BC31F3", total: total, creditAppliedAmount: credit,
            paymentType: Code(type: "PaymentType", value: cash ? 1 : 2),
            paymentStatus: Code(type: "PaymentStatus", value: 1), currencyCode: "CZK", freeCancellationHours: 24
        )
    }

    // MARK: - The deadline

    func testTheDeadlineIsTheOrdersHoursBeforeTheSlot() throws {
        let slot = try local(31, 9)
        let booked = try local(28, 20, 41)
        let deadline = { (hours: Int, language: String) in
            BookingSuccessView.freeCancellationDeadline(
                cleaningAt: slot, freeHours: hours, now: booked, locale: Locale(identifier: language)
            )
        }

        // iOS 16's CLDR joins this date and time with " at "; later ones with ", ".
        XCTAssertEqual(plain(deadline(24, "en"))?.replacingOccurrences(of: " at ", with: ", "), "Fri, Oct 30, 9:00 AM")
        XCTAssertEqual(plain(deadline(24, "cs")), "pá 30. 10. 9:00")
        XCTAssertEqual(
            plain(deadline(4, "en"))?.replacingOccurrences(of: " at ", with: ", "), "Sat, Oct 31, 5:00 AM",
            "an entitled Plus member's 4 h"
        )
        XCTAssertFalse(try XCTUnwrap(deadline(24, "en")).contains(" "), "the date never wraps")
    }

    func testADeadlineBehindOrMissingFallsBackToTheRuleThatAlwaysHolds() throws {
        let slot = try local(31, 9)

        XCTAssertNil(
            try BookingSuccessView.freeCancellationDeadline(
                cleaningAt: slot, freeHours: 24, now: local(30, 18), locale: Locale(identifier: "en")
            ),
            "booked under 24 h ahead: no past time"
        )
        XCTAssertNil(
            try BookingSuccessView.freeCancellationDeadline(
                cleaningAt: slot, freeHours: 24, now: local(30, 9), locale: Locale(identifier: "en")
            ),
            "a deadline that is now is behind"
        )
        XCTAssertNil(
            try BookingSuccessView.freeCancellationDeadline(
                cleaningAt: slot, freeHours: nil, now: local(28, 20, 41), locale: Locale(identifier: "en")
            ),
            "no hours on the order is never 24"
        )
    }

    func testWhileTheOrderLoadsTheDeadlineIsKnownAheadOnlyAStandardWindowOut() throws {
        let slot = try local(31, 9)

        XCTAssertTrue(try BookingSuccessView.deadlineAheadOnEveryPlan(cleaningAt: slot, now: local(30, 9)))
        XCTAssertFalse(try BookingSuccessView.deadlineAheadOnEveryPlan(cleaningAt: slot, now: local(30, 9, 1)))
        XCTAssertFalse(BookingSuccessView.deadlineAheadOnEveryPlan(cleaningAt: nil, now: slot))
    }

    /// The web hard-coded 24 and was ruled wrong; the deadline is the order's own figure.
    func testNoConfirmationCodePathAssumesTwentyFourHours() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        let hourWords = try NSRegularExpression(pattern: #"(?i)hour|deadline|cancel|3600|86400"#)
        let literal = try NSRegularExpression(pattern: #"\b24\b"#)
        for file in [
            "Sources/Features/Booking/BookingSuccessView.swift",
            "Sources/Features/Booking/BookingSuccessView+Summary.swift",
            "Sources/Features/Booking/BookingSuccessViewModel.swift"
        ] {
            let source = try String(contentsOf: root.appendingPathComponent(file), encoding: .utf8)
            for line in source.components(separatedBy: .newlines) {
                let range = NSRange(line.startIndex..., in: line)
                guard hourWords.firstMatch(in: line, range: range) != nil else { continue }
                XCTAssertNil(literal.firstMatch(in: line, range: range), "\(file) assumes 24 h: \(line)")
            }
        }
    }

    // MARK: - The finding

    func testTheRemedyFollowsHowTheBookingIsPaid() {
        XCTAssertEqual(BookingSuccessView.remedy(booking()), L10n.Booking.successNoCleanerCard)
        XCTAssertEqual(BookingSuccessView.remedy(booking(credit: false)), L10n.Booking.successNoCleanerCard)
        XCTAssertEqual(BookingSuccessView.remedy(booking(cash: true, crew: 1)), L10n.Booking.successNoCleanerCash)
        XCTAssertNotEqual(L10n.Booking.successNoCleanerCard, L10n.Booking.successNoCleanerCash)
    }

    func testTheFindingReadsAsOneSentenceWithTheCrewInWords() throws {
        try use("en")

        XCTAssertEqual(
            BookingSuccessView.findingSpoken(booking()),
            "No cleaner has taken it yet. It needs 2 cleaners. We'll let you know as soon as one does. "
                + "If no cleaner can take it, we cancel it and refund you in full, plus credit."
        )
        XCTAssertEqual(
            BookingSuccessView.findingSpoken(booking(cash: true, crew: 1)),
            "No cleaner has taken it yet. It needs 1 cleaner. We'll let you know as soon as one does. "
                + "If no cleaner can take it, we cancel it free of charge and give you credit."
        )
    }

    func testTheCrewIsCountedInEveryLanguagesPluralForms() throws {
        let crew: [String: [String]] = [
            "en": ["1 cleaner", "2 cleaners", "3 cleaners", "5 cleaners"],
            "cs": ["1 uklízeč", "2 uklízeči", "3 uklízeči", "5 uklízečů"],
            "sk": ["1 upratovač", "2 upratovači", "3 upratovači", "5 upratovačov"],
            "uk": ["1 клінер", "2 клінери", "3 клінери", "5 клінерів"],
            "ru": ["1 клинер", "2 клинера", "3 клинера", "5 клинеров"]
        ]
        let needed: [String: [String]] = [
            "en": ["It needs 1 cleaner.", "It needs 2 cleaners.", "It needs 3 cleaners.", "It needs 5 cleaners."],
            "cs": ["Potřebuje 1 uklízeče.", "Potřebuje 2 uklízeče.", "Potřebuje 3 uklízeče.", "Potřebuje 5 uklízečů."],
            "sk": [
                "Potrebuje 1 upratovača.", "Potrebuje 2 upratovačov.", "Potrebuje 3 upratovačov.",
                "Potrebuje 5 upratovačov."
            ],
            "uk": ["Потрібен 1 клінер.", "Потрібно 2 клінери.", "Потрібно 3 клінери.", "Потрібно 5 клінерів."],
            "ru": ["Нужен 1 клинер.", "Нужно 2 клинера.", "Нужно 3 клинера.", "Нужно 5 клинеров."]
        ]
        for language in Self.languages {
            try use(language)
            XCTAssertEqual([1, 2, 3, 5].map(L10n.Booking.successCrew), crew[language], language)
            XCTAssertEqual([1, 2, 3, 5].map(L10n.Booking.successCrewNeeded), needed[language], language)
        }
    }

    // MARK: - The money

    func testACreditBookingShowsTheTotalAndBothShares() {
        let money = BookingSuccessView.paymentRows(.loaded(order(total: 1150, credit: 250)), booking: booking())

        XCTAssertEqual(
            money.rows.map(\.label),
            [L10n.Booking.successTotalLabel, L10n.Credit.paidWithCredit, L10n.Credit.paidByCard]
        )
        XCTAssertEqual(money.rows.map(\.value), [
            OrdersFormat.price(1150, currencyCode: "CZK"),
            "−" + OrdersFormat.price(250, currencyCode: "CZK"),
            OrdersFormat.price(900, currencyCode: "CZK")
        ])
        XCTAssertNil(money.note)
    }

    func testACardBookingWithoutCreditStatesTheTotalOnceAndHowItWasPaid() {
        let money = BookingSuccessView.paymentRows(
            .loaded(order(total: 1150, credit: 0)), booking: booking(credit: false)
        )

        XCTAssertEqual(money.rows.map(\.label), [L10n.Booking.successTotalLabel])
        XCTAssertEqual(money.rows.map(\.value), [OrdersFormat.price(1150, currencyCode: "CZK")])
        XCTAssertEqual(money.note, L10n.Credit.paidByCard, "no repeated figure")
    }

    func testACashBookingStatesTheTotalAndWhenToPayIt() {
        let money = BookingSuccessView.paymentRows(
            .loaded(order(total: 1000, credit: 0, cash: true)), booking: booking(cash: true, crew: 1)
        )

        XCTAssertEqual(money.rows.map(\.label), [L10n.Booking.successTotalLabel])
        XCTAssertEqual(money.rows.map(\.value), [OrdersFormat.price(1000, currencyCode: "CZK")])
        XCTAssertEqual(money.note, L10n.Booking.successCashOnDay)
    }

    func testUnavailableDetailsGuessNoShares() {
        for draft in [booking(), booking(credit: false), booking(cash: true, crew: 1)] {
            let money = BookingSuccessView.paymentRows(.error, booking: draft)
            XCTAssertEqual(money.rows.map(\.label), [L10n.Booking.successTotalLabel])
            XCTAssertEqual(money.rows.map(\.value), ["—"])
            XCTAssertEqual(money.note, L10n.Booking.successDetailsUnavailable)
        }
    }

    func testWhileLoadingOnlyTheRowsTheDraftPromisesHoldBars() {
        let credit = BookingSuccessView.paymentRows(.loading, booking: booking())
        XCTAssertEqual(
            credit.rows.map(\.label),
            [L10n.Booking.successTotalLabel, L10n.Credit.paidWithCredit, L10n.Credit.paidByCard]
        )
        XCTAssertEqual(credit.rows.map(\.value), ["", "", ""])
        XCTAssertNil(credit.note)

        let card = BookingSuccessView.paymentRows(.loading, booking: booking(credit: false))
        XCTAssertEqual(card.rows.map(\.value), [""])
        XCTAssertEqual(card.note, L10n.Credit.paidByCard)

        let cash = BookingSuccessView.paymentRows(.loading, booking: booking(cash: true, crew: 1))
        XCTAssertEqual(cash.rows.map(\.value), [""])
        XCTAssertEqual(cash.note, L10n.Booking.successCashOnDay)
    }

    // MARK: - The actions

    /// The frames' `flex: 1 1 auto` at 1 : 1.4: each button starts at its label and 20 pt a side, and Back to
    /// home takes 1 of every 2.4 points left over; View order takes the rest.
    func testBackToHomeTakesOneOfEveryTwoPointFourPointsTheLabelsLeave() {
        XCTAssertEqual(BookingSuccessView.homeActionWidth(room: 361, homeLabel: 98, viewLabel: 78), 176)
        XCTAssertEqual(BookingSuccessView.homeActionWidth(room: 361, homeLabel: 78, viewLabel: 150), 135)
        XCTAssertEqual(
            BookingSuccessView.homeActionWidth(room: 361, homeLabel: 100, viewLabel: 169), 140,
            "nothing left over: each at its label and padding"
        )
        XCTAssertNil(
            BookingSuccessView.homeActionWidth(room: 361, homeLabel: 150, viewLabel: 130),
            "labels that do not fit side by side"
        )
        XCTAssertNil(BookingSuccessView.homeActionWidth(room: 361, homeLabel: 0, viewLabel: 0), "not measured yet")
    }

    // MARK: - The strings

    func testEveryNewStringIsWrittenAsSpecifiedInAllFiveLanguages() throws {
        let nbsp = "\u{00A0}"
        let expected: [String: [String: String]] = [
            "booking_success_finding": [
                "en": "No cleaner has taken it yet.", "cs": "Zatím ji nepřevzal žádný uklízeč.",
                "sk": "Zatiaľ ju neprevzal žiadny upratovač.", "uk": "Поки що жоден клінер його не взяв.",
                "ru": "Пока ни один клинер его не взял."
            ],
            "booking_success_finding_sub": [
                "en": "We'll let you know as soon as one does.", "cs": "Dáme vám vědět, jakmile ji někdo převezme.",
                "sk": "Dáme vám vedieť, hneď ako ju niekto prevezme.", "uk": "Повідомимо вас, щойно хтось його візьме.",
                "ru": "Сообщим вам, как только кто-то его возьмёт."
            ],
            "booking_success_free_until": [
                "en": "Free cancellation until %@", "cs": "Zrušení zdarma do %@", "sk": "Zrušenie zadarmo do %@",
                "uk": "Безкоштовне скасування до %@", "ru": "Бесплатная отмена до %@"
            ],
            "booking_success_free_while": [
                "en": "Free to cancel while no cleaner has taken it.",
                "cs": "Zrušení zdarma, dokud ji nepřevezme žádný uklízeč.",
                "sk": "Zrušenie zadarmo, kým ju neprevezme žiadny upratovač.",
                "uk": "Скасування безкоштовне, поки жоден клінер його не взяв.",
                "ru": "Отмена бесплатна, пока ни один клинер его не взял."
            ],
            "booking_success_no_cleaner_card": [
                "en": "If no cleaner can take it, we cancel it and refund you in full, plus credit.",
                "cs": "Když ji nikdo nepřevezme, zrušíme ji, vrátíme vám vše a\(nbsp)přidáme kredit.",
                "sk": "Keď ju nikto neprevezme, zrušíme ju, vrátime vám všetko a pridáme kredit.",
                "uk": "Якщо його ніхто не візьме, ми скасуємо його, повернемо всю суму й нарахуємо кредит.",
                "ru": "Если его никто не возьмёт, мы отменим его, вернём всю сумму и начислим кредит."
            ],
            "booking_success_no_cleaner_cash": [
                "en": "If no cleaner can take it, we cancel it free of charge and give you credit.",
                "cs": "Když ji nikdo nepřevezme, zrušíme ji zdarma a\(nbsp)přidáme vám kredit.",
                "sk": "Keď ju nikto neprevezme, zrušíme ju zadarmo a pridáme vám kredit.",
                "uk": "Якщо його ніхто не візьме, ми безкоштовно скасуємо його й нарахуємо вам кредит.",
                "ru": "Если его никто не возьмёт, мы бесплатно отменим его и начислим вам кредит."
            ],
            "booking_success_cash_on_day": [
                "en": "Please pay in cash to the cleaner on the day.",
                "cs": "Zaplaťte v\(nbsp)hotovosti uklízeči v\(nbsp)den úklidu.",
                "sk": "Zaplaťte v hotovosti upratovačovi v deň upratovania.",
                "uk": "Сплатіть готівкою клінеру в день прибирання.", "ru": "Оплатите наличными клинеру в день уборки."
            ]
        ]
        for language in Self.languages {
            try use(language)
            for (key, values) in expected {
                XCTAssertEqual(L10n.localized(key), values[language], "\(language)/\(key)")
            }
        }
    }

    func testTheStringsTheOldSheetReadAreGone() throws {
        for language in Self.languages {
            try use(language)
            for key in Self.removed {
                XCTAssertEqual(
                    L10n.bundle.localizedString(forKey: key, value: "∅", table: nil), "∅", "\(language) keeps \(key)"
                )
            }
        }
    }
}
