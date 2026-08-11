import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The server decides disclosure on `CanAccessOrderAsync` and reports `isAssignedToCurrentUser` off
/// the assignment list. Those disagree for the employee who books a cleaning for their own home —
/// entitled, not assigned — so every gate here is driven at that shape: the field **populated** and
/// the flag **false**. A case with the flag true proves nothing, and one with the field blank proves
/// the server's behaviour rather than the client's.
final class OrderDetailRedactionGateTests: XCTestCase {
    func testTheContactChipsRenderForAnEntitledNonAssignee() throws {
        XCTAssertTrue(try detail(isMine: false, phone: "+420 777 111 222").showsCustomerContact)
    }

    func testTheContactChipsStayHiddenOnAPhoneTheServerBlanked() throws {
        // `OrderPiiRedaction` sends "" for a browsing cleaner, never null.
        for withheld in [nil, "", "   "] {
            XCTAssertFalse(
                try detail(isMine: true, phone: withheld).showsCustomerContact,
                "\(withheld.debugDescription) is a redacted phone, not a dialable one"
            )
        }
    }

    func testTheAccessCardRendersForAnEntitledNonAssignee() throws {
        XCTAssertTrue(try detail(status: 3, isMine: false, access: "Code 1234 at the gate.").showsAccessCard)
    }

    func testTheAccessCardKeepsItsLifecycleTerm() throws {
        for live in [3, 4] {
            XCTAssertTrue(
                try detail(status: live, isMine: false, access: "Code 1234.").showsAccessCard,
                "status \(live) is when a door code is useful"
            )
        }
        for quiet in [0, 2, 5, 6] {
            XCTAssertFalse(
                try detail(status: quiet, isMine: true, access: "Code 1234.").showsAccessCard,
                "status \(quiet) must not leave a door code on screen"
            )
        }
    }

    func testTheAccessCardStaysHiddenWhenTheServerWithheldTheInstructions() throws {
        for withheld in [nil, "", "   "] {
            XCTAssertFalse(
                try detail(status: 4, isMine: true, access: withheld).showsAccessCard,
                "\(withheld.debugDescription) is a withheld instruction, not an empty card"
            )
        }
    }

    func testNotesAndIssuesRenderForAnEntitledNonAssignee() throws {
        XCTAssertTrue(
            try detail(status: 4, isMine: false, notes: [OrderNoteDto(id: "n1", content: "Gate was locked.")])
                .showsNotesAndIssues
        )
        XCTAssertTrue(
            try detail(status: 4, isMine: false, issues: [OrderIssueDto(id: "i1", description: "Broken tap.")])
                .showsNotesAndIssues
        )
    }

    /// The record of what was reported during the job outlives the job, on both sides of the flag.
    func testNotesAndIssuesSurviveOntoATerminalOrder() throws {
        XCTAssertTrue(
            try detail(status: 5, isMine: false, notes: [OrderNoteDto(id: "n1", content: "Gate was locked.")])
                .showsNotesAndIssues
        )
    }

    func testNotesAndIssuesStayHiddenWhenTheServerSentEmptyLists() throws {
        XCTAssertFalse(try detail(status: 5, isMine: false).showsNotesAndIssues)
        XCTAssertFalse(try detail(status: 5, isMine: true).showsNotesAndIssues)
    }

    func testTheSectionStillOpensForAnAssigneeWithNothingRecordedYet() throws {
        XCTAssertTrue(try detail(status: 3, isMine: true).showsNotesAndIssues)
    }

    /// Writing a note is an **action**, so it keeps the assignment term and fails closed — withdrawing
    /// it would offer an entitled non-assignee a write the server refuses.
    func testAddingANoteStaysGatedOnTheAssignment() throws {
        for status in [0, 2, 3, 4, 5, 6] {
            XCTAssertFalse(
                try detail(status: status, isMine: false).canAddNotes,
                "status \(status) must not offer a write to a cleaner who has not taken the order"
            )
        }
        for live in [3, 4] {
            XCTAssertTrue(try detail(status: live, isMine: true).canAddNotes)
        }
        for quiet in [0, 2, 5, 6] {
            XCTAssertFalse(
                try detail(status: quiet, isMine: true).canAddNotes,
                "status \(quiet) is not work in flight"
            )
        }
    }

    private func detail(
        status: Int = 4,
        isMine: Bool,
        phone: String? = nil,
        access: String? = nil,
        notes: [OrderNoteDto] = [],
        issues: [OrderIssueDto] = []
    ) throws -> OrderDetail {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.isAssignedToCurrentUser = isMine
        item.customerPhone = phone
        item.accessInstructions = access
        item.orderNotes = notes
        item.orderIssues = issues
        return try OrderDetail(item)
    }
}
