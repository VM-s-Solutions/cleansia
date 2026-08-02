import SwiftUI
import XCTest
@testable import CleansiaCore

/// The reveal is claimed to be shoulder-surf resistant, so the property that has to
/// hold is not "the label says Show" — it is that nothing composes the secret while
/// it is concealed, and that leaving the foreground puts it back.
final class SecretRevealPolicyTests: XCTestCase {
    func testStartsConcealed() {
        XCTAssertEqual(SecretRevealState.initial, .concealed)
    }

    func testTapRevealsAndTapsBack() {
        XCTAssertEqual(SecretRevealPolicy.toggled(.concealed), .revealed)
        XCTAssertEqual(SecretRevealPolicy.toggled(.revealed), .concealed)
    }

    func testOnlyTheRevealedStateComposesTheSecret() {
        XCTAssertFalse(SecretRevealState.concealed.composesSecret)
        XCTAssertTrue(SecretRevealState.revealed.composesSecret)
    }

    func testLeavingTheForegroundConcealsAgain() {
        XCTAssertEqual(SecretRevealPolicy.onScenePhaseChange(to: .inactive, current: .revealed), .concealed)
        XCTAssertEqual(SecretRevealPolicy.onScenePhaseChange(to: .background, current: .revealed), .concealed)
    }

    func testReturningToTheForegroundDoesNotRevealByItself() {
        XCTAssertEqual(SecretRevealPolicy.onScenePhaseChange(to: .active, current: .concealed), .concealed)
        XCTAssertEqual(SecretRevealPolicy.onScenePhaseChange(to: .active, current: .revealed), .revealed)
    }

    func testTimeoutConcealsOnlyOnceTheWindowHasElapsed() {
        let revealedAt = Date(timeIntervalSince1970: 1000)
        XCTAssertEqual(
            SecretRevealPolicy.onTick(now: revealedAt.addingTimeInterval(59), revealedAt: revealedAt),
            .revealed
        )
        XCTAssertEqual(
            SecretRevealPolicy.onTick(now: revealedAt.addingTimeInterval(60), revealedAt: revealedAt),
            .concealed
        )
    }

    func testTimeoutIsOneMinute() {
        XCTAssertEqual(SecretRevealPolicy.autoConcealAfter, 60)
    }
}
