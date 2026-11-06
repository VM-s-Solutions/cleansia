import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

@MainActor
final class OrderTrackerRuleTests: XCTestCase {
    func testAFourStepTrackerHasFourSegments() {
        XCTAssertEqual(
            OrderTrackerRule.state(step: 1, cancelled: false, completed: false, stepCount: 4),
            .steps(segments: [.past, .current, .future, .future], stepNumber: 2)
        )
        XCTAssertEqual(
            OrderTrackerRule.state(step: 9, cancelled: false, completed: false, stepCount: 4),
            .steps(segments: [.past, .past, .past, .current], stepNumber: 4),
            "a step past the end is the last one"
        )
        XCTAssertEqual(
            OrderTrackerRule.state(step: 3, cancelled: false, completed: true, stepCount: 4),
            .steps(segments: [.past, .past, .past, .past], stepNumber: 4)
        )
    }

    func testWithoutAStepCountTheTrackerKeepsFiveSteps() {
        XCTAssertEqual(
            OrderTrackerRule.state(step: 2, cancelled: false, completed: false),
            .steps(segments: [.past, .past, .current, .future, .future], stepNumber: 3)
        )
    }

    /// The counter counts the segments it draws, so a four-step bar reads "/ 4".
    func testTheCounterCountsTheSegmentsTheBarDraws() async {
        var counted: [Int] = []
        let bar = OrderTrackerBar(
            state: OrderTrackerRule.state(step: 1, cancelled: false, completed: false, stepCount: 4),
            cancelledLabel: "",
            stepCounterLabel: { step, total in
                counted.append(total)
                return "\(step) / \(total)"
            }
        )
        let canvas = HostedCanvas(size: CGSize(width: 300, height: 60)) { bar.frame(width: 300) }
        defer { canvas.close() }
        await canvas.settle { !counted.isEmpty }

        XCTAssertFalse(counted.isEmpty)
        XCTAssertEqual(Set(counted), [4])
    }
}
