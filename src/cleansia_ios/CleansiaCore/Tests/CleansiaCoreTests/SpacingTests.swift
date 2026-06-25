import SwiftUI
import XCTest
@testable import CleansiaCore

final class SpacingTests: XCTestCase {
    func testEightPointGridScale() {
        XCTAssertEqual(Spacing.hair, 2)
        XCTAssertEqual(Spacing.xxs, 4)
        XCTAssertEqual(Spacing.xs, 8)
        XCTAssertEqual(Spacing.s, 12)
        XCTAssertEqual(Spacing.m, 16)
        XCTAssertEqual(Spacing.ml, 20)
        XCTAssertEqual(Spacing.l, 24)
        XCTAssertEqual(Spacing.xl, 32)
        XCTAssertEqual(Spacing.xxl, 40)
    }

    func testCornerRadiusTokens() {
        XCTAssertEqual(CornerRadius.extraSmall, 6)
        XCTAssertEqual(CornerRadius.small, 12)
        XCTAssertEqual(CornerRadius.medium, 16)
        XCTAssertEqual(CornerRadius.large, 24)
        XCTAssertEqual(CornerRadius.extraLarge, 32)
        XCTAssertEqual(CornerRadius.pill, 999)
    }
}
