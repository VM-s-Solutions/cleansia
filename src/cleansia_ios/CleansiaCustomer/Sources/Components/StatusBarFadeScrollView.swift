import CleansiaCore
import SwiftUI

/// A vertical scroll view whose content fades out under the status bar once it scrolls — for the
/// screens that hide the navigation bar (Home, Profile, the Plus offer). A band of the screen
/// background, opaque behind the status bar and fading out just below it, shown only once the content
/// has scrolled, so the full-bleed heroes stay untouched at rest and a pull-to-refresh never raises it.
/// One band on every iOS version: iOS 26's native soft scroll edge draws nothing with the bar hidden.
/// → /mobile-app/patterns#status-bar-fade
struct StatusBarFadeScrollView<Content: View>: View {
    @ViewBuilder let content: Content
    @State private var isScrolled = false

    var body: some View {
        ZStack(alignment: .top) {
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
            fadeBand
                .opacity(isScrolled ? 1 : 0)
                .animation(.easeInOut(duration: 0.2), value: isScrolled)
        }
        .coordinateSpace(name: StatusBarFade.space)
    }

    /// Drawn upward from the safe-area line by the status bar's height. The reader keeps the safe area
    /// on purpose: one that ignores it reports a top inset of 0, which collapses the band to its fade.
    private var fadeBand: some View {
        GeometryReader { proxy in
            let top = proxy.safeAreaInsets.top
            LinearGradient(
                stops: [
                    .init(color: CleansiaColors.background, location: 0),
                    .init(color: CleansiaColors.background, location: top / (top + StatusBarFade.depth)),
                    .init(color: CleansiaColors.background.opacity(0), location: 1)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .frame(height: top + StatusBarFade.depth)
            .offset(y: -top)
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

enum StatusBarFade {
    /// How far below the status bar the band fades out.
    static let depth: CGFloat = 24
    static let space = "statusBarFade"

    /// The content's top, measured in the space of the scroll view's parent, sits at 0 at rest, rises
    /// above it as the content scrolls up, and drops below it during a pull-to-refresh.
    static func isScrolled(contentMinY: CGFloat) -> Bool {
        contentMinY < -1
    }
}
