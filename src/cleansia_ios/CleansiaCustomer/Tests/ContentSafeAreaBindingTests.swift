import XCTest
@testable import CleansiaCustomer

final class ContentSafeAreaBindingTests: XCTestCase {
    func testProfileScrollKeepsTheSafeViewport() throws {
        try assertOnlyBackgroundsExtendUnderTheStatusBar(
            "CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift"
        )
    }

    func testPlusOfferAndReducedStatesKeepTheSafeViewport() throws {
        try assertOnlyBackgroundsExtendUnderTheStatusBar(
            "CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift"
        )
    }

    private func assertOnlyBackgroundsExtendUnderTheStatusBar(_ path: String) throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
        var content = source.components(separatedBy: .whitespacesAndNewlines).joined()
        XCTAssertTrue(content.contains("ScrollView{"), "No scrolling surface was inspected")
        for background in [
            "CleansiaColors.background.ignoresSafeArea()",
            "CleansiaColors.surface.ignoresSafeArea(edges:.bottom)",
            "LinearGradient(colors:BrandGradient.blue.colors,startPoint:.top,endPoint:.bottom)"
                + ".ignoresSafeArea(.container,edges:.top)",
            "MembershipPalette.heroGradient.ignoresSafeArea(.container,edges:.top)"
        ] {
            content = content.replacingOccurrences(of: background, with: "")
        }
        XCTAssertFalse(content.contains(".ignoresSafeArea("), "Content can escape its safe viewport in \(path)")
        XCTAssertFalse(content.contains("topInset"), "Manual content insets no longer match the safe viewport")
    }
}
