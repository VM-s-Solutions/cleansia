import SwiftUI
import XCTest
@testable import CleansiaCore

/// The filled button's fill and label ink. A caller that names no disabled colours gets the button's own look, the
/// disabled container under its ink while it cannot be pressed; one that names them gets them only in that state, as
/// the Android button's disabled colours do.
@MainActor
final class PrimaryButtonColorsTests: XCTestCase {
    func testAnUntouchedButtonKeepsItsLook() {
        XCTAssertEqual(CleansiaPrimaryButton.fill(pressable: true, disabled: nil), CleansiaColors.primary)
        XCTAssertEqual(CleansiaPrimaryButton.ink(pressable: true, disabled: nil), CleansiaColors.onPrimary)
        XCTAssertEqual(CleansiaPrimaryButton.fill(pressable: false, disabled: nil), CleansiaColors.disabledContainer)
        XCTAssertEqual(CleansiaPrimaryButton.ink(pressable: false, disabled: nil), CleansiaColors.onDisabledContainer)
    }

    func testTheCallersDisabledColoursDrawOnlyWhileItCannotBePressed() {
        let fill = CleansiaColors.surfaceVariant
        let ink = CleansiaColors.onSurfaceVariant

        XCTAssertEqual(CleansiaPrimaryButton.fill(pressable: false, disabled: fill), fill)
        XCTAssertEqual(CleansiaPrimaryButton.ink(pressable: false, disabled: ink), ink)
        XCTAssertEqual(CleansiaPrimaryButton.fill(pressable: true, disabled: fill), CleansiaColors.primary)
        XCTAssertEqual(CleansiaPrimaryButton.ink(pressable: true, disabled: ink), CleansiaColors.onPrimary)
    }
}
