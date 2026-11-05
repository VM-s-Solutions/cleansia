import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The day and time under an order's number are never shrunk to fit: a window that does not fit beside its day
/// moves whole onto the next line, and a line too long for its room wraps.
@MainActor
final class OrderCalendarLineTests: XCTestCase {
    private let day = "Thu, Nov 12"
    private let window = "14:00–16:00"

    func testADayAndWindowThatFitStayOnOneLine() {
        let line = size(OrderCalendarLine(day, window: window))

        XCTAssertEqual(line.height, size(OrderCalendarLine(day)).height, accuracy: 0.5)
        XCTAssertEqual(size(OrderCalendarLine(day, window: window), width: line.width + 1).height, line.height)
    }

    func testAWindowThatDoesNotFitMovesWholeOntoTheNextLine() {
        let oneLine = size(OrderCalendarLine(day)).height
        let brokenInsideTheWindow = size(OrderCalendarLine("\(day) · 14:00–")).width

        let wrapped = size(OrderCalendarLine(day, window: window), width: brokenInsideTheWindow + 1)

        XCTAssertEqual(wrapped.height, 2 * oneLine, accuracy: 1, "the line shrank instead of wrapping")
        XCTAssertLessThan(wrapped.width, brokenInsideTheWindow, "the window broke inside itself")
    }

    func testALineTooLongForItsRoomWrapsInsteadOfShrinking() {
        let text = "Wed, Sep 29 at 10:30"
        let oneLine = size(OrderCalendarLine(text))
        let room = (oneLine.width * 0.7).rounded()

        let fitted = size(OrderCalendarLine(text), width: room)

        XCTAssertGreaterThan(fitted.height, 1.5 * oneLine.height, "the line was shrunk or cut to one line")
        XCTAssertLessThanOrEqual(fitted.width, room)
    }

    private func size(_ line: OrderCalendarLine, width: CGFloat = .greatestFiniteMagnitude) -> CGSize {
        UIHostingController(rootView: line).sizeThatFits(in: CGSize(width: width, height: .greatestFiniteMagnitude))
    }
}
