import UIKit
import XCTest
@testable import CleansiaCore

/// The edge swipe's delegate answers the questions UIKit's own answered before it was replaced: nothing to pop,
/// a screen that refuses to be left, and a pushed screen that may go back.
@MainActor
final class InteractivePopGestureEnablerTests: XCTestCase {
    func testTheSwipeRefusesAtTheRootOfTheStack() {
        XCTAssertFalse(shouldBegin(on: [UIViewController()]))
    }

    func testTheSwipeRefusesOnAScreenThatHidesItsBackButton() {
        let top = UIViewController()
        top.navigationItem.hidesBackButton = true

        XCTAssertFalse(shouldBegin(on: [UIViewController(), top]))
    }

    func testTheSwipeBeginsOnAPushedScreen() {
        XCTAssertTrue(shouldBegin(on: [UIViewController(), UIViewController()]))
    }

    func testTheSwipeRefusesWithNoStack() {
        XCTAssertFalse(InteractivePopGestureEnabler.PopDelegate().gestureRecognizerShouldBegin(UIGestureRecognizer()))
    }

    private func shouldBegin(on screens: [UIViewController]) -> Bool {
        let stack = UINavigationController()
        stack.setViewControllers(screens, animated: false)
        let delegate = InteractivePopGestureEnabler.PopDelegate()
        return withExtendedLifetime(stack) {
            delegate.navigationController = stack
            return delegate.gestureRecognizerShouldBegin(UIGestureRecognizer())
        }
    }
}
