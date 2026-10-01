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

    /// A background inside a scroll view has no top safe area left to ignore, so each scrolling hero
    /// paints its own status-bar strip by bleeding its background above its frame — the gradient keeps
    /// the hero's bounds and a block of its first stop rises above them.
    func testTheScrollingHeroesPaintTheirOwnStatusBarStrip() throws {
        let profile = try compactSource("CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift")
        XCTAssertTrue(profile.contains(
            "VStack(spacing:0){BrandGradient.blue.colors[0].frame(height:heroBleed)"
                + "LinearGradient(colors:BrandGradient.blue.colors,startPoint:.top,endPoint:.bottom)}"
                + ".padding(.top,-heroBleed)"
        ))
        let plus = try compactSource("CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift")
        XCTAssertTrue(plus.contains(
            "VStack(spacing:0){MembershipPalette.sky950.frame(height:heroBleed)MembershipPalette.heroGradient}"
                + ".padding(.top,-heroBleed)"
        ))
    }

    /// The sticky Plus CTA is mounted as a bottom inset, so the offer reserves the bar's real height;
    /// a fixed spacer under the perks drifted from the bar as soon as its padding changed.
    func testThePlusOfferReservesTheStickyBarThroughItsInset() throws {
        let plus = try compactSource("CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift")
        XCTAssertTrue(plus.contains(".safeAreaInset(edge:.bottom,spacing:0){ifvm.canSubscribe{StickyCtaBar("))
        XCTAssertFalse(plus.contains("Color.clear.frame(height:140)"))
    }

    /// These three hide the navigation bar, so nothing else gives their content an edge treatment and
    /// it would run under the clock and the Dynamic Island; each scrolls through the fading container.
    func testTheBarlessScreensFadeTheirContentUnderTheStatusBar() throws {
        for path in [
            "CleansiaCustomer/Sources/Features/Home/HomeTab.swift",
            "CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift",
            "CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift"
        ] {
            XCTAssertTrue(try compactSource(path).contains("StatusBarFadeScrollView{"), path)
        }
    }

    /// At rest the heroes reach the top untouched and a pull-to-refresh moves the content down, so only
    /// content that has scrolled up raises the band.
    func testTheFadeShowsOnlyOnceTheContentHasScrolledUp() {
        XCTAssertFalse(StatusBarFade.isScrolled(contentMinY: 0))
        XCTAssertFalse(StatusBarFade.isScrolled(contentMinY: 80), "a pull-to-refresh raised the band")
        XCTAssertFalse(StatusBarFade.isScrolled(contentMinY: -0.5))
        XCTAssertTrue(StatusBarFade.isScrolled(contentMinY: -2))
        XCTAssertTrue(StatusBarFade.isScrolled(contentMinY: -400))
    }

    private func assertOnlyBackgroundsExtendUnderTheStatusBar(_ path: String) throws {
        var content = try compactSource(path)
        XCTAssertTrue(content.contains("ScrollView{"), "No scrolling surface was inspected")
        for background in [
            "CleansiaColors.background.ignoresSafeArea()",
            "CleansiaColors.surface.ignoresSafeArea(edges:.bottom)",
            // The reduced Plus states do not scroll, so their hero still reaches the top this way.
            "MembershipPalette.heroGradient.ignoresSafeArea(.container,edges:.top)"
        ] {
            content = content.replacingOccurrences(of: background, with: "")
        }
        XCTAssertFalse(content.contains(".ignoresSafeArea("), "Content can escape its safe viewport in \(path)")
        XCTAssertFalse(content.contains("topInset"), "Manual content insets no longer match the safe viewport")
    }

    private func compactSource(_ path: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
        return source.components(separatedBy: .whitespacesAndNewlines).joined()
    }
}
