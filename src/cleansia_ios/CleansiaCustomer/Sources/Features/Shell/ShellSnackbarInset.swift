import CleansiaCore
import Foundation

/// The `SnackbarInsetScope(88.dp)` parity (`MainShell.kt:244-246`): while the
/// shell root is visible, snackbars must clear the pill+FAB composite — 88pt
/// of layout (64 pill + 12+12 padding; the 74pt FAB rides up to the composite
/// top) + a 12pt visible gap. Pushed children hide the bar, so the default
/// inset applies again.
enum ShellSnackbarInset {
    static let overShellBar: CGFloat = 100

    static func inset(pathDepth: Int) -> CGFloat {
        pathDepth == 0 ? overShellBar : SnackbarController.defaultBottomInset
    }
}
