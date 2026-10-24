import XCTest
@testable import CleansiaCore

/// Android's twin (`CleansiaBankAccountInput`) and the web's (`cleansia-bank-account`) are held to the same
/// cases; keep the lists in step.
final class CleansiaBankAccountFieldPasteTests: XCTestCase {
    private let everyBox: [CleansiaBankAccountField.Segment] = [.prefix, .number, .bankCode]

    private func split(
        _ raw: String,
        into box: CleansiaBankAccountField.Segment,
        replacing current: String = ""
    ) -> [String?]? {
        CleansiaBankAccountField.splitPastedAccount(raw, replacing: current, into: box)
            .map { [$0.prefix, $0.number, $0.bankCode] }
    }

    /// A written-out account fills all three boxes, whichever one it was pasted into.
    private func assertSplitsFromEveryBox(
        _ raw: String,
        _ expected: [String?],
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        for box in everyBox {
            XCTAssertEqual(split(raw, into: box), expected, "\(raw) into the \(box) box", file: file, line: line)
        }
    }

    /// The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones.
    func testASegmentKeepsASCIIDigitsOnlyUpToItsLength() {
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("١٢٣123", maxLength: 6), "123")
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("１２３", maxLength: 6), "")
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("08 00x99", maxLength: 4), "0800")
    }

    func testANumberAndBankCodeSplitAndClearThePrefix() {
        assertSplitsFromEveryBox("12321414/3545", ["", "12321414", "3545"])
    }

    func testAllThreePartsSplit() {
        assertSplitsFromEveryBox("19-2000145399/0800", ["19", "2000145399", "0800"])
    }

    /// Banking apps space the parts, and some use a no-break space or a narrow one.
    func testWhitespaceOfEveryKindIsIgnored() {
        assertSplitsFromEveryBox(" 19 - 2000145399 / 0800 ", ["19", "2000145399", "0800"])
        assertSplitsFromEveryBox("19\u{00A0}-\u{00A0}2000145399\u{202F}/\u{202F}0800", ["19", "2000145399", "0800"])
        assertSplitsFromEveryBox("12321414 /\n3545", ["", "12321414", "3545"])
    }

    func testAnEnOrEmDashReadsAsAHyphen() {
        assertSplitsFromEveryBox("19–2000145399/0800", ["19", "2000145399", "0800"])
        assertSplitsFromEveryBox("19—2000145399/0800", ["19", "2000145399", "0800"])
    }

    /// No bank code in the paste leaves the one already entered alone.
    func testAPrefixAndNumberWithoutABankCodeKeepTheBankCode() {
        assertSplitsFromEveryBox("19-2000145399", ["19", "2000145399", nil])
    }

    /// A part copied on its own is pasted back into its own box, and the number already entered stays.
    func testABareBankCodeOrPrefixThatFitsItsOwnBoxStaysThere() {
        XCTAssertNil(split("0800", into: .bankCode))
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("0800", maxLength: 4), "0800")
        XCTAssertNil(split("19", into: .prefix))
        XCTAssertNil(split("000019", into: .prefix))
        XCTAssertNil(split("5500", into: .bankCode, replacing: "0800"), "pasted over the box's own digits")
    }

    /// A bare number too long for the box it landed in is the account number, so "2000145399" pasted into
    /// the empty prefix box does not become the prefix "200014". The prefix and bank code already entered
    /// stay.
    func testABareNumberTooLongForItsBoxGoesToTheNumberAndLeavesTheOtherTwo() {
        XCTAssertEqual(split("2000145399", into: .prefix), [nil, "2000145399", nil])
        XCTAssertEqual(split("2000 1453 99", into: .prefix), [nil, "2000145399", nil])
        XCTAssertEqual(split("1234567", into: .prefix), [nil, "1234567", nil])
        XCTAssertEqual(split("12345", into: .bankCode), [nil, "12345", nil])
        XCTAssertEqual(
            split("2000145399", into: .bankCode, replacing: "0800"),
            [nil, "2000145399", nil],
            "pasted over a bank code"
        )
    }

    func testABareNumberPastedIntoTheNumberBoxStaysThere() {
        XCTAssertNil(split("2000145399", into: .number))
        XCTAssertNil(split("19", into: .number))
    }

    /// The number pad types one digit at a time, and a typed prefix or bank code must stay where it is.
    func testTypingNeverJumpsToTheNumber() {
        for box in everyBox {
            XCTAssertNil(split("5", into: box), "\(box)")
            XCTAssertNil(split("", into: box), "\(box)")
            XCTAssertNil(split("", into: box, replacing: "19"), "\(box)")
        }
        XCTAssertNil(split("12", into: .prefix, replacing: "1"))
        XCTAssertNil(split("1234567", into: .prefix, replacing: "123456"))
        XCTAssertNil(split("08001", into: .bankCode, replacing: "0800"))
    }

    func testABareRunTooLongForANumberIsLeftToTheClamp() {
        for box in everyBox {
            XCTAssertNil(split("20001453991", into: box), "\(box)")
        }
        XCTAssertNil(
            split("08002000145399", into: .bankCode, replacing: "0800"),
            "pasted after a bank code already there"
        )
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
            for box in everyBox {
                XCTAssertNil(split(raw, into: box), "\(raw) into the \(box) box")
            }
        }
    }

    func testACzechIbanIsBrokenIntoItsDomesticParts() {
        assertSplitsFromEveryBox("CZ65 0800 0000 1920 0014 5399", ["19", "2000145399", "0800"])
        assertSplitsFromEveryBox("cz6508000000192000145399", ["19", "2000145399", "0800"])
    }

    func testASlovakIbanIsBrokenIntoItsDomesticParts() {
        assertSplitsFromEveryBox("SK31 1200 0000 1987 4263 7541", ["19", "8742637541", "1200"])
    }

    /// Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is no
    /// prefix at all.
    func testIbanPaddingIsDropped() {
        assertSplitsFromEveryBox("CZ55 0800 0000 0000 0012 3457", ["", "123457", "0800"])
    }

    func testOtherIbansAreNotDomesticAccounts() {
        for raw in [
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "CZ65 0800 0000 1920 0014 53990",
            "CZ6X 0800 0000 1920 0014 5399",
            "CZ00 0800 0000 0000 0000 0000"
        ] {
            for box in everyBox {
                XCTAssertNil(split(raw, into: box), "\(raw) into the \(box) box")
            }
        }
    }
}
