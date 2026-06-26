import XCTest
@testable import CleansiaPartner

final class PartnerRootRouteTests: XCTestCase {
    func testVerifiedLoginRoutesToSplash() {
        let route = PartnerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: false, email: nil)
        )
        XCTAssertEqual(route, .splash)
    }

    func testUnverifiedLoginRoutesToVerifyEmailCarryingTheEmail() {
        let route = PartnerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: true, email: "a@b.cz")
        )
        XCTAssertEqual(route, .verifyEmail(email: "a@b.cz"))
    }

    func testUnverifiedLoginWithoutEmailRoutesToVerifyEmailNil() {
        let route = PartnerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: true, email: nil)
        )
        XCTAssertEqual(route, .verifyEmail(email: nil))
    }

    func testSeedIsAlwaysSplash() {
        XCTAssertEqual(PartnerRootView.Route.seed(), .splash)
    }

    func testSplashOutcomeRouting() {
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.authenticated), .dashboard)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.needsRegistrationLock), .registrationLock)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.needsOnboarding), .onboarding)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.unauthenticated), .login)
    }

    func testRegisterIsADistinctTopLevelAudience() {
        XCTAssertNotEqual(PartnerRootView.Route.register, .login)
        XCTAssertNotEqual(PartnerRootView.Route.register, .splash)
        XCTAssertEqual(PartnerRootView.Route.register, .register)
    }

    func testForgotPasswordAndOnboardingAreDistinctTopLevelAudiences() {
        XCTAssertNotEqual(PartnerRootView.Route.forgotPassword, .login)
        XCTAssertNotEqual(PartnerRootView.Route.onboarding, .login)
        XCTAssertNotEqual(PartnerRootView.Route.forgotPassword, .onboarding)
    }
}
