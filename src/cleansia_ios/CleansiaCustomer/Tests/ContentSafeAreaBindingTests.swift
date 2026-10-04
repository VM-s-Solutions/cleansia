import SwiftUI
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
            XCTAssertTrue(try compactSource(path).contains("StatusBarFadeScrollView"), path)
        }
    }

    /// The fade is the colour actually behind the status bar (owner remark 2026-10-04: on the Plus offer
    /// it was a pale band over the navy hero). The two screens with a hero at their top hand the fade
    /// the hero's top colour and mark the hero; Home's top is the page, so its fade is the page colour.
    func testTheHeroScreensFadeInTheirHerosColour() throws {
        let plus = try compactSource("CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift")
        XCTAssertTrue(plus.contains("StatusBarFadeScrollView(heroTint:MembershipPalette.sky950){"))
        XCTAssertTrue(plus.contains("onBack:onBack).statusBarFadeHero()"), "the Plus hero is not marked")
        let profile = try compactSource("CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift")
        XCTAssertTrue(profile.contains("StatusBarFadeScrollView(heroTint:BrandGradient.blue.colors[0]){"))
        XCTAssertTrue(
            profile.contains("onAvatarLoadSuccess:onAvatarLoadSuccess).statusBarFadeHero()"),
            "the Profile hero is not marked"
        )
        XCTAssertTrue(
            try compactSource("CleansiaCustomer/Sources/Features/Home/HomeTab.swift")
                .contains("StatusBarFadeScrollView{")
        )
    }

    /// The hero's colour covers the fade while the hero reaches below it and gives way to the page
    /// colour, in proportion and without a jump, as the hero's bottom passes up through it.
    func testTheHerosColourCrossFadesIntoThePageColourAsTheHeroScrollsPast() {
        for top: CGFloat in [20, 47, 62] {
            let span = top + StatusBarFade.tail
            XCTAssertEqual(StatusBarFade.heroShare(heroBottom: 400, statusBar: top), 1)
            XCTAssertEqual(StatusBarFade.heroShare(heroBottom: StatusBarFade.tail, statusBar: top), 1)
            XCTAssertEqual(StatusBarFade.heroShare(heroBottom: -top, statusBar: top), 0)
            XCTAssertEqual(StatusBarFade.heroShare(heroBottom: -400, statusBar: top), 0)
            XCTAssertEqual(
                StatusBarFade.heroShare(heroBottom: StatusBarFade.tail - span / 2, statusBar: top),
                0.5,
                accuracy: 0.0001
            )
            var previous = 0.0
            for bottom in stride(from: -top, through: StatusBarFade.tail, by: 1) {
                let share = StatusBarFade.heroShare(heroBottom: bottom, statusBar: top)
                XCTAssertGreaterThanOrEqual(share, previous, "the hero's colour flickers back")
                XCTAssertLessThanOrEqual(share - previous, 1 / span + 0.0001, "the cross-fade jumps")
                previous = share
            }
        }
    }

    /// The hero's reader reports only near the status bar, in whole points — enough for the tallest
    /// status bar and the tail — so scrolling elsewhere never redraws the fade.
    func testTheHeroIsReportedOnlyNearTheStatusBar() {
        XCTAssertEqual(StatusBarFade.heroBottom(maxY: 512.4), StatusBarFade.heroBottomRange.upperBound)
        XCTAssertEqual(StatusBarFade.heroBottom(maxY: -700), StatusBarFade.heroBottomRange.lowerBound)
        XCTAssertEqual(StatusBarFade.heroBottom(maxY: -12.4), -12)
        XCTAssertLessThanOrEqual(StatusBarFade.heroBottomRange.lowerBound, -62)
        XCTAssertGreaterThanOrEqual(StatusBarFade.heroBottomRange.upperBound, StatusBarFade.tail)
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

    /// The fade covers the status bar and a short tail, nothing more: one colour held at about 90 % across
    /// the status bar (owner remark 2026-10-04: the 40 % wash was too see-through), then eased to clear
    /// over the tail with no step a line could show at — the stops never rise, fall by little at a time
    /// and end clear at the tail's end.
    func testTheFadeHoldsAcrossTheStatusBarAndEasesOutOverAShortTail() {
        XCTAssertTrue((8 ... 12).contains(StatusBarFade.tail), "the tail below the status bar is not short")
        XCTAssertEqual(StatusBarFade.opacity, 0.9, accuracy: 0.02)
        for top: CGFloat in [20, 47, 59, 62] {
            let stops = StatusBarFade.stops(statusBar: top)
            let alphas = stops.map { UIColor($0.color).cgColor.alpha }
            let locations = stops.map(\.location)
            XCTAssertGreaterThanOrEqual(stops.count, 8, "too few stops for an eased curve")
            XCTAssertEqual(locations.first, 0)
            XCTAssertEqual(locations.last ?? 0, 1, accuracy: 0.0001)
            XCTAssertEqual(alphas.first ?? 0, StatusBarFade.opacity, accuracy: 0.001)
            XCTAssertEqual(alphas[1], StatusBarFade.opacity, accuracy: 0.001, "the hold is not even")
            XCTAssertEqual(alphas.last ?? 1, 0, accuracy: 0.001)
            XCTAssertEqual(
                locations[1],
                top / (top + StatusBarFade.tail),
                accuracy: 0.0001,
                "the falloff does not start at the status bar's edge"
            )
            for (earlier, later) in zip(stops, stops.dropFirst()) {
                XCTAssertLessThanOrEqual(earlier.location, later.location)
                let fall = UIColor(earlier.color).cgColor.alpha - UIColor(later.color).cgColor.alpha
                XCTAssertGreaterThanOrEqual(fall, -0.0001, "the fade rises again")
                XCTAssertLessThanOrEqual(fall, 0.2, "the fade drops in a step")
            }
        }
    }

    /// With Reduce Transparency on, the colour is drawn at full strength behind the status bar.
    func testReduceTransparencyDrawsTheColourAtFullStrength() {
        let stops = StatusBarFade.stops(statusBar: 59, reduceTransparency: true)
        XCTAssertEqual(UIColor(stops[0].color).cgColor.alpha, 1, accuracy: 0.001)
        XCTAssertEqual(UIColor(stops[1].color).cgColor.alpha, 1, accuracy: 0.001)
        XCTAssertEqual(UIColor(stops.last?.color ?? .black).cgColor.alpha, 0, accuracy: 0.001)
    }

    /// One fade on every version (iOS 26's system edge reaches far below the status bar), one solid
    /// colour with no material under it (the blur read as a different colour), Reduce Transparency read.
    func testTheFadeIsTheSameOnEveryVersionAndOneSolidColour() throws {
        let fade = try compactSource("CleansiaCustomer/Sources/Components/StatusBarFadeScrollView.swift")
        XCTAssertFalse(fade.contains("#available"), "the fade differs by version")
        XCTAssertFalse(fade.contains("scrollEdgeEffect"), "the system edge is back")
        XCTAssertFalse(fade.contains("Material"), "a material is back under the colour")
        XCTAssertTrue(fade.contains("@Environment(\\.accessibilityReduceTransparency)"))
        XCTAssertTrue(fade.contains("StatusBarFade.stops(statusBar:top,reduceTransparency:reduceTransparency)"))
        XCTAssertTrue(fade.contains(
            "CleansiaColors.backgroundifletheroTint{"
                + "heroTint.opacity(StatusBarFade.heroShare(heroBottom:heroBottom,statusBar:top))}"
        ))
        XCTAssertTrue(fade.contains(".allowsHitTesting(false).accessibilityHidden(true)"))
    }

    private func assertOnlyBackgroundsExtendUnderTheStatusBar(_ path: String) throws {
        var content = try compactSource(path)
        XCTAssertTrue(content.contains("ScrollView"), "No scrolling surface was inspected")
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
