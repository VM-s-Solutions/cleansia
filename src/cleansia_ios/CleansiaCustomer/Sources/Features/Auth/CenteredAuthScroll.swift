import SwiftUI

/// Scroll container that centers its content vertically when it fits the
/// viewport and scrolls when it does not — keeps the auth forms centered
/// instead of top-anchored, while staying keyboard-safe. A form that fits
/// neither rubber-bands nor shows a scroll indicator (iOS 16.4+); the scroll
/// view stays for the keyboard, field errors and large Dynamic Type.
struct CenteredAuthScroll<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        GeometryReader { proxy in
            let scroll = ScrollView {
                content
                    .frame(maxWidth: .infinity, minHeight: proxy.size.height)
            }
            if #available(iOS 16.4, *) {
                scroll.scrollBounceBehavior(.basedOnSize)
            } else {
                scroll
            }
        }
    }
}
