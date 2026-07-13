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
        XCTAssertEqual(EarningsFormat.currencySymbol("CZK", locale: Locale(identifier: "en_US")), "CZK")
    }

    func testCurrencySymbolHonorsCodeWhenLocaleIdentifierCarriesKeywords() {
        let locale = Locale(identifier: "en_US@rg=czzzzz")
        XCTAssertEqual(EarningsFormat.currencySymbol("CZK", locale: locale), "CZK")
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
}
