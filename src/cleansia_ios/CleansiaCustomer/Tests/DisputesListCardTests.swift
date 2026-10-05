import CleansiaCore
import SwiftUI
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

    /// Every status pill's ink reads 4.5:1 or more on its own 14 % wash over the card (list and detail both
    /// sit on the surface), in light and dark mode (finding 2026-10-05: Pending's amber read 1.93:1, Resolved
    /// 4.15:1 in light and 2.57:1 in dark, Closed 4.47:1 in dark, an unknown status 1.2:1).
    func testEveryStatusPillReadsOnItsWashInBothModes() {
        for style in [UIUserInterfaceStyle.light, .dark] {
            let card = rgb(CleansiaColors.surface, style)
            for value in [1, 2, 3, 4, 5, 6, nil, 99] as [Int?] {
                let ink = rgb(DisputeStatusPresentation.color(value), style)
                let wash = ink * 0.14 + card * 0.86
                XCTAssertGreaterThanOrEqual(contrast(ink, wash), 4.5, "status \(String(describing: value)), \(style)")
            }
        }
        XCTAssertEqual(
            contrast(
                rgb(DisputeStatusPresentation.pendingInk, .light),
                rgb(CleansiaColors.surface, .light) * 0.86
                    + rgb(DisputeStatusPresentation.pendingInk, .light) * 0.14
            ),
            5.70,
            accuracy: 0.02
        )
        XCTAssertEqual(DisputeStatusPresentation.color(1), DisputeStatusPresentation.pendingInk)
        XCTAssertEqual(DisputeStatusPresentation.color(4), DisputeStatusPresentation.resolvedInk)
        XCTAssertEqual(DisputeStatusPresentation.color(5), DisputeStatusPresentation.neutralInk)
        XCTAssertEqual(DisputeStatusPresentation.color(nil), DisputeStatusPresentation.neutralInk)
    }

    private func rgb(_ color: Color, _ style: UIUserInterfaceStyle) -> SIMD3<Double> {
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        UIColor(color).resolvedColor(with: UITraitCollection(userInterfaceStyle: style))
            .getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        return SIMD3(Double(red), Double(green), Double(blue))
    }

    private func contrast(_ first: SIMD3<Double>, _ second: SIMD3<Double>) -> Double {
        func luminance(_ color: SIMD3<Double>) -> Double {
            let linear = [color.x, color.y, color.z].map { $0 <= 0.04045 ? $0 / 12.92 : pow(($0 + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
        }
        let (lighter, darker) = (max(luminance(first), luminance(second)), min(luminance(first), luminance(second)))
        return (lighter + 0.05) / (darker + 0.05)
    }
}
