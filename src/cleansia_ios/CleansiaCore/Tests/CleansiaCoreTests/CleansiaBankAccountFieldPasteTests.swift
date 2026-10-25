import XCTest
@testable import CleansiaCore

/// What an edit of one box leaves in the bank account field. Android's twin (`CleansiaBankAccountInput`) and
/// the web's (`cleansia-bank-account`) pin the same case list, row for row; keep the three in step.
final class CleansiaBankAccountFieldPasteTests: XCTestCase {
    private typealias Box = CleansiaBankAccountField.Segment
    private typealias Edit = CleansiaBankAccountField.Edit

    private let everyBox: [Box] = [.prefix, .number, .bankCode]

    private func edit(_ raw: String, into box: Box, replacing current: String = "") -> Edit {
        CleansiaBankAccountField.splitPastedAccount(raw, replacing: current, into: box)
    }

    /// A written-out account fills all three boxes, whichever one it was pasted into.
    private func assertSplitsFromEveryBox(
        _ raw: String,
        _ expected: Edit,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        for box in everyBox {
            XCTAssertEqual(edit(raw, into: box), expected, "\(raw) into the \(box) box", file: file, line: line)
        }
    }

    /// Typing that turns `held` into `raw` leaves `kept` in the box and the other two empty, and goes through to the
    /// digit clamp, so the keyboard stays.
    private func assertTyping(
        _ raw: String,
        into box: Box,
        holding held: String,
        leaves kept: String,
        _ message: String = "",
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        var start = CleansiaBankAccountField.Boxes(prefix: "", number: "", bankCode: "")
        start[box] = held
        var end = start
        end[box] = kept
        let typed = CleansiaBankAccountField.boxes(afterEditing: box, to: raw, from: start)
        let typing = "\(raw) into the \(box) box \(message)"
        XCTAssertEqual(typed.boxes, end, typing, file: file, line: line)
        XCTAssertFalse(typed.rebuilds, "\(typing) rebuilds the boxes", file: file, line: line)
    }

    private static func maxLength(of box: Box) -> Int {
        switch box {
        case .prefix: 6
        case .number: 10
        case .bankCode: 4
        }
    }

