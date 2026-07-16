import XCTest
@testable import CleansiaCore

@MainActor
final class PushTapBufferTests: XCTestCase {
    func testTapBeforeHandlerBuffersThenFlushesExactlyOnceOnAssignment() {
        // Cold-launch case: didReceive delivers before the SwiftUI .task wires onTap.
        let buffer = PushTapBuffer<Int>()
        var fired: [Int] = []

        buffer.deliver(42)
        XCTAssertTrue(fired.isEmpty, "no handler yet — must buffer, not fire")

        buffer.onTap = { fired.append($0) }
        XCTAssertEqual(fired, [42], "assigning the handler flushes the buffered tap exactly once")
    }

    func testTapAfterHandlerFiresImmediately() {
        // Warm case: handler already wired.
        let buffer = PushTapBuffer<Int>()
        var fired: [Int] = []
        buffer.onTap = { fired.append($0) }

        buffer.deliver(7)
        XCTAssertEqual(fired, [7])
    }

    func testReassigningHandlerDoesNotReplayAConsumedTap() {
        let buffer = PushTapBuffer<Int>()
        var fired: [Int] = []

        buffer.deliver(1)
        buffer.onTap = { fired.append($0) } // flushes 1
        buffer.onTap = { fired.append($0 * 100) } // pending already cleared → no replay

        XCTAssertEqual(fired, [1], "a consumed tap is not re-delivered when the handler is reassigned")
    }

    func testLatestBufferedTapWins() {
        let buffer = PushTapBuffer<Int>()
        var fired: [Int] = []

        buffer.deliver(1)
        buffer.deliver(2)
        buffer.onTap = { fired.append($0) }

        XCTAssertEqual(fired, [2], "only the most recent pre-handler tap is flushed")
    }
}
