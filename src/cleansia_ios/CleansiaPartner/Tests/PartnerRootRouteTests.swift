import XCTest
@testable import CleansiaPartner

final class PartnerRootRouteTests: XCTestCase {
    func testVerifiedLoginRoutesToDashboard() {
        let route = PartnerRootView.Route.afterLogin(LoginSuccess(requiresEmailConfirmation: false))
        XCTAssertEqual(route, .dashboard)
    }

    func testUnverifiedLoginRoutesToVerifyEmail() {
        let route = PartnerRootView.Route.afterLogin(LoginSuccess(requiresEmailConfirmation: true))
        XCTAssertEqual(route, .verifyEmail)
    }
}
