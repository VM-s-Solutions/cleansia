import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// A section header can carry a count after its title ("Your next jobs · 2 jobs") on the title's own line; without
/// one it is the title alone, as before.
@MainActor
final class OrderSectionRowsTests: XCTestCase {
    func testACountRidesTheTitleLine() {
        let plain = size(OrderSectionHeaderRow(title: "Your next jobs", systemImage: "calendar"))
        let counted = size(OrderSectionHeaderRow(title: "Your next jobs", systemImage: "calendar", subtitle: "2 jobs"))

        XCTAssertGreaterThan(counted.width, plain.width + 20, "the count is not drawn")
        XCTAssertEqual(counted.height, plain.height, accuracy: 0.5, "the count left the title's line")
    }

    func testWithNoCountTheHeaderIsTheTitleAlone() {
        XCTAssertEqual(
            size(OrderSectionHeaderRow(title: "Payment", systemImage: "creditcard", subtitle: nil)),
            size(OrderSectionHeaderRow(title: "Payment", systemImage: "creditcard"))
        )
    }

    func testTheCountsDotNeverStartsALine() {
        XCTAssertEqual(OrderSectionHeaderRow.subtitleRun("2 jobs"), "\u{00A0}· 2 jobs")
    }

    private func size(_ header: OrderSectionHeaderRow) -> CGSize {
        UIHostingController(rootView: header.fixedSize())
            .sizeThatFits(in: CGSize(width: CGFloat.greatestFiniteMagnitude, height: .greatestFiniteMagnitude))
    }
}
