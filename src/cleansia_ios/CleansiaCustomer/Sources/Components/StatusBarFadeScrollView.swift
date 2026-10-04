import CleansiaCore
import SwiftUI

/// A vertical scroll view whose content fades out under the status bar once it scrolls — for the
/// screens that hide the navigation bar (Home, Profile, the Plus offer). Shown only once the content
/// has scrolled, so the full-bleed heroes stay untouched at rest and a pull-to-refresh never raises it.
///
/// The fade is one solid colour, the colour actually behind the status bar: the page colour, or — on a
/// screen with a hero at its top (`heroTint`, the hero marked `statusBarFadeHero()`) — the hero's top
/// colour while the hero is under the status bar, cross-faded into the page colour as the hero scrolls
/// past it, so a dark hero never wears a pale band. It reaches only as far as the status bar's content
/// (the Dynamic Island's or the notch's bottom, or a home-button phone's status bar; see
/// `StatusBarFade.height`), held at 90 % behind the clock, signal and battery, so the content under
/// them stays out of their way, and eased out to clear over its last few points, sampled at several
/// stops so no line marks its end. The glyphs' colour is the system's: before iOS 17 it follows the
/// colour scheme whatever is under it (black in light mode), so a screen hands over a dark `heroTint`
/// only where the glyphs read on it (the Plus offer's `fadeHeroTint`). No material: a blur under the
/// colour read as a different colour. With Reduce Transparency on, the colour is drawn at full
/// strength. It is the same on every version: iOS 26's system soft scroll edge was tried and the
/// system draws it well below the status bar, at a height an app cannot set.
///
/// → /mobile-app/patterns#status-bar-fade
struct StatusBarFadeScrollView<Content: View>: View {
    private let heroTint: Color?
    private let content: Content
    @State private var isScrolled = false
    /// Written by the marked hero's reader and read only by the fade, so scrolling the hero past the
    /// status bar redraws the fade alone, never the content.
    @State private var heroBottom = StatusBarFade.heroBottomRange.upperBound

    init(heroTint: Color? = nil, @ViewBuilder content: () -> Content) {
        self.heroTint = heroTint
        self.content = content()
    }

    var body: some View {
        ZStack(alignment: .top) {
            scroll
            StatusBarFadeBand(heroTint: heroTint, heroBottom: $heroBottom)
                .opacity(isScrolled ? 1 : 0)
                .animation(.easeInOut(duration: 0.2), value: isScrolled)
        }
    }

    private var scroll: some View {
        ScrollView {
            content
                .environment(\.statusBarFadeHeroBottom, heroTint == nil ? nil : $heroBottom)
                .background(alignment: .top) {
                    GeometryReader { proxy in
                        let scrolled = StatusBarFade.isScrolled(
                            contentMinY: proxy.frame(in: .named(StatusBarFade.space)).minY
                        )
                        // Keyed on the threshold, not the offset, so state is written only when the
                        // content crosses it rather than on every scrolled frame. A preference key would
                        // not do: the scroll view does not relay its content's changes to an
                        // `onPreferenceChange` outside it.
                        Color.clear.onChange(of: scrolled) { isScrolled = $0 }
                    }
                }
        }
        .coordinateSpace(name: StatusBarFade.space)
    }
}

/// The fade itself: drawn from the screen's top edge (the safe-area line less its inset) down to the status
/// bar's content. The reader keeps the safe area on purpose: one that ignores it reports a top inset of 0,
/// which collapses the fade to nothing.
private struct StatusBarFadeBand: View {
    let heroTint: Color?
    @Binding var heroBottom: CGFloat
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency

