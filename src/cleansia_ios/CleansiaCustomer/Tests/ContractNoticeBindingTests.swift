import XCTest
@testable import CleansiaCustomer

/// The wizard names the customer's contract at the offer and asks for the early-performance request for
/// EVERY booker. The terms tick is asked only of an account that has not already granted it, so the one
/// way to lose either silently is to move it inside that gate — which no view-model test can see. Pin the
/// call sites.
final class ContractNoticeBindingTests: XCTestCase {
    private static let confirmStep = "CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmStep.swift"
    private static let recurringForm = "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift"

    func testTheConfirmStepRendersTheNoticeAndTheRequestOutsideTheConsentGate() throws {
        let source = try read(Self.confirmStep)
        XCTAssertTrue(source.contains("ContractNotice()"), "the confirm step no longer names the contract")
        XCTAssertTrue(source.contains("earlyPerformanceRow\n"), "the confirm step no longer asks for the request")

        let gateStart = try XCTUnwrap(source.range(of: "private var termsRow"), "the consent gate moved")
        let gateEnd = try XCTUnwrap(source.range(of: "private func setTermsAccepted"), "the consent gate moved")
        let gate = source[gateStart.lowerBound ..< gateEnd.lowerBound]
        XCTAssertTrue(gate.contains("alreadyConsented"), "the block read is not the consent gate")
        XCTAssertFalse(gate.contains("ContractNotice"), "the sentence is shown only to a first-time booker")
        XCTAssertFalse(gate.contains("earlyPerformance"), "the request is asked only of a first-time booker")
    }

    func testTheNoticeReadsTheCatalogSentenceThroughTheSharedMarkup() throws {
        let source = try read(Self.confirmStep)
        XCTAssertTrue(
            source.contains("ConsentMarkdown.styled(L10n.Booking.contractNotice)"),
            "the notice no longer reads the sentence through the shared markup, which draws links in the text ink"
        )
        XCTAssertTrue(source.contains(".tint(CleansiaColors.primaryText)"))
        XCTAssertFalse(source.contains("workContract"), "the confirm step still names the contract for work")
    }

    /// One tick covers the schedule; an edit sends no request, so it asks for none.
    func testTheRecurringFormAsksForTheRequestOnANewScheduleOnly() throws {
        let source = try read(Self.recurringForm)
        XCTAssertTrue(source.contains("if !vm.isEditing {\n                    earlyPerformanceRow"))
        XCTAssertTrue(source.contains("set: vm.setEarlyPerformanceRequested"), "the tick is wired to nothing")
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
            .replacingOccurrences(of: "\r\n", with: "\n")
    }
}
