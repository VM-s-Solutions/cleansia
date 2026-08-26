import XCTest
@testable import CleansiaPartner

final class PartnerRootRouteTests: XCTestCase {
    /// The gate still runs — ADR-0020 D3 — but WITHOUT the brand reveal. Replaying the ~1.8s reveal
    /// on someone who has just typed their password is what reads as the app restarting itself.
    func testVerifiedLoginRoutesToSplashWithoutTheBrandReveal() {
        let route = PartnerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: false, email: nil)
        )
        XCTAssertEqual(route, .splash(showsBrandReveal: false))
    }

    /// The distinction is the whole change: a cold start and a post-login bounce are both `.splash`,
    /// and only one of them should play the reveal.
    func testColdStartAndPostLoginSplashAreNotTheSameRoute() {
        XCTAssertNotEqual(
            PartnerRootView.Route.seed(),
            PartnerRootView.Route.afterLogin(LoginSuccess(requiresEmailConfirmation: false, email: nil))
        )
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

    func testSeedIsAlwaysSplashWithTheBrandReveal() {
        XCTAssertEqual(PartnerRootView.Route.seed(), .splash(showsBrandReveal: true))
    }

    func testSplashOutcomeRouting() {
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.authenticated), .dashboard)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.needsRegistrationLock), .registrationLock)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.needsOnboarding), .onboarding)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.unauthenticated), .login)
        // Stays on the splash. SplashGateView does not call afterSplash for this outcome at all;
        // the mapping exists so the function is total, and this pins that it never acquires a
        // destination by accident.
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.unreachable), .splash(showsBrandReveal: false))
    }

    func testRegisterIsADistinctTopLevelAudience() {
        XCTAssertNotEqual(PartnerRootView.Route.register, .login)
        XCTAssertNotEqual(PartnerRootView.Route.register, .splash(showsBrandReveal: true))
        XCTAssertEqual(PartnerRootView.Route.register, .register)
    }

    func testForgotPasswordAndOnboardingAreDistinctTopLevelAudiences() {
        XCTAssertNotEqual(PartnerRootView.Route.forgotPassword, .login)
        XCTAssertNotEqual(PartnerRootView.Route.onboarding, .login)
        XCTAssertNotEqual(PartnerRootView.Route.forgotPassword, .onboarding)
    }
}
