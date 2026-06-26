import XCTest
@testable import CleansiaPartner

final class PartnerRootRouteTests: XCTestCase {
    func testVerifiedLoginRoutesToSplash() {
        let route = PartnerRootView.Route.afterLogin(LoginSuccess(requiresEmailConfirmation: false))
        XCTAssertEqual(route, .splash)
    }

    func testUnverifiedLoginRoutesToVerifyEmail() {
        let route = PartnerRootView.Route.afterLogin(LoginSuccess(requiresEmailConfirmation: true))
        XCTAssertEqual(route, .verifyEmail)
    }

    func testSeedWithValidSessionIsSplash() {
        XCTAssertEqual(PartnerRootView.Route.seed(hasValidSession: true), .splash)
    }

    func testSeedWithoutValidSessionIsLogin() {
        XCTAssertEqual(PartnerRootView.Route.seed(hasValidSession: false), .login)
    }

    func testSplashOutcomeRouting() {
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.authenticated), .dashboard)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.needsRegistrationLock), .registrationLock)
        XCTAssertEqual(PartnerRootView.Route.afterSplash(.unauthenticated), .login)
    }
}
