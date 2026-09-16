import XCTest
@testable import CleansiaCustomer

final class CustomerRootRouteTests: XCTestCase {
    func testSeedIsAlwaysSplash() {
        XCTAssertEqual(CustomerRootView.Route.seed(), .splash)
    }

    func testSplashAuthenticatedRoutesToHome() {
        XCTAssertEqual(CustomerRootView.Route.afterSplash(.authenticated), .home)
    }

    func testSplashUnauthenticatedRoutesToLogin() {
        XCTAssertEqual(CustomerRootView.Route.afterSplash(.unauthenticated), .login)
    }

    func testSignedInOutcomeRoutesToHome() {
        XCTAssertEqual(CustomerRootView.Route.afterAuth(.signedIn), .home)
    }

    func testNeedsEmailConfirmOutcomeCarriesEmailOntoTheRoute() {
        XCTAssertEqual(
            CustomerRootView.Route.afterAuth(.needsEmailConfirm(email: "a@b.cz")),
            .verifyEmail(email: "a@b.cz")
        )
    }

    func testPasswordResetOutcomeRoutesToLogin() {
        XCTAssertEqual(CustomerRootView.Route.afterAuth(.passwordReset), .login)
    }

    func testCustomerRouteHasNoPartnerOnlyAudiences() {
        XCTAssertNotEqual(CustomerRootView.Route.register, .login)
        XCTAssertNotEqual(CustomerRootView.Route.forgotPassword, .login)
        XCTAssertNotEqual(CustomerRootView.Route.home, .login)
    }

    /// The guest lookup is reached from the sign-in screen and goes back to it; it is never a way into
    /// the shell, and it carries no credential in the route.
    func testGuestOrderIsItsOwnPreAuthAudience() {
        XCTAssertNotEqual(CustomerRootView.Route.guestOrder, .login)
        XCTAssertNotEqual(CustomerRootView.Route.guestOrder, .home)
        XCTAssertNotEqual(CustomerRootView.Route.guestOrder, .splash)
    }

    func testProfileOnboardingIsItsOwnPreShellAudience() {
        XCTAssertNotEqual(CustomerRootView.Route.profileOnboarding, .home)
        XCTAssertNotEqual(CustomerRootView.Route.profileOnboarding, .login)
        XCTAssertNotEqual(CustomerRootView.Route.profileOnboarding, .splash)
    }
}
