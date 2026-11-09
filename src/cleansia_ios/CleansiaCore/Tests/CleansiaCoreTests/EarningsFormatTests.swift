import XCTest
@testable import CleansiaCore

final class EarningsFormatTests: XCTestCase {
    func testDecimalMoneyKeepsTwoFractionDigits() {
        let result = EarningsFormat.decimalMoney(4200.5, currencyCode: nil)
        XCTAssertTrue(result.hasSuffix(".50") || result.hasSuffix(",50"), "expected two fraction digits, got \(result)")
    }

    func testWholeMoneyHasNoFractionDigits() {
        let result = EarningsFormat.wholeMoney(4200.5, currencyCode: nil)
        XCTAssertFalse(result.contains(".50"))
        XCTAssertFalse(result.contains(",50"))
    }

    func testDecimalAndWholeDifferForSameAmount() {
        let amount = 1234.0
        XCTAssertNotEqual(
            EarningsFormat.decimalMoney(amount, currencyCode: nil),
            EarningsFormat.wholeMoney(amount, currencyCode: nil)
        )
    }

    func testNegativeAmountFormatsWithSign() {
        let result = EarningsFormat.decimalMoney(-150, currencyCode: nil)
        XCTAssertTrue(result.contains("-"), "expected a negative sign for a deduction, got \(result)")
    }

    func testCurrencySymbolFallsBackToRawCodeForUnknownCode() {
        XCTAssertEqual(EarningsFormat.currencySymbol("ZZZ"), "ZZZ")
    }

    func testCurrencySymbolUsesLocaleDisplaySymbol() {
        XCTAssertEqual(EarningsFormat.currencySymbol("CZK", locale: Locale(identifier: "cs_CZ")), "Kč")
        XCTAssertEqual(EarningsFormat.currencySymbol("USD", locale: Locale(identifier: "en_US")), "$")
    }

    /// A currency's symbol belongs to the currency, not to the reader: English and Ukrainian know none for
    /// the koruna and printed "CZK" on Earnings, Invoices and Period pay while the dashboard printed "Kč".
    func testTheKorunaReadsKcInEveryPhoneLanguage() {
        for identifier in ["en_US", "uk_UA", "en_US@rg=czzzzz", "cs_CZ"] {
            let locale = Locale(identifier: identifier)
            XCTAssertEqual(EarningsFormat.currencySymbol("CZK", locale: locale), "Kč", identifier)
        }
    }

    func testCurrencySymbolHonorsCodeWhenLocaleIdentifierCarriesKeywords() {
        let locale = Locale(identifier: "en_US@rg=czzzzz")
        XCTAssertEqual(EarningsFormat.currencySymbol("CZK", locale: locale), "Kč")
        XCTAssertEqual(EarningsFormat.currencySymbol("EUR", locale: locale), "€")
    }

    func testCurrencySymbolNilForEmptyOrNil() {
        XCTAssertNil(EarningsFormat.currencySymbol(nil))
        XCTAssertNil(EarningsFormat.currencySymbol(""))
    }

    func testMoneyAppendsSymbolWhenCodeKnown() {
        let result = EarningsFormat.wholeMoney(100, currencyCode: "USD")
        XCTAssertTrue(result.contains("100"))
        XCTAssertTrue(result.contains("$") || result.contains("US$") || result.contains("USD"))
    }

    func testMoneyHasNoSymbolWhenCodeNil() {
        let result = EarningsFormat.wholeMoney(100, currencyCode: nil)
        XCTAssertFalse(result.contains(" "))
    }

    func testShortDateNilForNil() {
        XCTAssertNil(EarningsFormat.shortDate(nil))
    }

    func testShortDateProducesNonEmptyForDate() {
        let date = Date(timeIntervalSince1970: 1_750_000_000)
        XCTAssertFalse(EarningsFormat.shortDate(date)?.isEmpty ?? true)
    }

    func testShortDateLocalizesMonthPerAppLocale() {
        let date = Date(timeIntervalSince1970: 1_623_758_400)
        XCTAssertNotEqual(
            EarningsFormat.shortDate(date, locale: Locale(identifier: "ru")),
            EarningsFormat.shortDate(date, locale: Locale(identifier: "en"))
        )
    }

    /// A pay period's days arrive as midnight UTC; read in the phone's zone, the period began and ended a day
    /// early west of Greenwich.
    func testAPeriodIsTheDaysTheServerNamesInEveryZone() throws {
        let start = try day("2026-11-03")
        let end = try day("2026-11-16")
        for zone in ["America/Los_Angeles", "Europe/Prague", "Pacific/Kiritimati"] {
            try inSystemZone(zone) {
                XCTAssertEqual(
                    EarningsFormat.period(start: start, end: end, locale: english),
                    "Nov 3, 2026 – Nov 16, 2026",
                    zone
                )
            }
        }
    }

    func testAPeriodNamesItsDaysInTheLanguageGiven() throws {
        let czech = try XCTUnwrap(EarningsFormat.period(
            start: day("2026-11-03"),
            end: day("2026-11-16"),
            locale: Locale(identifier: "cs")
        ))
        XCTAssertFalse(czech.contains("Nov"), czech)
        XCTAssertTrue(czech.hasPrefix("3.") && czech.contains("16.") && czech.hasSuffix("2026"), czech)
    }

    func testAPeriodMissingADayIsNoPeriod() throws {
        XCTAssertNil(try EarningsFormat.period(start: nil, end: day("2026-11-16"), locale: english))
        XCTAssertNil(try EarningsFormat.period(start: day("2026-11-03"), end: nil, locale: english))
    }

    private let english = Locale(identifier: "en")

    private func day(_ text: String) throws -> Date {
        try XCTUnwrap(ISO8601DateFormatter().date(from: "\(text)T00:00:00Z"))
    }

    /// `TimeZone.current` is the system's zone, which `NSTimeZone.default` does not move.
    private func inSystemZone(_ identifier: String, _ body: () throws -> Void) rethrows {
        let saved = getenv("TZ").map { String(cString: $0) }
        setenv("TZ", identifier, 1)
        NSTimeZone.resetSystemTimeZone()
        defer {
            if let saved { setenv("TZ", saved, 1) } else { unsetenv("TZ") }
            NSTimeZone.resetSystemTimeZone()
        }
        try body()
    }
}