    var body: some View {
        GeometryReader { proxy in
            let top = proxy.safeAreaInsets.top
            let height = StatusBarFade.height(safeTop: top)
            ZStack {
                CleansiaColors.background
                if let heroTint {
                    heroTint.opacity(StatusBarFade.heroShare(heroBottom: heroBottom, safeTop: top, height: height))
                }
            }
            .mask(LinearGradient(
                stops: StatusBarFade.stops(height: height, reduceTransparency: reduceTransparency),
                startPoint: .top,
                endPoint: .bottom
            ))
            .frame(height: height)
            .offset(y: -top)
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

extension View {
    /// Marks the hero whose colour the enclosing `StatusBarFadeScrollView` wears (its `heroTint`) while
    /// the hero is under the status bar.
    func statusBarFadeHero() -> some View {
        background {
            GeometryReader { proxy in
                StatusBarFadeHeroReader(
                    bottom: StatusBarFade.heroBottom(maxY: proxy.frame(in: .named(StatusBarFade.space)).maxY)
                )
            }
        }
    }
}

/// Reports the hero's bottom edge only while it is near the status bar (clamped and whole points), so
/// the fade is redrawn as the hero passes the status bar and not on every other scrolled frame.
private struct StatusBarFadeHeroReader: View {
    @Environment(\.statusBarFadeHeroBottom) private var heroBottom
    let bottom: CGFloat

    var body: some View {
        Color.clear
            .onAppear { heroBottom?.wrappedValue = bottom }
            .onChange(of: bottom) { heroBottom?.wrappedValue = $0 }
    }
}

private struct StatusBarFadeHeroBottomKey: EnvironmentKey {
    static let defaultValue: Binding<CGFloat>? = nil
}

private extension EnvironmentValues {
    var statusBarFadeHeroBottom: Binding<CGFloat>? {
        get { self[StatusBarFadeHeroBottomKey.self] }
        set { self[StatusBarFadeHeroBottomKey.self] = newValue }
    }
}

enum StatusBarFade {
    /// A home-button phone's status bar, its whole height the clock's row; its safe area's top is the same.
    static let classicStatusBar: CGFloat = 20
    /// How far above the safe area's top the Dynamic Island or the notch ends. Neither is reported by the
    /// system, and the status-bar frame it does report overshoots both (54pt over an island that ends at
    /// 48–50.7pt, 47pt over the iPhone 16e's notch that ends at 33.7pt). The safe area's top sits a
    /// near-constant distance below them: measured in the simulators, 11.3pt on the iPhone 17 Pro (iOS 26.3),
    /// 11pt on the iPhone 16 (18.6) and 14 Pro (16.4), 13.3pt on the 16e (18.6), so 12pt lands within
    /// 1.3pt of each.
    static let housingClearance: CGFloat = 12
    /// The eased run at the fade's end. There is little room under the clock: its row ends 11pt above an
    /// island's bottom, but 3–4.5pt above a notch's or a home-button phone's status bar's, so 5pt eases
    /// out below the clock on every phone measured (the mask is still over 85 % at its baseline).
    static let falloff: CGFloat = 5
    /// The colour's strength behind the status bar: the content under the clock stays out of its way,
    /// and the system's clock, signal and battery stay legible on it in light and dark mode, on a
    /// colour they read on (see the type's note on `heroTint`).
    static let opacity = 0.9
    /// The hero's bottom edge is reported only within this band around the status bar, which takes in
    /// the tallest status bar (62pt) and the fade.
    static let heroBottomRange: ClosedRange<CGFloat> = -80 ... 20
    static let space = "statusBarFade"

    /// The content's top, measured in the scroll view's space, sits at 0 at rest, rises above it as the
    /// content scrolls up, and drops below it during a pull-to-refresh.
    static func isScrolled(contentMinY: CGFloat) -> Bool {
        contentMinY < -1
    }

    /// The hero's bottom edge in the scroll view's space, as its reader reports it.
    static func heroBottom(maxY: CGFloat) -> CGFloat {
        min(max(maxY, heroBottomRange.lowerBound), heroBottomRange.upperBound).rounded()
    }

    /// How tall the fade is, from the screen's top edge: to the bottom of the status bar's content (owner
    /// remark 2026-10-04: the clock and the island's line, no further). A phone with a Dynamic Island or a
    /// notch ends `housingClearance` above the safe area's top; a home-button phone, at its 20pt status bar.
    static func height(safeTop: CGFloat) -> CGFloat {
        safeTop > classicStatusBar ? safeTop - housingClearance : max(safeTop, 0)
    }

    /// How much of the fade wears the hero's colour: all of it while the hero still reaches below the
    /// fade, none once its bottom has passed above the screen's top, and in between in proportion — the
    /// page colour takes over as the hero scrolls out from behind the fade, with no jump. The hero's bottom
    /// is in the scroll view's space, whose origin is the safe area's top, so the fade spans `-safeTop` to
    /// `height - safeTop` there.
    static func heroShare(heroBottom: CGFloat, safeTop: CGFloat, height: CGFloat) -> Double {
        Double(min(max((heroBottom + safeTop) / max(height, 1), 0), 1))
    }

    /// The fade's mask: held across the status bar's content, then a smoothstep falloff over its last
    /// `falloff` points to clear at its end, sampled at eight points so the eased curve shows no seam.
    /// Reduce Transparency holds it full.
    static func stops(height: CGFloat, reduceTransparency: Bool = false) -> [Gradient.Stop] {
        let peak = reduceTransparency ? 1 : opacity
        let hold = max(height - falloff, 0) / max(height, 1)
        let samples = 8
        let ease = (0 ... samples).map { index -> Gradient.Stop in
            let progress = Double(index) / Double(samples)
            let eased = progress * progress * (3 - 2 * progress)
            return .init(
                color: .black.opacity(peak * (1 - eased)),
                location: hold + (1 - hold) * CGFloat(progress)
            )
        }
        return [.init(color: .black.opacity(peak), location: 0)] + ease
    }
}
