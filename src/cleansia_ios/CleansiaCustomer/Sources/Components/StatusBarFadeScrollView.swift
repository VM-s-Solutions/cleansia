import CleansiaCore
import SwiftUI

/// A vertical scroll view whose content fades out under the status bar once it scrolls — for the
/// screens that hide the navigation bar (Home, Profile, the Plus offer). Shown only once the content
/// has scrolled, so the full-bleed heroes stay untouched at rest and a pull-to-refresh never raises it.
///
/// - **iOS 26** draws the system's soft scroll edge, the one under Edit schedule's navigation bar. The
///   system draws it under a bar only, and these screens hide theirs, so an empty bar the fade's depth
///   tall stands in. Its near-clear fill is what makes the system draw for it — a clear or zero-height
///   bar draws nothing — and the negative spacing gives its height back, so the content does not move.
/// - **iOS 16–25** lay a material blur over the status bar that fades out below it: the content stays
///   visible, blurred, through it, rather than behind a band of the page colour.
///
/// → /mobile-app/patterns#status-bar-fade
struct StatusBarFadeScrollView<Content: View>: View {
    @ViewBuilder let content: Content
    @State private var isScrolled = false

    var body: some View {
        if #available(iOS 26, *) {
            scroll
                .safeAreaBar(edge: .top, spacing: -StatusBarFade.depth) {
                    CleansiaColors.background.opacity(0.011)
                        .frame(maxWidth: .infinity)
                        .frame(height: StatusBarFade.depth)
                        .allowsHitTesting(false)
                        .accessibilityHidden(true)
                }
                .scrollEdgeEffectStyle(.soft, for: .top)
                .scrollEdgeEffectHidden(!isScrolled, for: .top)
        } else {
            ZStack(alignment: .top) {
                scroll
                fadeBand
                    .opacity(isScrolled ? 1 : 0)
                    .animation(.easeInOut(duration: 0.2), value: isScrolled)
            }
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

    /// Drawn upward from the safe-area line by the status bar's height: solid blur behind the status bar,
    /// fading out `depth` below it. The reader keeps the safe area on purpose: one that ignores it
    /// reports a top inset of 0, which collapses the band to its fade.
    private var fadeBand: some View {
        GeometryReader { proxy in
            let top = proxy.safeAreaInsets.top
            Rectangle()
                .fill(.ultraThinMaterial)
                .mask(
                    LinearGradient(
                        stops: [
                            .init(color: .black, location: 0),
                            .init(color: .black, location: top / (top + StatusBarFade.depth)),
                            .init(color: .black.opacity(0), location: 1)
                        ],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                )
                .frame(height: top + StatusBarFade.depth)
                .offset(y: -top)
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

enum StatusBarFade {
    /// How far below the status bar the fade reaches.
    static let depth: CGFloat = 24
    static let space = "statusBarFade"

    /// The content's top, measured in the scroll view's space, sits at 0 at rest, rises above it as the
    /// content scrolls up, and drops below it during a pull-to-refresh.
    static func isScrolled(contentMinY: CGFloat) -> Bool {
        contentMinY < -1
    }
}
