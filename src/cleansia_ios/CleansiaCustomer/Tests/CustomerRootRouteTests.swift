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

    func testVerifiedLoginRoutesToSplash() {
        let route = CustomerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: false, email: nil)
        )
        XCTAssertEqual(route, .splash)
    }

    func testUnverifiedLoginRoutesToVerifyEmailCarryingTheEmail() {
        let route = CustomerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: true, email: "a@b.cz")
        )
        XCTAssertEqual(route, .verifyEmail(email: "a@b.cz"))
    }

    func testCustomerRouteHasNoPartnerOnlyAudiences() {
        XCTAssertNotEqual(CustomerRootView.Route.register, .login)
        XCTAssertNotEqual(CustomerRootView.Route.forgotPassword, .login)
        XCTAssertNotEqual(CustomerRootView.Route.home, .login)
    }
}
