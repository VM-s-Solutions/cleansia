import SwiftUI

/// Restores NATIVE swipe-from-the-left-edge to go back, for a stack whose screens hide the nav bar.
///
/// **`isEnabled` was never the closed gate.** `interactivePopGestureRecognizer` ships with UIKit's own
/// delegate, and that delegate answers NO to `gestureRecognizerShouldBegin` when the top screen has no
/// visible back item — which is every screen that hides its bar. `isEnabled` only decides whether the
/// recognizer receives touches at all; UIKit consults it first and the delegate second, so a delegate NO
/// is final and no amount of setting `isEnabled` can outrank it. Two rounds of fixes went into that flag
/// before the gate was identified, which is why it is written down here rather than rediscovered again.
///
/// **What owning the delegate does NOT cost.** Edge scoping is a property of the recognizer — it is a
/// `UIScreenEdgePanGestureRecognizer` bound to the left edge — so the page still cannot be dragged from
/// the middle. The interactive slide is driven by the recognizer's own target/action pair, untouched, so
/// the pop stays native and interruptible.
///
/// **What it DOES cost:** every predicate UIKit's delegate enforced is now ours. The three that matter
/// are restated in `gestureRecognizerShouldBegin` below.
struct InteractivePopGestureEnabler: UIViewControllerRepresentable {
    /// ONE delegate for the whole stack, held statically.
    ///
    /// `UIGestureRecognizer.delegate` is weak, and a nil delegate means "begin unconditionally" — so a
    /// delegate owned by a screen's own controller would go nil when that screen pops and leave an edge
    /// swipe able to start a pop at a tab root with nothing beneath it. That wedges the navigation
    /// controller half-transitioned, and every later push silently does nothing: the app reads as frozen,
    /// in a way that looks nothing like this file. Static, it cannot dangle, which is also what makes it
    /// safe to mount this on every screen that needs it.
    fileprivate static let popDelegate = PopDelegate()

    final class PopDelegate: NSObject, UIGestureRecognizerDelegate {
        weak var navigationController: UINavigationController?

        /// The three questions UIKit's own delegate was answering:
        ///  - is there anything to pop (refusing at the root is what stops the wedge above);
        ///  - is a push or pop already in flight;
        ///  - did this screen deliberately suppress its back item.
        ///
        /// That last one keeps intent that would otherwise be lost. A screen hiding its whole BAR is
        /// making a layout choice and still wants the gesture; a screen setting
        /// `navigationBarBackButtonHidden` is refusing to be left — `MembershipSuccessScreen` does
        /// exactly that on purpose, because it is reached by paying and must not swipe back into the
        /// paywall. Reading the flag separates the two cases without either screen knowing about this.
        func gestureRecognizerShouldBegin(_: UIGestureRecognizer) -> Bool {
            guard let nav = navigationController else { return false }
            return nav.viewControllers.count > 1
                && nav.transitionCoordinator == nil
                && nav.topViewController?.navigationItem.hidesBackButton != true
        }
    }

    func makeUIViewController(context _: Context) -> GestureController {
        GestureController()
    }

    func updateUIViewController(_ controller: GestureController, context _: Context) {
        controller.install()
    }

    final class GestureController: UIViewController {
        override func didMove(toParent parent: UIViewController?) {
            super.didMove(toParent: parent)
            install()
        }

        /// The SwiftUI update pass runs BEFORE the pushed controller's appearance transition, and the
        /// bar is hidden inside that transition. Re-asserting once the screen is actually up covers the
        /// window in between, which is the shape the earlier rounds kept dying in.
        override func viewDidAppear(_ animated: Bool) {
            super.viewDidAppear(animated)
            install()
        }

        func install() {
            guard let nav = navigationController,
                  let gesture = nav.interactivePopGestureRecognizer else { return }
            gesture.isEnabled = true
            InteractivePopGestureEnabler.popDelegate.navigationController = nav
            gesture.delegate = InteractivePopGestureEnabler.popDelegate
        }
    }
}
