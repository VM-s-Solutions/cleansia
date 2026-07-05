import SwiftUI
import XCTest
@testable import CleansiaCustomer

final class CategoryPaletteTests: XCTestCase {
    func testSymbolMeaningPerCategory() {
        XCTAssertEqual(CategoryPalette.symbol(for: "home"), "bubbles.and.sparkles")
        XCTAssertEqual(CategoryPalette.symbol(for: "deep"), "leaf")
        XCTAssertEqual(CategoryPalette.symbol(for: "laundry"), "washer")
        XCTAssertEqual(CategoryPalette.symbol(for: "pet"), "pawprint")
        XCTAssertEqual(CategoryPalette.symbol(for: "unknown"), "star")
    }

    func testTintPerCategoryMatchesAndroid() {
        XCTAssertEqual(hex(CategoryPalette.tint(for: "home")), 0x0284C7)
        XCTAssertEqual(hex(CategoryPalette.tint(for: "deep")), 0x7C3AED)
        XCTAssertEqual(hex(CategoryPalette.tint(for: "laundry")), 0x0891B2)
        XCTAssertEqual(hex(CategoryPalette.tint(for: "pet")), 0xEA580C)
        XCTAssertEqual(hex(CategoryPalette.tint(for: "unknown")), 0x0284C7)
    }

    func testEverySymbolResolvesOnThisRuntime() {
        for slug in ["home", "deep", "laundry", "pet", "unknown"] {
            let symbol = CategoryPalette.symbol(for: slug)
            XCTAssertNotNil(
                UIImage(systemName: symbol),
                "SF Symbol \(symbol) (slug \(slug)) is unavailable on iOS \(UIDevice.current.systemVersion)"
            )
        }
    }

    private func hex(_ color: Color) -> UInt32 {
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        UIColor(color).getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        func channel(_ value: CGFloat) -> UInt32 {
            UInt32((value * 255).rounded())
        }
        return channel(red) << 16 | channel(green) << 8 | channel(blue)
    }
}
