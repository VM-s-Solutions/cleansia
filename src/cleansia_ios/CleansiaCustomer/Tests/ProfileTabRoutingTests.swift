import XCTest
@testable import CleansiaCustomer

final class ProfileTabRoutingTests: XCTestCase {
    func testSubscribeCtaRoutesToSubscribePlusNotEditProfile() {
        XCTAssertEqual(ProfileTab.subscribeRoute, .subscribePlus)
        XCTAssertNotEqual(ProfileTab.subscribeRoute, .editProfile)
    }
}
