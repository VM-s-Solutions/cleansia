import CleansiaCore
import SwiftUI

/// Bottom-chrome geometry for the tab roots, measured from the safe-area bottom
/// so one set of constants holds on both the iPhone 17 (26.x, 34pt indicator)
/// and iPhone 14 (16.4) runtimes without a per-device branch. The stock system
/// tab bar is ~49pt; the Book FAB floats a fixed gap above it (bottom-trailing,
/// so it can never overlap a tab item).
enum BookFabMetrics {
    static let systemTabBarHeight: CGFloat = 49
    static let size: CGFloat = 56
    static let gapAboveBar: CGFloat = 12
    static let trailingMargin: CGFloat = 16

    /// The FAB's bottom edge above the safe-area bottom — clear of the tab bar.
    static var bottomPadding: CGFloat {
        systemTabBarHeight + gapAboveBar
    }

    /// The FAB's top edge — the tallest point of the tab-root bottom chrome.
    static var chromeEnvelope: CGFloat {
        bottomPadding + size
    }
}

/// The floating Book action — the surviving piece of the retired pill composite
/// (ADR-0022 supersede, 2026-07-08: the native `TabView` replaces the custom
/// pill/FAB bar). A solid primary disc; the iOS 26 Liquid Glass FAB rendered
/// corrupted on device, so no glass branch.
struct BookFab: View {
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: "bubbles.and.sparkles")
                .font(.system(size: 24, weight: .semibold))
                .foregroundColor(CleansiaColors.onPrimary)
                .frame(width: BookFabMetrics.size, height: BookFabMetrics.size)
                .background(Circle().fill(CleansiaColors.primary))
                .shadow(color: .black.opacity(0.18), radius: 8, y: 3)
        }
        .accessibilityLabel(Text(verbatim: L10n.Shell.book))
    }
}

#if DEBUG
    struct BookFab_Previews: PreviewProvider {
        static var previews: some View {
            BookFab(action: {})
                .padding()
                .background(CleansiaColors.background)
        }
    }
#endif
