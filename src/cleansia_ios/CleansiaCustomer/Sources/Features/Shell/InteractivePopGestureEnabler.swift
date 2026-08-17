import SwiftUI

/// Restores NATIVE swipe-from-the-left-edge to go back, for a stack whose screens hide the nav bar.
///
/// **`isEnabled` was never the closed gate.** `interactivePopGestureRecognizer` ships with UIKit's own
/// delegate (`_UINavigationInteractiveTransition`), and that delegate refuses to let the gesture BEGIN
/// while the navigation bar is hidden — which this shell does at the root and on several pushed screens.
/// So flipping `isEnabled` back on, which is all this type used to do, changed a flag nothing was
/// reading: the delegate said no immediately afterwards. Two rounds of fixes went into that flag before
/// the gate was identified, so it is written down here rather than rediscovered a third time.
///
/// The fix is to own the delegate and answer the one question it is asked: is there something to pop.
///
/// **What replacing the delegate does NOT cost.** Edge scoping is a property of the recognizer, not the
/// delegate — it is a `UIScreenEdgePanGestureRecognizer` bound to the left edge, so the page still
/// cannot be dragged from the middle. The interactive slide is driven by the recognizer's own
/// target/action pair, which is untouched, so the pop stays fully native and interruptible.
///
/// **Install it at the ROOT only.** The recognizer holds its delegate weakly, so a copy mounted on a
/// pushed screen would nil the delegate out again the moment that screen is popped and its controller
/// deallocates. The root's controller lives as long as the stack does, which is exactly the lifetime
/// this needs.
struct InteractivePopGestureEnabler: UIViewControllerRepresentable {
    func makeUIViewController(context _: Context) -> GestureController {
        GestureController()
    }

    /// Re-assert on every update too: if this attaches before the ancestor `UINavigationController`
    /// joins the parent chain, `navigationController` is nil at `didMove` and nothing would be wired.
    func updateUIViewController(_ controller: GestureController, context _: Context) {
        controller.install()
    }

    final class GestureController: UIViewController, UIGestureRecognizerDelegate {
        override func didMove(toParent parent: UIViewController?) {
            super.didMove(toParent: parent)
            install()
        }

        func install() {
            guard let gesture = navigationController?.interactivePopGestureRecognizer else { return }
            gesture.isEnabled = true
            gesture.delegate = self
        }

        /// The whole contract: a pop is available when something is stacked on the root. Refusing at one
        /// screen is what stops an edge swipe from unwinding past the bottom of the stack, which UIKit's
        /// own delegate would otherwise have handled.
        func gestureRecognizerShouldBegin(_: UIGestureRecognizer) -> Bool {
            (navigationController?.viewControllers.count ?? 0) > 1
        }

        /// The edge pan owns the touch outright. Letting a scroll view claim it simultaneously is how the
        /// gesture ends up half-recognised — the page follows the finger and then snaps back.
        func gestureRecognizer(
            _: UIGestureRecognizer,
            shouldRecognizeSimultaneouslyWith _: UIGestureRecognizer
        ) -> Bool {
            false
        }
    }
}
