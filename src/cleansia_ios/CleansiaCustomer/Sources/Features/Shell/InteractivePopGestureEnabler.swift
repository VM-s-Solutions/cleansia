import SwiftUI

/// **Applied on the ROOT and on every pushed screen that hides its own bar.** Re-enabling once at the
/// root is not enough: SwiftUI disables the recognizer again when a pushed screen hides its toolbar, and
/// the root's representable does not update at that moment, so the screen arrives with the gesture off.
/// Each such screen therefore re-asserts it for itself.
///
/// Re-enables NATIVE swipe-to-go-back on pushed screens. SwiftUI leaves the navigation controller's
/// `interactivePopGestureRecognizer` disabled stack-wide once the shell root hides its nav bar
/// (`.toolbar(.hidden, for: .navigationBar)`), even on pushed screens that DO show a nav bar + native
/// back button (e.g. the order detail). We ONLY flip `isEnabled` back on. We deliberately do NOT replace
/// the recognizer's delegate: its native delegate is what enforces the left-edge scoping AND drives the
/// interactive slide-to-back. Leaving it intact keeps swipe-back edge-only (the page cannot be dragged
/// from the middle) and fully native. Screens that use a custom hidden-bar header keep their tap-back.
struct InteractivePopGestureEnabler: UIViewControllerRepresentable {
    func makeUIViewController(context _: Context) -> GestureController {
        GestureController()
    }

    /// Re-assert on every update too: if this attaches before the ancestor UINavigationController joins
    /// the parent chain, `navigationController` is nil at `didMove` and the gesture would stay disabled.
    func updateUIViewController(_ controller: GestureController, context _: Context) {
        controller.reenablePopGesture()
    }

    final class GestureController: UIViewController {
        override func didMove(toParent parent: UIViewController?) {
            super.didMove(toParent: parent)
            reenablePopGesture()
        }

        func reenablePopGesture() {
            navigationController?.interactivePopGestureRecognizer?.isEnabled = true
        }
    }
}
