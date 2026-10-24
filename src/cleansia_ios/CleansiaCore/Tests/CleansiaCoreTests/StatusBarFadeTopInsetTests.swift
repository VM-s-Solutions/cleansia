import SwiftUI
import XCTest
@testable import CleansiaCore

/// The fade and the clock it asks for belong to a status bar across the top of the screen. With no top inset
/// there is none there (hidden, or on the side of the iPhone Duo's displays), so the fade draws nothing and leaves
/// the clock to the system; every iPhone today has one, 20pt or deeper, and keeps its fade.
@MainActor
final class StatusBarFadeTopInsetTests: XCTestCase {
    private let darkHero = Color(red: 3 / 255, green: 105 / 255, blue: 161 / 255)

    func testAPhoneWithAStatusBarAcrossTheTopKeepsTheClockRequest() async {
        for top: CGFloat in [StatusBarFade.classicStatusBar, 47, 59, 62] {
            let asks = await asksForTheClock(safeTop: top)
            XCTAssertTrue(asks, "safe top \(top)")
        }
    }

    func testWithNoStatusBarAcrossTheTopTheClockIsLeftToTheSystem() async {
        for top: CGFloat in [0, StatusBarFade.classicStatusBar - 1] {
            let asks = await asksForTheClock(safeTop: top)
            XCTAssertFalse(asks, "safe top \(top)")
        }
    }

    private func asksForTheClock(safeTop: CGFloat) async -> Bool {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(
            size: CGSize(width: 393, height: 852),
            safeArea: EdgeInsets(top: safeTop, leading: 0, bottom: 34, trailing: 0)
        ) {
            StatusBarFadeScrollView(heroTint: darkHero) {
                Color.gray.frame(height: 1600).probed("content", in: probes)
            }
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["content"] != nil }
        return canvas.controllers.contains { $0 is StatusBarStyleBridge.Controller }
    }
}
