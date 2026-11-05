import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

@MainActor
final class CleansiaCheckboxTests: XCTestCase {
    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

    private func element(_ checkbox: CleansiaCheckbox<some View>) async -> NSObject? {
        let canvas = HostedCanvas(size: CGSize(width: 320, height: 200)) {
            checkbox.frame(width: 300)
        }
        defer { canvas.close() }
        await canvas.settle()
        return Self.elements(under: canvas.window).first
    }

    /// VoiceOver reads one button named by its label, ticked or not; the box itself is not read as an image.
    func testTheCheckboxIsOneButtonNamedByItsLabelThatSaysWhetherItIsTicked() async throws {
        let tickedElement = await element(CleansiaCheckbox(checked: .constant(true), label: "Kitchen"))
        let openElement = await element(CleansiaCheckbox(checked: .constant(false), label: "Kitchen"))
        let ticked = try XCTUnwrap(tickedElement)
        let open = try XCTUnwrap(openElement)

        XCTAssertEqual(ticked.accessibilityLabel, "Kitchen")
        XCTAssertTrue(ticked.accessibilityTraits.contains(.button))
        XCTAssertTrue(ticked.accessibilityTraits.contains(.selected))
        XCTAssertEqual(open.accessibilityLabel, "Kitchen")
        XCTAssertFalse(open.accessibilityTraits.contains(.selected))
    }

    func testACheckboxThatCannotBeChangedIsNotEnabled() async throws {
        let lockedElement = await element(CleansiaCheckbox(checked: .constant(true), label: "Kitchen", enabled: false))
        let openElement = await element(CleansiaCheckbox(checked: .constant(true), label: "Kitchen"))
        let locked = try XCTUnwrap(lockedElement)
        let open = try XCTUnwrap(openElement)

        XCTAssertTrue(locked.accessibilityTraits.contains(.notEnabled))
        XCTAssertFalse(open.accessibilityTraits.contains(.notEnabled))
    }

    /// A caller can hand over a view for the label, such as scope lines tied together, and it is read whole.
    func testAViewLabelIsReadWhole() async throws {
        let checkbox = CleansiaCheckbox(checked: .constant(false)) {
            HStack {
                Text(verbatim: "Complete Home Clean")
                Text(verbatim: "2 rooms")
            }
        }
        let found = await element(checkbox)
        let element = try XCTUnwrap(found)

        let label = element.accessibilityLabel ?? ""
        XCTAssertTrue(label.contains("Complete Home Clean") && label.contains("2 rooms"), label)
    }

    /// The box is centred on its label's first line, so a label that wraps hangs under the box rather than
    /// pulling it down to its middle.
    func testTheBoxSitsOnTheCapBandOfTheLabelsFirstLine() throws {
        let source = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/CleansiaCheckbox.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()

        XCTAssertTrue(source.contains("HStack(alignment:.firstTextBaseline,spacing:Spacing.xs){box.capBandAligned(of:"))
    }

    private static func elements(under view: NSObject, depth: Int = 0) -> [NSObject] {
        guard depth < 60 else { return [] }
        if view.isAccessibilityElement { return [view] }
        if let children = view.accessibilityElements as? [NSObject], !children.isEmpty {
            return children.flatMap { elements(under: $0, depth: depth + 1) }
        }
        let count = view.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            return (0 ..< count).compactMap { view.accessibilityElement(at: $0) as? NSObject }
                .flatMap { elements(under: $0, depth: depth + 1) }
        }
        return ((view as? UIView)?.subviews ?? []).flatMap { elements(under: $0, depth: depth + 1) }
    }
}
