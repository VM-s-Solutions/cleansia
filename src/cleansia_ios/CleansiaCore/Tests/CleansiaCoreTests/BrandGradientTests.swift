import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

final class BrandGradientTests: XCTestCase {
    func testBlueMatchesTheAndroidBrandPair() {
        assertPair(.blue, light: [0x0284C7, 0x38BDF8], dark: [0x075985, 0x0369A1])
    }

    func testPurpleMatchesTheAndroidBrandPair() {
        assertPair(.purple, light: [0x7C3AED, 0xA78BFA], dark: [0x5B2AB0, 0x7C5ABF])
    }

    func testCyanMatchesTheAndroidBrandPair() {
        assertPair(.cyan, light: [0x0891B2, 0x67E8F9], dark: [0x0E6E88, 0x4BAEC1])
    }

    func testPlusHeroIsTheFixedSky950ToSlate900Pair() {
        assertPair(.plusHero, light: [0x082F49, 0x0F172A], dark: [0x082F49, 0x0F172A])
    }

    private func assertPair(
        _ gradient: BrandGradient,
        light: [UInt32],
        dark: [UInt32],
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        XCTAssertEqual(gradient.colors.count, 2, file: file, line: line)
        for (index, color) in gradient.colors.enumerated() {
            assertResolved(color, style: .light, expected: light[index], file: file, line: line)
            assertResolved(color, style: .dark, expected: dark[index], file: file, line: line)
        }
    }

    private func assertResolved(
        _ color: Color,
        style: UIUserInterfaceStyle,
        expected: UInt32,
        file: StaticString,
        line: UInt
    ) {
        let resolved = UIColor(color).resolvedColor(with: UITraitCollection(userInterfaceStyle: style))
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        resolved.getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        let accuracy: CGFloat = 1 / 255
        XCTAssertEqual(red, CGFloat((expected >> 16) & 0xFF) / 255, accuracy: accuracy, file: file, line: line)
        XCTAssertEqual(green, CGFloat((expected >> 8) & 0xFF) / 255, accuracy: accuracy, file: file, line: line)
        XCTAssertEqual(blue, CGFloat(expected & 0xFF) / 255, accuracy: accuracy, file: file, line: line)
        XCTAssertEqual(alpha, 1, file: file, line: line)
    }
}
