import CleansiaCore
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
            "VStack(spacing:0){ProfileTab.heroTop(colorScheme).frame(height:heroBleed)"
                + "LinearGradient(colors:[ProfileTab.heroTop(colorScheme),BrandGradient.blue.colors[1]],"
                + "startPoint:.top,endPoint:.bottom)}.padding(.top,-heroBleed)"
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
        XCTAssertTrue(profile.contains("StatusBarFadeScrollView(heroTint:Self.heroTop(colorScheme)){"))
        XCTAssertTrue(
            profile.contains("onAvatarLoadSuccess:onAvatarLoadSuccess).statusBarFadeHero()"),
            "the Profile hero is not marked"
        )
        XCTAssertTrue(
            try compactSource("CleansiaCustomer/Sources/Features/Home/HomeTab.swift")
                .contains("StatusBarFadeScrollView{")
        )
    }

    /// The Plus fade wears the navy on every version and in both schemes: the screen asks for the white
    /// clock while the fade is dark enough for it, so iOS 16's black light-mode clock (1.7:1 on the 90 %
    /// navy band) no longer sits on it, and the page-colour exception it needed is gone (owner decision
    /// 2026-10-05).
    func testThePlusFadeWearsTheNavyOnEveryVersion() throws {
        let plus = try compactSource("CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift")
        XCTAssertFalse(plus.contains("fadeHeroTint"))
        XCTAssertFalse(plus.contains("#available(iOS17"))
        // With no plan to price, the navy hero does not scroll and is always under the status bar.
        XCTAssertTrue(plus.contains(
            ".background(MembershipPalette.heroGradient.ignoresSafeArea(.container,edges:.top))"
                + ".background(StatusBarStyleBridge(lightContent:true))"
        ), "the reduced offer leaves iOS 16's black light-mode clock on the navy")
    }

    /// The status bar sits on Profile's hero at rest and on the fade in the hero's top colour once scrolled,
    /// where the screen asks for the white clock, so it must read on that colour (finding 2026-10-04: white
    /// on the brand blue's sky-600, 4.1:1): sky-700 in light mode on every version, the brand blue's sky-800
    /// in dark. The shared brand blue is not changed.
    func testTheClockReadsOnProfilesHeroAtLeastAt4_5() throws {
        let white = SIMD3<Double>(1, 1, 1)
        for style in [UIUserInterfaceStyle.light, .dark] {
            let top = rgb(ProfileTab.heroTop(style == .dark ? .dark : .light), style)
            XCTAssertGreaterThanOrEqual(contrast(white, top), 4.5, "\(style) at rest")
            // Scrolled, the fade wears the top colour at 90 % over whatever passes under it — at worst the
            // white avatar or stats card.
            let fade = top * StatusBarFade.opacity + white * (1 - StatusBarFade.opacity)
            XCTAssertGreaterThanOrEqual(contrast(white, fade), 4.5, "\(style) scrolled, over white content")
        }
        let brandTop = try XCTUnwrap(BrandGradient.blue.stops.first)
        XCTAssertEqual(brandTop.light, 0x0284C7, "the shared brand blue changed")
        let lightTop = rgb(ProfileTab.heroTop(.light), .light), sky700 = hex(0x0369A1)
        for channel in 0 ..< 3 {
            XCTAssertEqual(lightTop[channel], sky700[channel], accuracy: 0.002, "the light hero's top is not sky-700")
        }
        let profile = try compactSource("CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift")
        XCTAssertFalse(profile.contains("#available(iOS17"), "the hero's top differs by version again")
    }

    /// While the fade wears a dark hero's colour the screen asks for the white clock; once the page colour has
    /// taken over it leaves the choice to the system, which draws it black on the light page and white on the
    /// dark one (owner decision 2026-10-05: from iOS 17 the system drew it black on Profile's 90 % sky-700 at
    /// 4.1:1 when white content was under it). On every share the clock it gets reads 4.5:1 or more: white
    /// over white content when asked for, or a fade over the hero light enough for black.
    func testTheWhiteClockIsAskedForWhileTheFadeIsDarkEnoughForIt() {
        let white = SIMD3<Double>(1, 1, 1)
        for (name, tint) in [("Profile", ProfileTab.heroTop(.light)), ("Plus", MembershipPalette.sky950)] {
            let hero = StatusBarFade.rgb(tint, .light)
            let page = StatusBarFade.rgb(CleansiaColors.background, .light)
            XCTAssertTrue(StatusBarFade.asksForWhiteClock(share: 1, hero: hero, page: page), "\(name) at rest")
            XCTAssertFalse(StatusBarFade.asksForWhiteClock(share: 0, hero: hero, page: page), "\(name) scrolled past")
            for step in 0 ... 1000 {
                let share = StatusBarFade.legibleShare(Double(step) / 1000, hero: hero, page: page)
                if StatusBarFade.asksForWhiteClock(share: share, hero: hero, page: page) {
                    let fade = StatusBarFade.fade(share, hero: hero, page: page, over: white)
                    XCTAssertGreaterThanOrEqual(contrast(white, fade), 4.5, "\(name) at \(share)")
                } else {
                    let overHero = StatusBarFade.fade(share, hero: hero, page: page, over: hero)
                    XCTAssertGreaterThanOrEqual(
                        StatusBarFade.luminance(overHero), StatusBarFade.blackClockFloor, "\(name) at \(share)"
                    )
                }
            }
        }
        for tint in [ProfileTab.heroTop(.dark), MembershipPalette.sky950] {
            let hero = StatusBarFade.rgb(tint, .dark)
            let page = StatusBarFade.rgb(CleansiaColors.background, .dark)
            for step in 0 ... 100 {
                XCTAssertTrue(StatusBarFade.asksForWhiteClock(share: Double(step) / 100, hero: hero, page: page))
            }
        }
        // A hero too light for the white clock is left to the system.
        let brandBlue = StatusBarFade.rgb(BrandGradient.blue.colors[0], .light)
        let page = StatusBarFade.rgb(CleansiaColors.background, .light)
        XCTAssertFalse(StatusBarFade.asksForWhiteClock(share: 1, hero: brandBlue, page: page))
    }

    /// The bridge asks UIKit for the white clock when told to and for the system's choice otherwise, and the
    /// fade hands it the same drawn share it paints.
    @MainActor
    func testTheBridgeAsksForTheStyleTheFadeComputes() throws {
        let controller = StatusBarStyleBridge.Controller()
        XCTAssertEqual(controller.preferredStatusBarStyle, .default)
        controller.lightContent = true
        XCTAssertEqual(controller.preferredStatusBarStyle, .lightContent)
        controller.lightContent = false
        XCTAssertEqual(controller.preferredStatusBarStyle, .default)

        let fade = try compactSource("CleansiaCustomer/Sources/Components/StatusBarFadeScrollView.swift")
        XCTAssertTrue(fade.contains(
            "StatusBarStyleBridge(lightContent:StatusBarFade.asksForWhiteClock(share:share,hero:hero,page:page))"
        ))
        XCTAssertTrue(fade.contains("heroTint.opacity(share)"))
    }

    /// The hero's colour covers the fade while the hero reaches below it and gives way to the page
    /// colour, in proportion and without a jump, as the hero's bottom passes up through it. The hero's
    /// bottom is in the scroll view's space (0 at the safe area's top), where the fade spans
    /// `-top ... height - top`.
    func testTheHerosColourCrossFadesIntoThePageColourAsTheHeroScrollsPast() {
        for top: CGFloat in [20, 47, 59, 62] {
            let height = StatusBarFade.height(safeTop: top)
            func share(_ bottom: CGFloat) -> Double {
                StatusBarFade.heroShare(heroBottom: bottom, safeTop: top, height: height)
            }
            XCTAssertEqual(share(400), 1)
            XCTAssertEqual(share(height - top), 1, "the hero still under the whole fade does not fill it")
            XCTAssertEqual(share(-top), 0)
            XCTAssertEqual(share(-400), 0)
            XCTAssertEqual(share(height / 2 - top), 0.5, accuracy: 0.0001)
            var previous = 0.0
            for bottom in stride(from: -top, through: height - top, by: 1) {
                let next = share(bottom)
                XCTAssertGreaterThanOrEqual(next, previous, "the hero's colour flickers back")
                XCTAssertLessThanOrEqual(next - previous, 1 / height + 0.0001, "the cross-fade jumps")
                previous = next
            }
        }
    }

    /// From iOS 17 the system draws the clock white or black from what is under it, and over Profile's
    /// proportional cross-fade it kept it white until the fade was nearly as light as 0.5, down to 2.2:1
    /// (finding 2026-10-05). With a dark hero over the light page the hero's share now steps over those
    /// shades: on every share the fade is either dark enough for the white clock at 4.5:1 over white content,
    /// or, over the hero itself, light enough that the system draws it black. It jumps across once, at the
    /// band's middle, never back, and leaves every share outside the band alone.
    func testTheHerosShareStepsOverTheShadesTheClockCannotBeReadOn() {
        let page = StatusBarFade.rgb(CleansiaColors.background, .light)
        let white = SIMD3<Double>(1, 1, 1)
        for (name, tint) in [("Profile", ProfileTab.heroTop(.light)), ("Plus", MembershipPalette.sky950)] {
            let hero = StatusBarFade.rgb(tint, .light)
            var previous = 1.0
            var jumps = 0
            for step in stride(from: 1000, through: 0, by: -1) {
                let share = Double(step) / 1000
                let stepped = StatusBarFade.legibleShare(share, hero: hero, page: page)
                let overWhite = StatusBarFade.fade(stepped, hero: hero, page: page, over: white)
                let overHero = StatusBarFade.fade(stepped, hero: hero, page: page, over: hero)
                XCTAssertTrue(
                    contrast(white, overWhite) >= 4.5
                        || StatusBarFade.luminance(overHero) >= StatusBarFade.blackClockFloor,
                    "\(name) at \(share): the clock is white on a shade it cannot be read on"
                )
                XCTAssertLessThanOrEqual(stepped, previous, "\(name): the hero's colour flickers back")
                if previous - stepped > 0.05 { jumps += 1 }
                previous = stepped
            }
            XCTAssertEqual(jumps, 1, name)
            XCTAssertEqual(StatusBarFade.legibleShare(1, hero: hero, page: page), 1, name)
            XCTAssertEqual(StatusBarFade.legibleShare(0, hero: hero, page: page), 0, name)
            XCTAssertEqual(StatusBarFade.legibleShare(0.98, hero: hero, page: page), 0.98, name)
            XCTAssertEqual(StatusBarFade.legibleShare(0.1, hero: hero, page: page), 0.1, name)
        }
    }

    /// No band to step over: a dark page (dark mode), where the clock is white on every shade, and a hero
    /// too light for the white clock (the brand blue), where it is the system's black.
    func testTheShareKeepsItsProportionWhereNoShadeIsIllegible() {
        let cases: [(name: String, scheme: ColorScheme)] = [("Profile", .dark), ("Plus", .dark), ("brand blue", .light)]
        let tints = [ProfileTab.heroTop(.dark), MembershipPalette.sky950, BrandGradient.blue.colors[0]]
        for ((name, scheme), tint) in zip(cases, tints) {
            let hero = StatusBarFade.rgb(tint, scheme)
            let page = StatusBarFade.rgb(CleansiaColors.background, scheme)
            for step in 0 ... 100 {
                let share = Double(step) / 100
                XCTAssertEqual(StatusBarFade.legibleShare(share, hero: hero, page: page), share, "\(name) at \(share)")
            }
        }
    }

    /// The hero's reader reports only near the status bar, in whole points — enough for the tallest
    /// status bar and the fade — so scrolling elsewhere never redraws the fade.
    func testTheHeroIsReportedOnlyNearTheStatusBar() {
        XCTAssertEqual(StatusBarFade.heroBottom(maxY: 512.4), StatusBarFade.heroBottomRange.upperBound)
        XCTAssertEqual(StatusBarFade.heroBottom(maxY: -700), StatusBarFade.heroBottomRange.lowerBound)
        XCTAssertEqual(StatusBarFade.heroBottom(maxY: -12.4), -12)
        XCTAssertLessThanOrEqual(StatusBarFade.heroBottomRange.lowerBound, -62)
        for top: CGFloat in [20, 47, 59, 62] {
            XCTAssertGreaterThanOrEqual(
                StatusBarFade.heroBottomRange.upperBound,
                StatusBarFade.height(safeTop: top) - top
            )
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

    /// The status bar's content measured in the simulators, in points from the screen's top (2026-10-05):
    /// the safe area's top, the bottom of the Dynamic Island, the notch or a home-button phone's status
    /// bar, and the bottom of the clock's digits.
    private struct MeasuredPhone {
        let name: String
        let safeTop: CGFloat
        let housingBottom: CGFloat
        let clockBottom: CGFloat
    }

    private let measuredPhones = [
        MeasuredPhone(name: "iPhone 17 Pro, iOS 26.3", safeTop: 62, housingBottom: 50.67, clockBottom: 39),
        MeasuredPhone(name: "iPhone 16, iOS 18.6", safeTop: 59, housingBottom: 48, clockBottom: 35.67),
        MeasuredPhone(name: "iPhone 14 Pro, iOS 16.4", safeTop: 59, housingBottom: 48, clockBottom: 35.67),
        MeasuredPhone(name: "iPhone SE 3rd gen, iOS 16.4", safeTop: 20, housingBottom: 20, clockBottom: 15.5)
    ]

    /// The notch phones (2026-10-05). A notch's clock ends only 1–4.9pt above the notch, so the ease at the
    /// fade's end is shorter on them (`StatusBarFade.falloff(safeTop:)`), to begin at or under the digits'
    /// baseline as it does under an island.
    private let measuredNotchPhones = [
        MeasuredPhone(name: "iPhone X, iOS 16.4", safeTop: 44, housingBottom: 30, clockBottom: 28.33),
        MeasuredPhone(name: "iPhone XS, iOS 18.6", safeTop: 44, housingBottom: 30, clockBottom: 28.33),
        MeasuredPhone(name: "iPhone 11 Pro, iOS 18.6", safeTop: 44, housingBottom: 30, clockBottom: 28.33),
        MeasuredPhone(name: "iPhone 12, iOS 18.6", safeTop: 47, housingBottom: 32, clockBottom: 30.67),
        MeasuredPhone(name: "iPhone 13, iOS 26.3", safeTop: 47, housingBottom: 33.67, clockBottom: 30.67),
        MeasuredPhone(name: "iPhone 16e, iOS 18.6", safeTop: 47, housingBottom: 33.67, clockBottom: 30.67),
        MeasuredPhone(name: "iPhone XR, iOS 16.4", safeTop: 48, housingBottom: 33, clockBottom: 30.5),
        MeasuredPhone(name: "iPhone 11, iOS 18.6", safeTop: 48, housingBottom: 33, clockBottom: 30.5),
        MeasuredPhone(name: "iPhone 12 mini, iOS 18.6 and 26.3", safeTop: 50, housingBottom: 34.03, clockBottom: 32.99),
        MeasuredPhone(name: "iPhone 13 mini, iOS 26.3", safeTop: 50, housingBottom: 37.5, clockBottom: 32.64)
    ]

    /// The fade ends where the status bar's content does — the Dynamic Island's bottom, the notch's, or a
    /// home-button phone's status bar's (owner remark 2026-10-04: it reached 10pt below the safe area's
    /// top, 72pt on an iPhone 17 Pro, far under the island) — within 2pt on every phone measured.
    func testTheFadeEndsAtTheBottomOfTheIslandTheNotchOrTheStatusBar() {
        for phone in measuredPhones + measuredNotchPhones {
            XCTAssertEqual(
                StatusBarFade.height(safeTop: phone.safeTop),
                phone.housingBottom,
                accuracy: 2,
                phone.name
            )
            XCTAssertLessThan(StatusBarFade.height(safeTop: phone.safeTop), phone.safeTop + 0.01, phone.name)
        }
        XCTAssertEqual(StatusBarFade.height(safeTop: 0), 0, "a hidden status bar keeps a fade")
    }

    /// One colour held at about 90 % over the clock, signal and battery (owner remark 2026-10-04: the 40 %
    /// wash was too see-through), then eased to clear by the fade's end over its last few points with no
    /// step a line could show at: the stops never rise, fall by little at a time and end clear. The content
    /// under the clock's digits is held back on every phone, notch phones included (finding 2026-10-05: a
    /// 5pt ease under a notch left 0.52–0.80 at the digits' baseline).
    func testTheFadeHoldsOverTheClockAndEasesOutByItsEnd() {
        XCTAssertEqual(StatusBarFade.opacity, 0.9, accuracy: 0.02)
        for phone in measuredPhones + measuredNotchPhones {
            let height = StatusBarFade.height(safeTop: phone.safeTop)
            let falloff = StatusBarFade.falloff(safeTop: phone.safeTop)
            XCTAssertTrue((3 ... 8).contains(falloff), "\(phone.name): the ease at the end is not a few points")
            let stops = StatusBarFade.stops(height: height, falloff: falloff)
            let alphas = stops.map { UIColor($0.color).cgColor.alpha }
            let locations = stops.map(\.location)
            XCTAssertGreaterThanOrEqual(stops.count, 8, "too few stops for an eased curve")
            XCTAssertEqual(locations.first, 0)
            XCTAssertEqual(locations.last ?? 0, 1, accuracy: 0.0001)
            XCTAssertEqual(alphas.first ?? 0, StatusBarFade.opacity, accuracy: 0.001)
            XCTAssertEqual(alphas[1], StatusBarFade.opacity, accuracy: 0.001, "the hold is not even")
            XCTAssertEqual(alphas.last ?? 1, 0, accuracy: 0.001)
            XCTAssertEqual(
                locations[1] * height,
                height - falloff,
                accuracy: 0.0001,
                "the ease does not start a few points above the fade's end"
            )
            XCTAssertGreaterThanOrEqual(
                alpha(at: phone.clockBottom / height, stops: stops),
                0.85,
                "\(phone.name): the content under the clock's digits is not held back"
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
        let stops = StatusBarFade.stops(
            height: StatusBarFade.height(safeTop: 59),
            falloff: StatusBarFade.falloff(safeTop: 59),
            reduceTransparency: true
        )
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
        XCTAssertTrue(fade.contains("letheight=StatusBarFade.height(safeTop:top)"))
        XCTAssertTrue(fade.contains(
            "StatusBarFade.stops(height:height,falloff:StatusBarFade.falloff(safeTop:top),"
                + "reduceTransparency:reduceTransparency)"
        ))
        XCTAssertTrue(fade.contains(".frame(height:height).offset(y:-top)"), "the fade is not as tall as its rule")
        XCTAssertTrue(fade.contains(
            "letpage=StatusBarFade.rgb(CleansiaColors.background,colorScheme)"
                + "lethero=heroTint.map{StatusBarFade.rgb($0,colorScheme)}"
                + "letshare=hero.map{StatusBarFade.legibleShare("
                + "StatusBarFade.heroShare(heroBottom:heroBottom,safeTop:top,height:height),hero:$0,page:page)}??0"
                + "ZStack{CleansiaColors.backgroundifletheroTint{heroTint.opacity(share)}}"
        ))
        XCTAssertTrue(fade.contains(".allowsHitTesting(false).accessibilityHidden(true)"))
    }

    private func rgb(_ color: Color, _ style: UIUserInterfaceStyle) -> SIMD3<Double> {
        let resolved = UIColor(color).resolvedColor(with: UITraitCollection(userInterfaceStyle: style))
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        resolved.getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        return SIMD3(Double(red), Double(green), Double(blue))
    }

    private func hex(_ value: UInt32) -> SIMD3<Double> {
        SIMD3(Double((value >> 16) & 0xFF), Double((value >> 8) & 0xFF), Double(value & 0xFF)) / 255
    }

    private func contrast(_ first: SIMD3<Double>, _ second: SIMD3<Double>) -> Double {
        func luminance(_ color: SIMD3<Double>) -> Double {
            let linear = [color.x, color.y, color.z].map { $0 <= 0.04045 ? $0 / 12.92 : pow(($0 + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
        }
        let (lighter, darker) = (max(luminance(first), luminance(second)), min(luminance(first), luminance(second)))
        return (lighter + 0.05) / (darker + 0.05)
    }

    /// The mask's alpha at `location`, linearly between its stops as the gradient draws it.
    private func alpha(at location: CGFloat, stops: [Gradient.Stop]) -> CGFloat {
        for (earlier, later) in zip(stops, stops.dropFirst()) where location <= later.location {
            let from = UIColor(earlier.color).cgColor.alpha, to = UIColor(later.color).cgColor.alpha
            let span = later.location - earlier.location
            return span <= 0 ? to : from + (to - from) * (location - earlier.location) / span
        }
        return UIColor(stops.last?.color ?? .clear).cgColor.alpha
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
