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

    /// Right after signing up, the password just typed rides to the confirm step, which sends it instead of
    /// asking for one again; every other way onto that step carries none.
    func testTheSignUpPasswordRidesOntoTheConfirmStep() {
        XCTAssertEqual(
            CustomerRootView.Route.afterAuth(.needsEmailConfirm(email: "a@b.cz", password: "abcdefg1")),
            .verifyEmail(email: "a@b.cz", password: "abcdefg1")
        )
        XCTAssertEqual(
            CustomerRootView.Route.afterAuth(.needsEmailConfirm(email: "a@b.cz")),
            .verifyEmail(email: "a@b.cz", password: nil)
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

    func testProfileOnboardingIsItsOwnPreShellAudience() {
        XCTAssertNotEqual(CustomerRootView.Route.profileOnboarding, .home)
        XCTAssertNotEqual(CustomerRootView.Route.profileOnboarding, .login)
        XCTAssertNotEqual(CustomerRootView.Route.profileOnboarding, .splash)
    }
}
