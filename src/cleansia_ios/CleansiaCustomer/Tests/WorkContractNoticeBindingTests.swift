import XCTest
@testable import CleansiaCustomer

/// The wizard names the contract for work at the offer for EVERY booker. The consent tick is asked
/// only of an account that has not already granted it, so the one way to lose the sentence silently
/// is to move it inside that gate — which no view-model test can see. Pin the call site.
final class WorkContractNoticeBindingTests: XCTestCase {
    private static let confirmStep = "CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmStep.swift"

    func testTheConfirmStepRendersTheNoticeOutsideTheConsentGate() throws {
        let source = try read(Self.confirmStep)
        XCTAssertTrue(source.contains("WorkContractNotice()"), "the confirm step no longer names the contract")

        let gateStart = try XCTUnwrap(source.range(of: "private var termsRow"), "the consent gate moved")
        let gateEnd = try XCTUnwrap(source.range(of: "private func setTermsAccepted"), "the consent gate moved")
        let gate = source[gateStart.lowerBound ..< gateEnd.lowerBound]
        XCTAssertTrue(gate.contains("alreadyConsented"), "the block read is not the consent gate")
        XCTAssertFalse(gate.contains("WorkContractNotice"), "the sentence is shown only to a first-time booker")
    }

    func testTheNoticeReadsTheCatalogSentenceThroughTheSharedMarkup() throws {
        let source = try read(Self.confirmStep)
        XCTAssertTrue(source.contains("ConsentMarkdown.styled(L10n.Booking.workContractNotice)"))
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
