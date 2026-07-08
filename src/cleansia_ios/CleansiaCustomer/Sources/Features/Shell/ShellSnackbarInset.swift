import CleansiaCore
import Foundation

/// Lifts a tab-root snackbar above the bottom chrome — the stock system tab bar
/// plus the center-docked Book FAB that straddles its top edge (ADR-0022
/// supersede, 2026-07-08: the native `TabView` replaces the pill composite). The
/// FAB's top edge is the tallest point, so the inset clears it. The stock bar
/// self-insets scroll content, but the global snackbar host sits at the app root
/// outside the tab bar, so it is lifted explicitly. Measured from the safe-area
/// bottom (the host respects the safe area), so one constant holds across
/// devices — the
/// Android `SnackbarInsetScope` clearing the whole bar+FAB composite. Pushed
/// children cover the chrome, so the default inset applies again.
enum ShellSnackbarInset {
    static let snackbarGap: CGFloat = 12
    static let overShellBar: CGFloat = BookFabMetrics.chromeEnvelope + snackbarGap

    static func inset(pathDepth: Int) -> CGFloat {
        pathDepth == 0 ? overShellBar : SnackbarController.defaultBottomInset
    }
}
