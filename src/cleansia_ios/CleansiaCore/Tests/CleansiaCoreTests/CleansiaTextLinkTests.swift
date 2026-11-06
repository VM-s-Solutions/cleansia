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
            "label.padding(Spacing.xxs).frame(minWidth:44,minHeight:44).contentShape(Rectangle())}"
                + ".buttonStyle(.plain)"
        ))
    }

    func testAnIconWidensTheLinkByItselfAndItsGap() {
        let plain = fittingSize(CleansiaTextLink("Job details") {}).width
        let trailing = fittingSize(CleansiaTextLink("Job details", trailingIcon: "chevron.right") {}).width
        let both = fittingSize(CleansiaTextLink(
            "Job details",
            leadingIcon: "doc.on.doc",
            trailingIcon: "chevron.right"
        ) {})
            .width

        XCTAssertGreaterThan(trailing, plain + Spacing.xs + 4)
        XCTAssertGreaterThan(both, trailing + Spacing.xs + 4)
        XCTAssertGreaterThanOrEqual(fittingSize(CleansiaTextLink("Retry", trailingIcon: "chevron.right") {}).height, 44)
    }

    /// One line by default, so every current link keeps truncating; a caller that lets it wrap gets the lines.
    func testALinkWrapsOnlyUpToTheLinesItIsGiven() {
        let label = "Payouts are in Pay and Earnings, every week"
        let oneLine = fittingSize(CleansiaTextLink(label, trailingIcon: "chevron.right") {}, width: 100).height
        let threeLines = fittingSize(CleansiaTextLink(label, trailingIcon: "chevron.right", maxLines: 3) {}, width: 100)
            .height

        XCTAssertEqual(oneLine, 44, accuracy: 0.5)
        XCTAssertGreaterThan(threeLines, 50)
    }

    /// A one-line link centres its icons on the line, as the app's own disclosure links do; a link that may wrap puts
    /// a leading icon on its first line's cap band and a trailing one on its last line's.
    func testTheIconsSitOnTheirLinesCapBand() throws {
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

        XCTAssertTrue(button.contains("ifmaxLines==1{HStack(alignment:.center,spacing:Spacing.xs){"))
        XCTAssertTrue(button.contains(
            "HStack(alignment:.firstTextBaseline,spacing:Spacing.xs){ifletleadingIcon{icon(leadingIcon)"
                + ".capBandAligned(of:style)}HStack(alignment:.lastTextBaseline,spacing:Spacing.xs){words"
        ))
        XCTAssertTrue(button.contains("icon(trailingIcon).capBandAligned(.lastTextBaseline,of:style)"))
    }

    private func fittingSize(_ link: CleansiaTextLink, width: CGFloat = .greatestFiniteMagnitude) -> CGSize {
        UIHostingController(rootView: link.dynamicTypeSize(.large))
            .sizeThatFits(in: CGSize(width: width, height: .greatestFiniteMagnitude))
    }
}
