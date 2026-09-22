import XCTest
@testable import CleansiaCustomer

/// The dispute card carries its status in the pill only; the left accent strip is gone by owner
/// ruling. A SwiftUI body has no unit harness, so the card is pinned as source text scoped to its
/// one struct.
final class DisputesListCardTests: XCTestCase {
    private func disputeRowCard() throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let path = "CleansiaCustomer/Sources/Features/Disputes/DisputesListView.swift"
        let source = try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
        return try typeBody(source, "struct DisputeRowCard: View {")
    }

    /// The braces of `signature` … `}`, so a sibling view cannot leak in.
    private func typeBody(_ source: String, _ signature: String) throws -> String {
        let start = try XCTUnwrap(source.range(of: signature), "`\(signature)` no longer appears — this pin is stale")
        var depth = 0
        var index = source.index(before: start.upperBound)
        while index < source.endIndex {
            switch source[index] {
            case "{":
                depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return String(source[start.lowerBound ... index]) }
            default:
                break
            }
            index = source.index(after: index)
        }
        throw NSError(domain: "DisputesListCardTests", code: 1, userInfo: [
            NSLocalizedDescriptionKey: "unbalanced braces after `\(signature)`"
        ])
    }

    func testTheCardPaintsNoLeftAccentStrip() throws {
        let card = try disputeRowCard()
        XCTAssertFalse(card.contains("Rectangle()"), "the card still draws a strip")
        XCTAssertFalse(card.contains(".frame(width: 4)"), "the card still reserves a 4pt column")
    }

    func testTheStatusColourSurvivesOnThePill() throws {
        let card = try disputeRowCard()
        XCTAssertTrue(card.contains("DisputeStatusPill("))
        XCTAssertTrue(card.contains("color: DisputeStatusPresentation.color(dispute.statusValue)"))
    }
}
