import XCTest
@testable import CleansiaCore

final class AnonymousAllowListTests: XCTestCase {
    private let sharedAuthPaths = [
        "/api/Auth/Login",
        "/api/Auth/Register",
        "/api/Auth/RegisterEmployee",
        "/api/Auth/GoogleAuth",
        "/api/Auth/ConfirmUserEmail",
        "/api/Auth/ResendConfirmationEmail",
        "/api/Auth/ForgotPassword",
        "/api/Auth/RefreshToken",
        "/api/User/RequestPasswordChange",
        "/api/User/ChangePassword"
    ]

    private let customerGuestPaths = [
        "/api/Service/GetOverview",
        "/api/Package/GetOverview",
        "/api/Extra/GetOverview",
        "/api/Membership/GetPlans",
        "/api/Order/Quote",
        "/api/Order/CreateOrder",
        "/api/Order/Lookup",
        "/api/Order/LookupBatch",
        "/api/Payment/CreateOrder",
        "/api/Referral/Validate"
    ]

    func testPartnerAllowsSharedAuthPaths() {
        let list = AnonymousAllowList.partner
        for path in sharedAuthPaths {
            XCTAssertTrue(list.isAnonymous(path: path), "partner should allow \(path)")
        }
    }

    func testPartnerDoesNotAllowGuestBookingPaths() {
        let list = AnonymousAllowList.partner
        for path in customerGuestPaths {
            XCTAssertFalse(list.isAnonymous(path: path), "partner must NOT treat \(path) as anonymous")
        }
    }

    func testCustomerAllowsSharedAuthPaths() {
        let list = AnonymousAllowList.customer
        for path in sharedAuthPaths {
            XCTAssertTrue(list.isAnonymous(path: path), "customer should allow \(path)")
        }
    }

    func testCustomerAllowsGuestBookingPaths() {
        let list = AnonymousAllowList.customer
        for path in customerGuestPaths {
            XCTAssertTrue(list.isAnonymous(path: path), "customer should allow guest path \(path)")
        }
    }

    func testLogoutIsNeverAnonymous() {
        XCTAssertFalse(AnonymousAllowList.partner.isAnonymous(path: "/api/Auth/Logout"))
        XCTAssertFalse(AnonymousAllowList.customer.isAnonymous(path: "/api/Auth/Logout"))
    }

    func testMatchIsCaseInsensitive() {
        XCTAssertTrue(AnonymousAllowList.partner.isAnonymous(path: "/API/AUTH/login"))
        XCTAssertTrue(AnonymousAllowList.customer.isAnonymous(path: "/api/order/quote"))
    }

    func testAuthedBusinessPathIsNotAnonymous() {
        XCTAssertFalse(AnonymousAllowList.partner.isAnonymous(path: "/api/Order/Get"))
        XCTAssertFalse(AnonymousAllowList.customer.isAnonymous(path: "/api/Order/Get"))
    }
}
