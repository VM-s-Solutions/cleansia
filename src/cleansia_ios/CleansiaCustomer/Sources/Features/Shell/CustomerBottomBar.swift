import CleansiaCore
import SwiftUI

/// Bottom-chrome geometry for the tab roots, measured from the safe-area bottom
/// so one set of constants holds on both the iPhone 17 (26.x, 34pt indicator)
/// and iPhone 14 (16.4) runtimes without a per-device branch. The stock system
/// tab bar is ~49pt; the Book FAB is horizontally centered over the dead gap
/// between tabs 2 and 3 and center-docked onto the bar's top edge — half the
/// disc rises above the bar, half overlaps it — so it never covers a tab icon.
enum BookFabMetrics {
    static let systemTabBarHeight: CGFloat = 49
    static let size: CGFloat = 56

    /// The FAB's bottom edge above the safe-area bottom. Docked so the disc's
    /// vertical center lands on the tab bar's top edge (a center-docked FAB).
    static var bottomPadding: CGFloat {
        systemTabBarHeight - size / 2
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