    /// Each row starts with the other two boxes empty and ends with what the three boxes hold, as prefix, number and
    /// bank code. An edit the control lets through as typed keeps the keyboard; any other rebuilds the boxes.
    func testTheCaseListTheTwinsShare() {
        let account = ["19", "2000145399", "0800"]
        let rows = [
            PasteRow(1, into: .bankCode, holding: "", paste: "0800", leaves: ["", "", "0800"]),
            PasteRow(2, into: .bankCode, holding: "0800", paste: "5500", leaves: ["", "", "5500"]),
            PasteRow(3, into: .bankCode, holding: "0800", paste: "5500", .before, leaves: ["", "", "5500"]),
            PasteRow(4, into: .bankCode, holding: "0800", paste: "55", .over("08"), leaves: ["", "", "5500"]),
            PasteRow(5, into: .bankCode, holding: "08", paste: "00", leaves: ["", "", "0800"]),
            PasteRow(6, into: .bankCode, holding: "08", paste: "0800", leaves: ["", "", "0800"]),
            PasteRow(7, into: .bankCode, holding: "0800", paste: "2000145399", leaves: ["", "2000145399", "0800"]),
            PasteRow(8, into: .bankCode, holding: "", paste: "2000145399", leaves: ["", "2000145399", ""]),
            PasteRow(9, into: .prefix, holding: "12345", paste: "67", leaves: ["67", "", ""]),
            PasteRow(10, into: .prefix, holding: "", paste: "19", leaves: ["19", "", ""]),
            PasteRow(11, into: .number, holding: "", paste: "19-2000145399/0800", leaves: account),
            PasteRow(12, into: .number, holding: "", paste: "CZ65 0800 0000 1920 0014 5399", leaves: account),
            PasteRow(13, into: .bankCode, holding: "0800", paste: "5", leaves: ["", "", "0800"]),
            PasteRow(14, into: .number, holding: "2000145399", paste: "1234567890", leaves: ["", "1234567890", ""]),
            PasteRow(15, into: .number, holding: "", paste: "12345/0800", leaves: ["", "12345", "0800"]),
            PasteRow(16, into: .number, holding: "", paste: "19-1234567", leaves: ["19", "1234567", ""]),
            PasteRow(17, into: .bankCode, holding: "0800", paste: "5", .before, leaves: ["", "", "5080"]),
            PasteRow(18, into: .bankCode, holding: "0800", paste: "19-2000145399/0800", .over("0800"), leaves: account),
            PasteRow(
                19,
                into: .number,
                holding: "1234567890",
                paste: "19-2000145399/0801",
                .over("1234567890"),
                leaves: ["19", "2000145399", "0801"]
            ),
            PasteRow(20, into: .bankCode, holding: "0800", paste: "19-2000145399/0800", leaves: account),
            PasteRow(
                21,
                into: .number,
                holding: "2000145399",
                paste: "2000145399/0800",
                .over("2000145399"),
                leaves: ["", "2000145399", "0800"]
            ),
            PasteRow(22, into: .number, holding: "19", paste: "19-2000145399/0800", .before, leaves: account),
            PasteRow(
                23,
                into: .bankCode,
                holding: "0800",
                paste: "19-2000145399/0800",
                .caret(after: "0"),
                leaves: account
            ),
            PasteRow(24, into: .bankCode, holding: "0", paste: "19-2000145399/0800", .over("0"), leaves: account),
            PasteRow(26, into: .prefix, holding: "0", paste: "19-2000145399/0800", leaves: account),
            PasteRow(27, into: .prefix, holding: "0", paste: "1234567890", leaves: ["0", "1234567890", ""])
        ]
        let typedThrough: Set = [1, 3, 4, 5, 10, 13, 17]
        for row in rows {
            var held = CleansiaBankAccountField.Boxes(prefix: "", number: "", bankCode: "")
            held[row.box] = row.held
            let (boxes, rebuilds) = CleansiaBankAccountField.boxes(afterEditing: row.box, to: row.newText, from: held)
            XCTAssertEqual([boxes.prefix, boxes.number, boxes.bankCode], row.leaves, "row \(row.number)")
            XCTAssertEqual(rebuilds, !typedThrough.contains(row.number), "row \(row.number) rebuilds the boxes")
        }
    }

    /// The known residual, pinned so that changing it is a choice: seeing only a box's old and new text, the control
    /// can misread an account pasted inside a box's digits or over part of them, one pasted over all of digits it
    /// begins with, and bare digits pasted before digits that start the same way, which the web, reading the pasted
    /// text, gets right.
    func testTheKnownResidualAPasteIsReadWithoutDigitsItSharesWithWhatTheBoxHeld() {
        let held = CleansiaBankAccountField.Boxes(prefix: "", number: "1234567890", bankCode: "")
        let newText = "1234567890".replacingOccurrences(of: "12345", with: "19-2000145399/0800")
        let (boxes, rebuilds) = CleansiaBankAccountField.boxes(afterEditing: .number, to: newText, from: held)
        XCTAssertEqual(boxes, CleansiaBankAccountField.Boxes(prefix: "9", number: "2000145399", bankCode: "0800"))
        XCTAssertTrue(rebuilds)

        let selectedAll = CleansiaBankAccountField.Boxes(prefix: "", number: "2000", bankCode: "")
        let overAll = CleansiaBankAccountField.boxes(afterEditing: .number, to: "2000145399/0800", from: selectedAll)
        XCTAssertEqual(
            overAll.boxes,
            CleansiaBankAccountField.Boxes(prefix: "", number: "145399", bankCode: "0800"),
            "2000145399/0800 pasted over all of the number 2000"
        )
        XCTAssertTrue(overAll.rebuilds)
    }

