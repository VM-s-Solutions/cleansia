import CleansiaCore
import XCTest
@testable import CleansiaPartner

@MainActor
final class OrderNotesViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        snackbar = SnackbarController()
    }

    private func makeVM(orderId: String = "o1") -> OrderNotesViewModel {
        OrderNotesViewModel(orderId: orderId, client: client, snackbar: snackbar)
    }

    // MARK: add / update / delete

    func testAddNoteSendsCommandAndSignalsParent() async {
        let vm = makeVM()
        var mutatedFired = false
        let token = vm.mutated.sink { mutatedFired = true }
        defer { token.cancel() }

        await vm.addNote("A new note")

        XCTAssertEqual(client.noteCommands.map(\.name), ["addNote"])
        XCTAssertEqual(client.noteCommands.first?.content, "A new note")
        XCTAssertTrue(mutatedFired)
        XCTAssertFalse(vm.isSavingNote)
    }

    func testBlankNoteIsNotSent() async {
        let vm = makeVM()
        await vm.addNote("   ")
        XCTAssertTrue(client.noteCommands.isEmpty)
    }

    func testUpdateAndDeleteNoteSendForTheRowId() async {
        let vm = makeVM()
        await vm.updateNote("note-7", "edited")
        await vm.deleteNote("note-7")
        XCTAssertEqual(client.noteCommands.map(\.name), ["updateNote", "deleteNote"])
        XCTAssertEqual(client.noteCommands.allSatisfy { $0.id == "note-7" || $0.name == "updateNote" }, true)
        XCTAssertNil(vm.mutatingId)
    }

    func testReportUpdateDeleteIssue() async {
        let vm = makeVM()
        await vm.reportIssue("broken tap")
        await vm.updateIssue("issue-3", "leaking tap")
        await vm.deleteIssue("issue-3")
        XCTAssertEqual(client.noteCommands.map(\.name), ["reportIssue", "updateIssue", "deleteIssue"])
    }

    // MARK: mutatingId in-flight + re-entry guard (under concurrency)

    func testMutatingIdTracksTheRowDuringFlightThenClears() async {
        client.suspendCommands = true
        let vm = makeVM()

        let task = Task { await vm.deleteNote("note-9") }
        while client.noteCommands.isEmpty {
            await Task.yield()
        }
        XCTAssertEqual(vm.mutatingId, "note-9") // (a) tracks the row id mid-flight

        client.resumeCommand()
        await task.value
        XCTAssertNil(vm.mutatingId)
    }

    func testConcurrentSecondMutationIsDroppedByReentryGuard() async {
        client.suspendCommands = true
        let vm = makeVM()

        let first = Task { await vm.updateNote("note-1", "first") }
        while client.noteCommands.isEmpty {
            await Task.yield()
        }
        // (b) a concurrent second mutation is dropped while mutatingId is set.
        await vm.deleteNote("note-2")
        XCTAssertEqual(client.noteCommands.count, 1)

        client.resumeCommand()
        await first.value
        XCTAssertNil(vm.mutatingId)
    }

    func testFailureSurfacesSnackbarAndDoesNotSignalParent() async {
        client.commandResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        var mutatedFired = false
        let token = vm.mutated.sink { mutatedFired = true }
        defer { token.cancel() }

        await vm.addNote("note")

        XCTAssertNotNil(snackbar.current)
        XCTAssertFalse(mutatedFired)
        XCTAssertFalse(vm.isSavingNote)
    }

    // MARK: author-only edit/delete gate

    func testIsAuthorTrueForOwnId() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.resolveCurrentEmployeeId()
        XCTAssertTrue(vm.isAuthor(noteEmployeeId: "emp-self"))
    }

    func testIsAuthorFalseForOtherId() async {
        client.employeeIdResult = .success("emp-self")
        let vm = makeVM()
        await vm.resolveCurrentEmployeeId()
        XCTAssertFalse(vm.isAuthor(noteEmployeeId: "emp-other"))
    }

    func testIsAuthorFalseWhenUnresolvedOrNil() {
        let vm = makeVM() // currentEmployeeId not resolved
        XCTAssertFalse(vm.isAuthor(noteEmployeeId: "emp-self"))
        XCTAssertFalse(vm.isAuthor(noteEmployeeId: nil))
    }
}
