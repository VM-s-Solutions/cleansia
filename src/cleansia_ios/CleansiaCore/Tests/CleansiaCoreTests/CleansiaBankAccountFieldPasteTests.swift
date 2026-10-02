import XCTest
@testable import CleansiaCore

/// Android's twin (`CleansiaBankAccountInput`) is held to the same cases; keep the two lists in step.
final class CleansiaBankAccountFieldPasteTests: XCTestCase {
    private func split(_ raw: String, replacing current: String = "") -> [String?]? {
        CleansiaBankAccountField.splitPastedAccount(raw, replacing: current)
            .map { [$0.prefix, $0.number, $0.bankCode] }
    }

    /// The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones.
    func testASegmentKeepsASCIIDigitsOnlyUpToItsLength() {
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("١٢٣123", maxLength: 6), "123")
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("１２３", maxLength: 6), "")
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("08 00x99", maxLength: 4), "0800")
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

    /// A bare number is the account number whichever box received it, so "2000145399" pasted into the
    /// empty prefix box no longer becomes the prefix "200014". The split never learns which box it was:
    /// these are the prefix, number and bank-code boxes alike, and prefix and bank code are left alone.
    func testABarePastedNumberGoesToTheNumberAndLeavesTheOtherTwo() {
        XCTAssertEqual(split("2000145399"), [nil, "2000145399", nil])
        XCTAssertEqual(split("2000 1453 99"), [nil, "2000145399", nil])
        XCTAssertEqual(split("2000145399", replacing: "0800"), [nil, "2000145399", nil], "pasted over a bank code")
        // Owner decision D14 as written: two digits pasted into the empty prefix box are the number too.
        XCTAssertEqual(split("19"), [nil, "19", nil])
    }

    /// The number pad types one digit at a time, and a typed prefix or bank code must stay where it is.
    func testTypingNeverJumpsToTheNumber() {
        XCTAssertNil(split("5"))
        XCTAssertNil(split("12", replacing: "1"))
        XCTAssertNil(split("08001", replacing: "0800"))
        XCTAssertNil(split(""))
        XCTAssertNil(split("", replacing: "19"))
    }

    func testABareRunTooLongForANumberIsLeftToTheClamp() {
        XCTAssertNil(split("20001453991"))
        XCTAssertNil(split("08002000145399", replacing: "0800"), "pasted after a bank code already there")
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
