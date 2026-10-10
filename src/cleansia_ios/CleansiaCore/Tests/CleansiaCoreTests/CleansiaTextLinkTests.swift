import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// Every Android text link is a Material TextButton, which gives it a 48dp touch target; the iOS link was its
/// label alone, about 36 by 27pt, under the 44pt the house rule asks of every control. A call site cannot grow
/// a plain button's target from outside, so the link carries it.
@MainActor
final class CleansiaTextLinkTests: XCTestCase {
    func testTheLinkIsAFullTouchTargetInBothAxes() {
        let size = UIHostingController(rootView: CleansiaTextLink("Skip") {})
            .sizeThatFits(in: CGSize(width: CGFloat.greatestFiniteMagnitude, height: .greatestFiniteMagnitude))

        XCTAssertGreaterThanOrEqual(size.width, 44, "the link is \(size.width)pt wide")
        XCTAssertGreaterThanOrEqual(size.height, 44, "the link is \(size.height)pt tall")
    }

    /// A size alone does not take the tap: a plain button answers only where its label draws, so the frame
    /// carries a shape inside the label.
    func testTheWholeTargetTakesTheTap() throws {
        let button = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/CleansiaButton.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()

        XCTAssertTrue(button.contains(
            ".lineLimit(1).padding(Spacing.xxs).frame(minWidth:44,minHeight:44).contentShape(Rectangle())}"
                + ".buttonStyle(.plain)"
        ))
    }
}
