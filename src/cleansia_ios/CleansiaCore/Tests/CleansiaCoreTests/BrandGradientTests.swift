import SwiftUI
import XCTest
@testable import CleansiaCore

/// Asserts the hex STOPS, not a resolved-color roundtrip: SwiftUI `Color` →
/// `UIColor` flattens dynamic providers on iOS 16 (preservation arrived in
/// iOS 17), so any trait-resolution roundtrip is red on the ADR-0014 floor
/// runtime while green on the latest — the exact failure class this phase
/// hunts. The stops ARE the source `colors` derives from, so value equality
/// here pins the shipped gradient.
final class BrandGradientTests: XCTestCase {
    func testBlueMatchesTheAndroidBrandPair() {
        assertStops(.blue, light: [0x0284C7, 0x38BDF8], dark: [0x075985, 0x0369A1])
    }

    func testPurpleMatchesTheAndroidBrandPair() {
        assertStops(.purple, light: [0x7C3AED, 0xA78BFA], dark: [0x5B2AB0, 0x7C5ABF])
    }

    func testCyanMatchesTheAndroidBrandPair() {
        assertStops(.cyan, light: [0x0891B2, 0x67E8F9], dark: [0x0E6E88, 0x4BAEC1])
    }

    func testPlusHeroIsTheFixedSky950ToSlate900Pair() {
        assertStops(.plusHero, light: [0x082F49, 0x0F172A], dark: [0x082F49, 0x0F172A])
    }

    func testEveryGradientDerivesItsColorsFromItsStops() {
        for gradient in BrandGradient.allCases {
            XCTAssertEqual(gradient.colors.count, gradient.stops.count, "\(gradient)")
            XCTAssertEqual(gradient.stops.count, 2, "\(gradient)")
        }
    }

    private func assertStops(
        _ gradient: BrandGradient,
        light: [UInt32],
        dark: [UInt32],
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertEqual(gradient.stops.map(\.light), light, file: file, line: line)
        XCTAssertEqual(gradient.stops.map(\.dark), dark, file: file, line: line)
    }
}
