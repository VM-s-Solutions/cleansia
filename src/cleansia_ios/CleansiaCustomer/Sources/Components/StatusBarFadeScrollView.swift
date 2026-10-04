import CleansiaCore
import SwiftUI

/// A vertical scroll view whose content fades out under the status bar once it scrolls — for the
/// screens that hide the navigation bar (Home, Profile, the Plus offer). Shown only once the content
/// has scrolled, so the full-bleed heroes stay untouched at rest and a pull-to-refresh never raises it.
///
/// The fade covers the status bar alone — the clock, signal and battery — and ends in a short soft
/// tail just below it: a light blur under a see-through wash of the page colour, eased out over
/// several stops so no line marks the status bar's edge. The wash moves the content the way the page
/// does (lighter in light mode, darker in dark), which keeps the clock legible over a busy card. It
/// is the same on every version: iOS 26's system soft scroll edge was tried and the system draws it
/// well below the status bar, at a height an app cannot set. With Reduce Transparency on, the wash
/// alone, a little stronger, stands in for the blur.
///
/// → /mobile-app/patterns#status-bar-fade
struct StatusBarFadeScrollView<Content: View>: View {
    @ViewBuilder let content: Content
    @State private var isScrolled = false
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency

    var body: some View {
        ZStack(alignment: .top) {
            scroll
            fade
                .opacity(isScrolled ? 1 : 0)
                .animation(.easeInOut(duration: 0.2), value: isScrolled)
        }
    }

    private var scroll: some View {
        ScrollView {
            content.background(alignment: .top) {
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

    /// Drawn upward from the safe-area line by the status bar's height. The reader keeps the safe area
    /// on purpose: one that ignores it reports a top inset of 0, which collapses the fade to its tail.
    private var fade: some View {
        GeometryReader { proxy in
            let top = proxy.safeAreaInsets.top
            fill
                .mask(LinearGradient(stops: StatusBarFade.stops(statusBar: top), startPoint: .top, endPoint: .bottom))
                .frame(height: top + StatusBarFade.tail)
                .offset(y: -top)
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }

    private var fill: some View {
        ZStack {
            if !reduceTransparency {
                Rectangle().fill(.ultraThinMaterial).opacity(StatusBarFade.blur)
            }
            CleansiaColors.background.opacity(reduceTransparency ? StatusBarFade.solidWash : StatusBarFade.wash)
        }
    }
}

enum StatusBarFade {
    /// How far below the status bar the fade's tail reaches.
    static let tail: CGFloat = 8
    /// The blur's strength behind the clock: light, so the content stays readable through it.
    static let blur = 0.4
    /// The page colour's wash over the blur behind the clock: with the blur, enough to keep the clock
    /// legible over a busy card in light and dark mode, and no more.
    static let wash = 0.4
    /// The wash behind the clock when Reduce Transparency takes the blur away.
    static let solidWash = 0.85
    /// Where the falloff starts, as a share of the status bar's height: the backing is full only across
    /// the top of the status bar and eases out through the rest of it, so no edge shows.
    static let holdShare: CGFloat = 0.35
    static let space = "statusBarFade"

    /// The content's top, measured in the scroll view's space, sits at 0 at rest, rises above it as the
    /// content scrolls up, and drops below it during a pull-to-refresh.
    static func isScrolled(contentMinY: CGFloat) -> Bool {
        contentMinY < -1
    }

    /// The fade's mask over the status bar and the tail: opaque down to the hold, then a smoothstep
    /// falloff to clear at the tail's end, sampled at eight points so the eased curve shows no seam.
    static func stops(statusBar top: CGFloat) -> [Gradient.Stop] {
        let height = max(top + tail, 1)
        let hold = top * holdShare / height
        let samples = 8
        let falloff = (0 ... samples).map { index -> Gradient.Stop in
            let progress = Double(index) / Double(samples)
            let eased = progress * progress * (3 - 2 * progress)
            return .init(
                color: .black.opacity(1 - eased),
                location: hold + (1 - hold) * CGFloat(progress)
            )
        }
        return [.init(color: .black, location: 0)] + falloff
    }
}
