import XCTest
@testable import CleansiaCustomer

/// The cash rule's resolvers are covered by the view-model suites, and none of that proves a screen draws
/// what they resolve: an option left enabled, a reason never shown or a schedule card that stops saying it
/// books nothing all keep every view-model test green.
final class CashChoiceBindingTests: XCTestCase {
    private static let list = "CleansiaCustomer/Sources/Features/Recurring/RecurringBookingsScreen.swift"
    private static let form = "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift"
    private static let confirm = "CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmStep.swift"
    private static let sheet = "CleansiaCustomer/Sources/Features/Booking/BookingSheetView.swift"

    /// A schedule that needs a payment change books nothing, and the card is the only place that says so.
    func testTheScheduleCardSaysItNeedsAPaymentChange() throws {
        let card = try block(in: read(Self.list), after: "private struct TemplateCard: View {")
        XCTAssertTrue(card.contains("switch RecurringStatusBadge.of(template)"), "the card ignores the badge rule")
        XCTAssertTrue(card.contains("case .needsPaymentChange:"), "the card draws no needs-a-change badge")
        XCTAssertTrue(card.contains("L10n.Recurring.statusNeedsChange"), "the needs-a-change badge has no label")
        XCTAssertTrue(
            card.contains("if template.requiresPaymentMethodChange {"),
            "the card never asks whether the schedule needs a payment change"
        )
        XCTAssertTrue(
            card.contains("PaymentChangeNotice(showChangeAction: showEdit, onChange: onEdit)"),
            "the notice is dropped, or offers a change the lapsed member cannot make"
        )

        let notice = try block(in: read(Self.list), after: "private struct PaymentChangeNotice: View {")
        XCTAssertTrue(notice.contains("L10n.Recurring.cashChangeTitle"), "the notice has no title")
        XCTAssertTrue(notice.contains("L10n.Recurring.cashChangeBody"), "the notice does not say why")
        XCTAssertTrue(
            notice.contains("CleansiaTextLink(L10n.Recurring.cashChangeAction, action: onChange)"),
            "the notice offers no way to change the schedule"
        )
    }

    func testTheBookingPaymentStepDisablesCashAndSaysWhy() throws {
        let source = try read(Self.confirm)
        let section = try block(in: source, after: "private var paymentSection: some View {")
        XCTAssertTrue(section.contains("let cash = viewModel.cashEligibility"), "the step never reads the rule")
        XCTAssertTrue(section.contains("enabled: cash == .available"), "cash is offered whatever the crew")
        XCTAssertTrue(
            section.contains("action: { viewModel.selectPayment(.cash) }"),
            "cash is chosen past the view model's refusal"
        )
        XCTAssertTrue(section.contains("L10n.Booking.cashReason(cash)"), "cash is disabled without a reason")
        XCTAssertTrue(section.contains("if viewModel.cashCleared"), "a cash choice is taken away without a word")
        XCTAssertFalse(source.contains("next.paymentMethod = method"), "the step writes the payment itself again")
    }

    /// A refused slide has sent nothing, so the slider must come back for the customer's next choice.
    func testARefusedCashSubmitHandsTheSliderBack() throws {
        let submit = try block(in: read(Self.sheet), after: "private func submit() async {")
        let refused = try XCTUnwrap(submit.range(of: "case .paymentMethodCleared:"), "the refusal is not handled")
        let next = submit.range(of: "case ", range: refused.upperBound ..< submit.endIndex)?.lowerBound
            ?? submit.endIndex
        XCTAssertTrue(
            submit[refused.upperBound ..< next].contains("slideResetCount += 1"),
            "a refused cash submit leaves the slider spent"
        )
    }

    func testTheScheduleFormDisablesCashAndSaysWhy() throws {
        let source = try read(Self.form)
        XCTAssertTrue(source.contains("cash: vm.cashEligibility"), "the form never reads the rule")
        XCTAssertTrue(source.contains("onSelect: vm.setPaymentType"), "cash is chosen past the view model's refusal")
        let section = try block(in: source, after: "private struct PaymentSection: View {")
        XCTAssertTrue(section.contains("enabled: cash == .available"), "cash is offered whatever the crew")
        XCTAssertTrue(section.contains("L10n.Recurring.cashReason(cash)"), "cash is disabled without a reason")
        XCTAssertTrue(section.contains("if cashCleared"), "a cash choice is taken away without a word")
    }

    // MARK: - Reading

    /// Body of the brace-delimited block opened by `marker`, so a token elsewhere in the file cannot
    /// satisfy an assertion about this one.
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
