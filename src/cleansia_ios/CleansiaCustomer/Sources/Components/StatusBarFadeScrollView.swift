import CleansiaCore
import SwiftUI

/// A vertical scroll view whose content fades out under the status bar once it scrolls — for the
/// screens that hide the navigation bar (Home, Profile, the Plus offer). Shown only once the content
/// has scrolled, so the full-bleed heroes stay untouched at rest and a pull-to-refresh never raises it.
///
/// The fade is one solid colour, the colour actually behind the status bar: the page colour, or — on a
/// screen with a hero at its top (`heroTint`, the hero marked `statusBarFadeHero()`) — the hero's top
/// colour while the hero is under the status bar, cross-faded into the page colour as the hero scrolls
/// past it, so a dark hero never wears a pale band. It is held at 90 % behind the status bar, so the
/// content under the clock stays out of its way while the system's clock, signal and battery stay
/// legible on it, and eased out to clear over a short tail below it, sampled at several stops so no
/// line marks the status bar's edge. No material: a blur under the colour read as a different colour.
/// With Reduce Transparency on, the colour is drawn at full strength. It is the same on every version:
/// iOS 26's system soft scroll edge was tried and the system draws it well below the status bar, at a
/// height an app cannot set.
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

/// The fade itself: drawn upward from the safe-area line by the status bar's height. The reader keeps the
/// safe area on purpose: one that ignores it reports a top inset of 0, which collapses the fade to its tail.
private struct StatusBarFadeBand: View {
    let heroTint: Color?
    @Binding var heroBottom: CGFloat
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency

    var body: some View {
        GeometryReader { proxy in
            let top = proxy.safeAreaInsets.top
            ZStack {
                CleansiaColors.background
                if let heroTint {
                    heroTint.opacity(StatusBarFade.heroShare(heroBottom: heroBottom, statusBar: top))
                }
            }
            .mask(LinearGradient(
                stops: StatusBarFade.stops(statusBar: top, reduceTransparency: reduceTransparency),
                startPoint: .top,
                endPoint: .bottom
            ))
            .frame(height: top + StatusBarFade.tail)
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
    /// How far below the status bar the fade's tail reaches.
    static let tail: CGFloat = 10
    /// The colour's strength behind the status bar: the content under the clock stays out of its way,
    /// and the system's clock, signal and battery stay legible on it in light and dark mode.
    static let opacity = 0.9
    /// The hero's bottom edge is reported only within this band around the status bar, which takes in
    /// the tallest status bar (62pt) and the tail.
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

    /// How much of the fade wears the hero's colour: all of it while the hero still reaches below the
    /// tail, none once its bottom has passed above the status bar, and in between in proportion — the
    /// page colour takes over as the hero scrolls out from behind the fade, with no jump.
    static func heroShare(heroBottom: CGFloat, statusBar top: CGFloat) -> Double {
        let span = max(top + tail, 1)
        return Double(min(max((heroBottom + top) / span, 0), 1))
    }

    /// The fade's mask: held across the status bar, then a smoothstep falloff to clear at the tail's
    /// end, sampled at eight points so the eased curve shows no seam. Reduce Transparency holds it full.
    static func stops(statusBar top: CGFloat, reduceTransparency: Bool = false) -> [Gradient.Stop] {
        let peak = reduceTransparency ? 1 : opacity
        let hold = top / max(top + tail, 1)
        let samples = 8
        let falloff = (0 ... samples).map { index -> Gradient.Stop in
            let progress = Double(index) / Double(samples)
            let eased = progress * progress * (3 - 2 * progress)
            return .init(
                color: .black.opacity(peak * (1 - eased)),
                location: hold + (1 - hold) * CGFloat(progress)
            )
        }
        return [.init(color: .black.opacity(peak), location: 0)] + falloff
    }
}
