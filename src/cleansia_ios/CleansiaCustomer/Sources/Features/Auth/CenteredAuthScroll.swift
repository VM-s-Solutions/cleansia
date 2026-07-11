import SwiftUI

/// Scroll container that centers its content vertically when it fits the
/// viewport and scrolls when it does not — keeps the auth forms centered
/// instead of top-anchored, while staying keyboard-safe.
struct CenteredAuthScroll<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        GeometryReader { proxy in
            ScrollView {
                content
                    .frame(maxWidth: .infinity, minHeight: proxy.size.height)
            }
        }
    }
}
