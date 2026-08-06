import XCTest
@testable import CleansiaCustomer

/// `RecurringBookingRepository.update` existed and was unit-tested on Android for a full release with
/// zero production callers — no route, no button — so the edit was unreachable while every suite stayed
/// green. These pin the chain end to end: list → route → form → repository, and the same way the pause
/// and delete buttons stay reachable after a membership lapses.
final class RecurringEditReachabilityTests: XCTestCase {
    private static let list = "CleansiaCustomer/Sources/Features/Recurring/RecurringBookingsScreen.swift"
    private static let shell = "CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift"
    private static let form = "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringViewModel.swift"
    private static let home = "CleansiaCustomer/Sources/Features/Home/HomeTabViewModel.swift"

    func testTheListOffersAnEditAction() throws {
        let source = try read(Self.list)
        XCTAssertTrue(source.contains("label: L10n.Recurring.edit"), "no edit affordance on a template card")
        XCTAssertTrue(source.contains("action: onEdit"), "the edit affordance calls nothing")
    }

    func testTheShellPushesAndBuildsTheEditForm() throws {
        let source = try read(Self.shell)
        XCTAssertTrue(
            source.contains("onEdit: { model.path.append(ShellRoute.editRecurring(templateId: $0.id)) }"),
            "the list's edit callback pushes no route"
        )
        XCTAssertTrue(source.contains("case let .editRecurring(templateId)"), "the route has no destination")
        XCTAssertTrue(source.contains("editing: template"), "the destination builds a blank create form")
    }

    func testTheFormSubmitsThroughUpdateWhenEditing() throws {
        let source = try read(Self.form)
        XCTAssertTrue(source.contains("await repository.update(UpdateRecurringInput("), "editing still creates")
    }

    /// A lapsed membership does not stop a schedule, so the list — and the pause and delete
    /// buttons on it — must survive the lapse. The upsell replaces the empty state only.
    func testTheListIsNeverReplacedByThePlusGate() throws {
        let source = try read(Self.list)
        XCTAssertTrue(source.contains("if vm.affordances.showPlusUpsell {"), "the gate is not the empty-state branch")
        XCTAssertFalse(source.contains("if !vm.isPlusMember"), "the Plus gate replaces the whole screen again")
        XCTAssertNil(
            source.range(of: #"if [^\n{]*(isPlusMember|hasMembership)"#, options: .regularExpression),
            "a membership term guards the list of schedules"
        )
        let list = try block(in: source, after: "private struct TemplateList: View {")
        XCTAssertTrue(list.contains("onToggle: { onToggle(template) }"), "pause/resume is not wired")
        XCTAssertTrue(list.contains("onDelete: { onDelete(template) }"), "delete is not wired")
        XCTAssertTrue(list.contains("showEdit: showEdit"), "the edit gate is applied to the list, not the card")
    }

    func testTheCreateAffordanceIsTheThingBehindTheGate() throws {
        let source = try read(Self.list)
        let overlay = try block(in: source, after: "if vm.affordances.showCreateAction {")
        XCTAssertTrue(overlay.contains("L10n.Recurring.createFab"), "the create button is not behind the gate")
    }

    /// `HomeTab` is the only browse entry point on iOS, and it hid a running schedule from
    /// the customer being charged for it.
    func testHomeShowsExistingSchedulesRegardlessOfMembership() throws {
        let source = try read(Self.home)
        let section = try block(in: source, after: "var showRecurringSection: Bool {")
        XCTAssertFalse(section.contains("isPlus"), "the home section is gated on membership again")
        let refresh = try block(in: source, after: "func refreshRecurring() async {")
        XCTAssertFalse(refresh.contains("isPlus"), "a lapsed member's templates never load")
    }

    /// Body of the brace-delimited block opened by `marker`, so a token elsewhere in the
    /// file cannot satisfy an assertion about this one.
    private func block(in source: String, after marker: String) throws -> String {
        let start = try XCTUnwrap(source.range(of: marker), "no `\(marker)` in source")
        XCTAssertEqual(source.range(of: marker, options: .backwards), start, "`\(marker)` is not unique")
        var depth = 1
        var index = start.upperBound
        while index < source.endIndex {
            if source[index] == "{" { depth += 1 }
            if source[index] == "}" {
                depth -= 1
                if depth == 0 { return String(source[start.upperBound ..< index]) }
            }
            index = source.index(after: index)
        }
        throw XCTSkip("unbalanced braces after `\(marker)`")
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