    /// The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones.
    func testASegmentKeepsASCIIDigitsOnlyUpToItsLength() {
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("١٢٣123", maxLength: 6), "123")
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("１２３", maxLength: 6), "")
        XCTAssertEqual(CleansiaBankAccountField.clampSegment("08 00x99", maxLength: 4), "0800")
    }

    func testANumberAndBankCodeSplitAndClearThePrefix() {
        assertSplitsFromEveryBox("12321414/3545", .account(prefix: "", number: "12321414", bankCode: "3545"))
    }

    func testAllThreePartsSplit() {
        assertSplitsFromEveryBox("19-2000145399/0800", .account(prefix: "19", number: "2000145399", bankCode: "0800"))
    }

    /// Banking apps space the parts, and some use a no-break space or a narrow one.
    func testWhitespaceOfEveryKindIsIgnored() {
        let account: Edit = .account(prefix: "19", number: "2000145399", bankCode: "0800")
        assertSplitsFromEveryBox(" 19 - 2000145399 / 0800 ", account)
        assertSplitsFromEveryBox("19\u{00A0}-\u{00A0}2000145399\u{202F}/\u{202F}0800", account)
        assertSplitsFromEveryBox("12321414 /\n3545", .account(prefix: "", number: "12321414", bankCode: "3545"))
    }

    func testAnEnOrEmDashReadsAsAHyphen() {
        let account: Edit = .account(prefix: "19", number: "2000145399", bankCode: "0800")
        assertSplitsFromEveryBox("19–2000145399/0800", account)
        assertSplitsFromEveryBox("19—2000145399/0800", account)
    }

    /// No bank code in the paste leaves the one already entered alone.
    func testAPrefixAndNumberWithoutABankCodeKeepTheBankCode() {
        assertSplitsFromEveryBox("19-2000145399", .account(prefix: "19", number: "2000145399", bankCode: nil))
    }

    /// Pasted after the digits a box holds, the account is what was pasted, not those digits run into it.
    func testAnAccountPastedBesideDigitsIsReadWithoutThem() {
        XCTAssertEqual(
            edit("12319-2000145399/0800", into: .number, replacing: "123"),
            .account(prefix: "19", number: "2000145399", bankCode: "0800")
        )
    }

    /// A part copied on its own is pasted back into its own box, and the number already entered stays.
    func testABareBankCodeOrPrefixThatFitsItsOwnBoxStaysThere() {
        XCTAssertEqual(edit("0800", into: .bankCode), .box("0800"))
        XCTAssertEqual(edit("19", into: .prefix), .box("19"))
        XCTAssertEqual(edit("000019", into: .prefix), .box("000019"))
        XCTAssertEqual(edit("5500", into: .bankCode, replacing: "0800"), .box("5500"), "pasted over the box's digits")
        XCTAssertEqual(edit("1234567", into: .prefix, replacing: "123"), .box("4567"), "pasted after the box's digits")
    }

    /// A bare number too long for the box it landed in is the account number, so "2000145399" pasted into
    /// the empty prefix box does not become the prefix "200014". The box it landed in keeps what it held.
    func testABareNumberTooLongForItsBoxGoesToTheNumberAndLeavesTheOtherTwo() {
        XCTAssertEqual(edit("2000145399", into: .prefix), .number("2000145399"))
        XCTAssertEqual(edit("2000 1453 99", into: .prefix), .number("2000145399"))
        XCTAssertEqual(edit("1234567", into: .prefix), .number("1234567"))
        XCTAssertEqual(edit("12345", into: .bankCode), .number("12345"))
        XCTAssertEqual(
            edit("2000145399", into: .bankCode, replacing: "0800"),
            .number("2000145399"),
            "pasted over a bank code"
        )
    }

    func testABareNumberPastedIntoTheNumberBoxStaysThere() {
        XCTAssertEqual(edit("2000145399", into: .number), .box("2000145399"))
        XCTAssertEqual(edit("19", into: .number), .box("19"))
    }

    /// The number pad types one digit at a time, and typing never moves a digit to another box. A digit that fits
    /// stays where it was typed; one typed into a full box goes to the box's digit clamp, and the keyboard stays: at
    /// the end of the box nothing changes, and before or inside it the box's last digit falls out. A `-` or `/` typed
    /// among a box's digits, which takes a hardware keyboard, is dropped by the clamp like any other non-digit.
    func testTypingNeverJumpsToTheNumber() {
        for box in everyBox {
            XCTAssertEqual(edit("5", into: box), .box("5"), "\(box)")
            XCTAssertEqual(edit("", into: box), .box(""), "\(box)")
            XCTAssertEqual(edit("", into: box, replacing: "19"), .box(""), "\(box)")
        }
        XCTAssertEqual(edit("12", into: .prefix, replacing: "1"), .box("12"))
        assertTyping("1234567", into: .prefix, holding: "123456", leaves: "123456")
        assertTyping("08001", into: .bankCode, holding: "0800", leaves: "0800")
        assertTyping("50800", into: .bankCode, holding: "0800", leaves: "5080", "typed before the code")
        assertTyping("08500", into: .bankCode, holding: "0800", leaves: "0850", "typed inside the code")
        assertTyping("1-2345", into: .prefix, holding: "12345", leaves: "12345", "row 25, after the 1")
        assertTyping("200014/5399", into: .number, holding: "2000145399", leaves: "2000145399", "row 25b, after 200014")
    }

    /// A run of digits too long even for the number goes nowhere: the box keeps what it held.
    func testABareRunTooLongForANumberIsRefused() {
        for box in everyBox {
            XCTAssertEqual(edit("20001453991", into: box), .box(""), "\(box)")
        }
        XCTAssertEqual(
            edit("080020001453991", into: .bankCode, replacing: "0800"),
            .box("0800"),
            "pasted after a bank code already there"
        )
    }

    /// Text that is not an account is never split. Where it fits its box it is an ordinary edit, which the
    /// digit clamp takes; where it does not, the box keeps what it held.
    func testAnythingThatIsNotAnAccountIsNeverSplit() {
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
                let expected: Edit = raw.count <= Self.maxLength(of: box) ? .box(raw) : .box("")
                XCTAssertEqual(edit(raw, into: box), expected, "\(raw) into the \(box) box")
            }
        }
    }

    func testACzechIbanIsBrokenIntoItsDomesticParts() {
        let account: Edit = .account(prefix: "19", number: "2000145399", bankCode: "0800")
        assertSplitsFromEveryBox("CZ65 0800 0000 1920 0014 5399", account)
        assertSplitsFromEveryBox("cz6508000000192000145399", account)
    }

    func testASlovakIbanIsBrokenIntoItsDomesticParts() {
        assertSplitsFromEveryBox(
            "SK31 1200 0000 1987 4263 7541",
            .account(prefix: "19", number: "8742637541", bankCode: "1200")
        )
    }

    /// Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is no
    /// prefix at all.
    func testIbanPaddingIsDropped() {
        assertSplitsFromEveryBox(
            "CZ55 0800 0000 0000 0012 3457",
            .account(prefix: "", number: "123457", bankCode: "0800")
        )
    }

    /// Too long for any box, and no account the control can split: the box keeps what it held.
    func testOtherIbansAreNotDomesticAccounts() {
        for raw in [
            "DE89 3704 0044 0532 0130 00",
            "CZ65 0800 0000 1920 0014 539",
            "CZ65 0800 0000 1920 0014 53990",
            "CZ6X 0800 0000 1920 0014 5399",
            "CZ00 0800 0000 0000 0000 0000"
        ] {
            for box in everyBox {
                XCTAssertEqual(edit(raw, into: box), .box(""), "\(raw) into the \(box) box")
            }
        }
    }
}

/// One row of the shared case list: the box, what it held, what was pasted or typed and where (after what the box
/// held, unless placed otherwise), and what the three boxes hold after it.
private struct PasteRow {
    enum Place {
        case after, before
        case over(String)
        case caret(after: String)
    }

    let number: Int
    let box: CleansiaBankAccountField.Segment
    let held: String
    let newText: String
    let leaves: [String]

    init(
        _ number: Int,
        into box: CleansiaBankAccountField.Segment,
        holding held: String,
        paste: String,
        _ place: Place = .after,
        leaves: [String]
    ) {
        self.number = number
        self.box = box
        self.held = held
        switch place {
        case .after: newText = held + paste
        case .before: newText = paste + held
        case let .over(selection): newText = held.replacingOccurrences(of: selection, with: paste)
        case let .caret(after: start): newText = start + paste + held.dropFirst(start.count)
        }
        self.leaves = leaves
    }
}
