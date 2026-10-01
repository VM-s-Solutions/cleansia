import XCTest
@testable import CleansiaCore

/// Android's twin (`CleansiaBankAccountInput`) is held to the same cases; keep the two lists in step.
final class CleansiaBankAccountFieldPasteTests: XCTestCase {
    private func split(_ raw: String) -> [String?]? {
        CleansiaBankAccountField.splitPastedAccount(raw).map { [$0.prefix, $0.number, $0.bankCode] }
    }

    func testANumberAndBankCodeSplitAndClearThePrefix() {
        XCTAssertEqual(split("12321414/3545"), ["", "12321414", "3545"])
    }

    func testAllThreePartsSplit() {
        XCTAssertEqual(split("19-2000145399/0800"), ["19", "2000145399", "0800"])
    }

    /// Banking apps space the parts, and some use a no-break space or a narrow one.
    func testWhitespaceOfEveryKindIsIgnored() {
        XCTAssertEqual(split(" 19 - 2000145399 / 0800 "), ["19", "2000145399", "0800"])
        XCTAssertEqual(split("19\u{00A0}-\u{00A0}2000145399\u{202F}/\u{202F}0800"), ["19", "2000145399", "0800"])
        XCTAssertEqual(split("12321414 /\n3545"), ["", "12321414", "3545"])
    }

    func testAnEnOrEmDashReadsAsAHyphen() {
        XCTAssertEqual(split("19–2000145399/0800"), ["19", "2000145399", "0800"])
        XCTAssertEqual(split("19—2000145399/0800"), ["19", "2000145399", "0800"])
    }

    /// No bank code in the paste leaves the one already entered alone.
    func testAPrefixAndNumberWithoutABankCodeKeepTheBankCode() {
        XCTAssertEqual(split("19-2000145399"), ["19", "2000145399", nil])
    }

    /// A bare number is not split: it stays in the segment it was pasted into, where the digit clamp
    /// takes it — in the number box, that is the number.
    func testABareNumberIsNotSplit() {
        XCTAssertNil(split("2000145399"))
        XCTAssertNil(split("19"))
        XCTAssertNil(split(""))
    }

    func testAnythingThatIsNotAnAccountIsLeftToTheClamp() {
        for raw in [
            "1234567-1/0800",
            "12345678901/0800",
            "2000145399/08000",
            "20001/45/399",
            "19-20-2000145399/0800",
            "-2000145399/0800",
            "19-/0800",
            "2000145399/",
            "/0800",
            "12a4/0800",
            "١٢٣/0800"
        ] {
            XCTAssertNil(split(raw), raw)
        }
    }

    func testACzechIbanIsBrokenIntoItsDomesticParts() {
        XCTAssertEqual(split("CZ65 0800 0000 1920 0014 5399"), ["19", "2000145399", "0800"])
        XCTAssertEqual(split("cz6508000000192000145399"), ["19", "2000145399", "0800"])
    }

    func testASlovakIbanIsBrokenIntoItsDomesticParts() {
        XCTAssertEqual(split("SK31 1200 0000 1987 4263 7541"), ["19", "8742637541", "1200"])
    }

    /// Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is no
    /// prefix at all.
    func testIbanPaddingIsDropped() {
        XCTAssertEqual(split("CZ55 0800 0000 0000 0012 3457"), ["", "123457", "0800"])
    }

    func testOtherIbansAreNotDomesticAccounts() {
        for raw in [
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "CZ65 0800 0000 1920 0014 53990",
            "CZ6X 0800 0000 1920 0014 5399",
            "CZ00 0800 0000 0000 0000 0000"
        ] {
            XCTAssertNil(split(raw), raw)
        }
    }
}
