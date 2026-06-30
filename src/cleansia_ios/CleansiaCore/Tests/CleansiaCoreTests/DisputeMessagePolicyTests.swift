import XCTest
@testable import CleansiaCore

final class DisputeMessagePolicyTests: XCTestCase {
    func testLiveStatusesAllowMessages() {
        XCTAssertTrue(DisputeMessagePolicy.allowsMessages(statusValue: 1))
        XCTAssertTrue(DisputeMessagePolicy.allowsMessages(statusValue: 2))
        XCTAssertTrue(DisputeMessagePolicy.allowsMessages(statusValue: 3))
    }

    func testTerminalStatusesBlockMessages() {
        XCTAssertFalse(DisputeMessagePolicy.allowsMessages(statusValue: 4))
        XCTAssertFalse(DisputeMessagePolicy.allowsMessages(statusValue: 5))
        XCTAssertFalse(DisputeMessagePolicy.allowsMessages(statusValue: 6))
    }

    func testNilAndUnknownDefaultToAllowing() {
        XCTAssertTrue(DisputeMessagePolicy.allowsMessages(statusValue: nil))
        XCTAssertTrue(DisputeMessagePolicy.allowsMessages(statusValue: 0))
        XCTAssertTrue(DisputeMessagePolicy.allowsMessages(statusValue: 99))
    }
}
