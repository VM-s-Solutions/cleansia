import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class DashboardFormatTests: XCTestCase {
    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    // MARK: - Money

    /// Android's `CurrencySymbols` (2026-08-23): a currency's symbol belongs to the currency, so a Czech cleaner
    /// reads "Kč" whatever language their phone is in. English and Ukrainian know no symbol for the koruna, and
    /// the dashboard printed "CZK" beside an orders list printing the server's "Kč".
    func testTheKorunaReadsKcInEveryPhoneLanguage() {
        for identifier in ["en_US", "uk_UA", "cs_CZ", "en_US@rg=czzzzz"] {
            XCTAssertEqual(
                DashboardFormat.money(2034, currencyCode: "CZK", locale: Locale(identifier: identifier)),
                "2\u{202F}034 Kč",
                identifier
            )
        }
    }

    func testOtherCodesKeepTheirSymbolAndAnUnknownOneIsItsOwnLabel() {
        let english = Locale(identifier: "en_US")
        XCTAssertEqual(DashboardFormat.money(2034, currencyCode: "EUR", locale: english), "2\u{202F}034 €")
        XCTAssertEqual(DashboardFormat.money(2034, currencyCode: "XYZ", locale: english), "2\u{202F}034 XYZ")
    }

    func testNoCodeIsABareFigureAndNothingEarnedIsADash() {
        XCTAssertEqual(DashboardFormat.money(2034, currencyCode: nil), "2\u{202F}034")
        XCTAssertEqual(DashboardFormat.money(0, currencyCode: "CZK"), "—")
    }

    /// The pay-period and last-month figures were bare numbers on both platforms, "3609" under the week's
    /// "2 034 Kč".
    func testThePayPeriodAndLastMonthFiguresCarryTheirCurrency() throws {
        let cards = try compactSource("Dashboard/DashboardCards.swift")
        XCTAssertTrue(cards.contains("DashboardFormat.money(period.earnings,currencyCode:currencyCode)"))
        XCTAssertTrue(cards.contains("DashboardFormat.money(data.lastMonthEarnings,currencyCode:data.currencyCode)"))
        XCTAssertFalse(try compactSource("Dashboard/DashboardFormat.swift").contains("plainMoney"))
    }

    // MARK: - The pay period

    /// The period's days are the server's calendar days and today is the phone's. At 21:00 on the 15th in Los
    /// Angeles, already the 16th in Prague, a period of the 3rd to the 16th is on its 13th day of 14 with one left.
    func testThePeriodCountsCalendarDaysFromThePhonesToday() throws {
        let start = try utcDay("2026-11-03")
        let end = try utcDay("2026-11-16")
        let now = try XCTUnwrap(ISO8601DateFormatter().date(from: "2026-11-16T05:00:00Z"))
        let window = try XCTUnwrap(PayPeriodWindow(stats: .stub(
            currentPayPeriodStart: start,
            currentPayPeriodEnd: end
        )))
        let expected: [String: (day: Int, left: Int)] = ["America/Los_Angeles": (13, 1), "Europe/Prague": (14, 0)]

        for (zone, want) in expected {
            let timeZone = try XCTUnwrap(TimeZone(identifier: zone))
            let progress = DashboardFormat.payPeriodProgress(start: start, end: end, now: now, timeZone: timeZone)
            XCTAssertEqual(progress.day, want.day, zone)
            XCTAssertEqual(progress.total, 14, zone)
            XCTAssertEqual(window.daysRemaining(now: now, timeZone: timeZone), want.left, zone)
        }
    }

    func testTheFigureIsThisPeriodsPayNotYetInvoicedAndSaysWhenItWillBe() throws {
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(L10n.Earnings.currentPeriod, "This period, not invoiced yet")
        XCTAssertEqual(L10n.Earnings.invoicedOnClose("Nov 16, 2026"), "Invoiced when the period closes on Nov 16, 2026")
        for language in ["cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            XCTAssertTrue(L10n.Earnings.invoicedOnClose("16.").hasSuffix(" 16."), language)
            XCTAssertNotEqual(L10n.Earnings.currentPeriod, "This period, not invoiced yet", language)
        }
    }

    /// Both cards label the figure and name the day the period closes into an invoice, and neither promises a
    /// payout date: payouts are manual transfers until they are automated.
    func testBothPeriodCardsSayWhenThePayIsInvoicedAndNeverWhenItIsPaid() throws {
        for path in ["Dashboard/DashboardCards.swift", "Earnings/EarningsContent.swift"] {
            let source = try compactSource(path)
            XCTAssertTrue(source.contains("Text(L10n.Earnings.currentPeriod)"), path)
            XCTAssertTrue(source.contains("L10n.Earnings.invoicedOnClose(CalendarDay.text("), path)
            XCTAssertFalse(source.lowercased().contains("payout"), path)
        }
    }

    // MARK: - The next job's hero

    func testTheLabelFollowsTheJobsStatus() throws {
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(DashboardFormat.nextJobLabel(._2), "Next job")
        XCTAssertEqual(DashboardFormat.nextJobLabel(._3), "On the way")
        XCTAssertEqual(DashboardFormat.nextJobLabel(._4), "In progress")
        for language in ["cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            for status in [OrderStatus._2, ._3, ._4] {
                let label = DashboardFormat.nextJobLabel(status)
                XCTAssertFalse(label.isEmpty || label.hasPrefix("dash_"), "\(status) in \(language): \(label)")
            }
        }
    }

    func testNoStartIsADash() {
        XCTAssertEqual(DashboardFormat.nextJobWhen(nil, now: at(day: 9, hour: 10)), "—")
    }

    func testWithinTheHourItCountsTheMinutes() throws {
        let now = at(day: 9, hour: 10)
        let startsAt = now.addingTimeInterval(35 * 60 + 20)
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: english), "In 35m")
        L10n.bundle = try localeBundle("ru")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: russian), "Через 35м")
    }

    func testLaterTodayItNamesTheTime() throws {
        let now = at(day: 9, hour: 10)
        let startsAt = at(day: 9, hour: 13, minute: 30)
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: english), "Today 13:30")
        L10n.bundle = try localeBundle("ru")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: russian), "Сегодня 13:30")
    }

    /// Android counts the hours of a job less than a day out that is not today, tomorrow morning included.
    func testOvernightWithinADayItCountsHoursAndMinutes() throws {
        let now = at(day: 9, hour: 20)
        let startsAt = at(day: 10, hour: 8, minute: 30)
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: english), "In 12h 30m")
        L10n.bundle = try localeBundle("ru")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: russian), "Через 12ч 30м")
    }

    func testADayToTwoOutItSaysTomorrow() throws {
        let now = at(day: 9, hour: 10)
        let startsAt = at(day: 10, hour: 14)
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: english), "Tomorrow 14:00")
        L10n.bundle = try localeBundle("ru")
        XCTAssertEqual(DashboardFormat.nextJobWhen(startsAt, now: now, locale: russian), "Завтра 14:00")
    }

    /// 'Tomorrow' is the next calendar day, not the next 24 to 48 hours: on Monday evening a Wednesday-morning
    /// job, 39 hours away, read "Tomorrow 09:00".
    func testTwoCalendarDaysOutItNamesTheDayNotTomorrow() throws {
        L10n.bundle = try localeBundle("en")
        let when = DashboardFormat.nextJobWhen(at(day: 11, hour: 9), now: at(day: 9, hour: 18), locale: english)

        XCTAssertFalse(when.hasPrefix("Tomorrow"), when)
        XCTAssertTrue(when.hasSuffix(" · 09:00"), when)
    }

    func testLateTomorrowEveningIsStillTomorrow() throws {
        L10n.bundle = try localeBundle("en")
        let when = DashboardFormat.nextJobWhen(at(day: 10, hour: 23), now: at(day: 9, hour: 21), locale: english)

        XCTAssertEqual(when, "Tomorrow 23:00")
    }

    func testFurtherOutItNamesTheDayAndTheYearOnlyWhenItDiffers() throws {
        L10n.bundle = try localeBundle("en")
        let now = at(day: 9, hour: 10)
        let sameYear = DashboardFormat.nextJobWhen(at(day: 30, hour: 10), now: now, locale: english)
        XCTAssertTrue(sameYear.hasSuffix(" · 10:00"), sameYear)
        XCTAssertFalse(sameYear.contains("2026"), sameYear)
        let nextYear = DashboardFormat.nextJobWhen(
            at(year: 2027, month: 1, day: 15, hour: 9),
            now: now,
            locale: english
        )
        XCTAssertTrue(nextYear.hasSuffix(" · 09:00"), nextYear)
        XCTAssertTrue(nextYear.contains("2027"), nextYear)
        let past = DashboardFormat.nextJobWhen(at(day: 8, hour: 10), now: now, locale: english)
        XCTAssertTrue(past.hasSuffix(" · 10:00"), past)
        let inRussian = DashboardFormat.nextJobWhen(at(day: 30, hour: 10), now: now, locale: russian)
        XCTAssertNotEqual(inRussian, sameYear)
        XCTAssertTrue(inRussian.contains { ("а" ... "я").contains($0) }, inRussian)
    }

    // MARK: - The greeting's today line

    func testTheTodayLineCountsTheDaysJobs() throws {
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(DashboardGreeting.todayLine(jobsToday: 0), "You're free today")
        XCTAssertEqual(DashboardGreeting.todayLine(jobsToday: 1), "1 job today")
        XCTAssertEqual(DashboardGreeting.todayLine(jobsToday: 3), "3 jobs today")
    }

    /// An upcoming list never read, or whose read failed, is not a free day: the greeting shows the date alone.
    func testTheGreetingDrawsTheTodayLineOnlyWhileTheDaysJobsAreKnown() throws {
        let view = try compactSource("Dashboard/DashboardView.swift")

        XCTAssertTrue(view.contains("lettodaysJobsCount:Int?"), "the greeting cannot tell an unread list")
        XCTAssertTrue(
            view.contains("iflettodaysJobsCount{")
                && view.contains("Text(DashboardGreeting.todayLine(jobsToday:todaysJobsCount))"),
            "the today line is drawn whether or not the day's jobs are known"
        )
    }

    // MARK: - Helpers

    private let english = Locale(identifier: "en")
    private let russian = Locale(identifier: "ru")

    private func utcDay(_ text: String) throws -> Date {
        try XCTUnwrap(ISO8601DateFormatter().date(from: "\(text)T00:00:00Z"))
    }

    private func at(year: Int = 2026, month: Int = 11, day: Int, hour: Int, minute: Int = 0) -> Date {
        let components = DateComponents(year: year, month: month, day: day, hour: hour, minute: minute)
        return Calendar.current.date(from: components) ?? Date()
    }

    private func compactSource(_ path: String) throws -> String {
        let features = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features")
        return try String(contentsOf: features.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
