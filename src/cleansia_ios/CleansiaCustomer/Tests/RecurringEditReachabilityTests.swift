import XCTest
@testable import CleansiaCustomer

/// `RecurringBookingRepository.update` existed and was unit-tested on Android for a full release with
/// zero production callers — no route, no button — so the edit was unreachable while every suite stayed
/// green. These pin the chain end to end: list → route → form → repository.
final class RecurringEditReachabilityTests: XCTestCase {
    private static let list = "CleansiaCustomer/Sources/Features/Recurring/RecurringBookingsScreen.swift"
    private static let shell = "CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift"
    private static let form = "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringViewModel.swift"

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

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
