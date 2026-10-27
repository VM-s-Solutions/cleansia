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

    /// Signing in to an unconfirmed account is confirming later: no password rides the route, so the confirm
    /// screen asks for the one the confirmation sets.
    func testUnverifiedLoginCarriesNoPasswordOntoTheConfirmStep() {
        let route = PartnerRootView.Route.afterLogin(
            LoginSuccess(requiresEmailConfirmation: true, email: "a@b.cz")
        )
        XCTAssertEqual(route, .verifyEmail(email: "a@b.cz", password: nil))
    }

    /// Right after registering, the password just typed rides to the confirm step, which sends it instead of
    /// asking again. A router that dropped it would still compile.
    func testTheRegistrationHandsItsPasswordToTheConfirmStep() throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: root.appendingPathComponent("CleansiaPartner/Sources/PartnerRootView.swift"),
            encoding: .utf8
        )
        XCTAssertTrue(source
            .contains("onRegistered: { email, password in route = .verifyEmail(email: email, password: password) }"))
        XCTAssertTrue(source.contains("registeredPassword: password,"))
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
