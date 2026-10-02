import XCTest
@testable import CleansiaCore

final class AnonymousAllowListTests: XCTestCase {
    private let sharedAuthPaths = [
        "/api/Auth/Login",
        "/api/Auth/Register",
        "/api/Auth/RegisterEmployee",
        "/api/Auth/GoogleAuth",
        "/api/Auth/AppleAuth",
        "/api/Auth/ConfirmUserEmail",
        "/api/Auth/ResendConfirmationEmail",
        "/api/Auth/ForgotPassword",
        "/api/Auth/RefreshToken",
        "/api/User/RequestPasswordChange",
        "/api/User/ChangePassword"
    ]

    private let customerPreSessionPaths = [
        "/api/Service/GetOverview",
        "/api/Package/GetOverview",
        "/api/Extra/GetOverview",
        "/api/Currency/GetOverview",
        "/api/Membership/GetPlans",
        "/api/Order/Quote",
        "/api/Referral/Validate"
    ]

    /// Both hosts list the markets anonymously: the partner register form picks one before there
    /// is an account, exactly like the customer's market chip.
    private let marketDirectoryPaths = ["/api/Market/GetOverview"]

    func testBothHostsAllowTheMarketDirectory() {
        for path in marketDirectoryPaths {
            XCTAssertTrue(AnonymousAllowList.partner.isAnonymous(path: path), "partner should allow \(path)")
            XCTAssertTrue(AnonymousAllowList.customer.isAnonymous(path: path), "customer should allow \(path)")
        }
    }

    func testPartnerAllowsSharedAuthPaths() {
        let list = AnonymousAllowList.partner
        for path in sharedAuthPaths {
            XCTAssertTrue(list.isAnonymous(path: path), "partner should allow \(path)")
        }
    }

    func testPartnerDoesNotAllowCustomerPreSessionPaths() {
        let list = AnonymousAllowList.partner
        for path in customerPreSessionPaths {
            XCTAssertFalse(list.isAnonymous(path: path), "partner must NOT treat \(path) as anonymous")
        }
    }

    func testCustomerAllowsSharedAuthPaths() {
        let list = AnonymousAllowList.customer
        for path in sharedAuthPaths {
            XCTAssertTrue(list.isAnonymous(path: path), "customer should allow \(path)")
        }
    }

    func testCustomerAllowsPreSessionPaths() {
        let list = AnonymousAllowList.customer
        for path in customerPreSessionPaths {
            XCTAssertTrue(list.isAnonymous(path: path), "customer should allow pre-session path \(path)")
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

    private let dualUsePaths = ["/api/Order/Quote"]

    func testCustomerClassifiesBookingPathsAsDualUse() {
        let list = AnonymousAllowList.customer
        for path in dualUsePaths {
            XCTAssertTrue(list.isDualUse(path: path), "customer should treat \(path) as dual-use")
        }
    }

    func testDualUsePathsRemainOnTheAnonymousAllowList() {
        let list = AnonymousAllowList.customer
        for path in dualUsePaths {
            XCTAssertTrue(list.isAnonymous(path: path), "dual-use \(path) must stay on the anonymous allow-list")
        }
    }

    func testPureAnonPathsAreNotDualUse() {
        let list = AnonymousAllowList.customer
        for path in sharedAuthPaths {
            XCTAssertFalse(list.isDualUse(path: path), "pure-anon \(path) must never be dual-use")
        }
        XCTAssertFalse(list.isDualUse(path: "/api/Service/GetOverview"))
        XCTAssertFalse(list.isDualUse(path: "/api/Referral/Validate"))
    }

    /// The app has no guest surface: a guest booking is tracked and cancelled from its e-mail link on the
    /// web, so no guest lookup or cancel route is anonymous here, and the account cancel stays authed.
    func testNoGuestOrderPathIsAnonymousAndTheAccountCancelStaysAuthed() {
        let list = AnonymousAllowList.customer
        for path in ["/api/Order/Lookup", "/api/Order/GuestCancellationPreview", "/api/Order/CancelGuest"] {
            XCTAssertFalse(list.isAnonymous(path: path), "\(path) must not be on the anonymous allow-list")
        }
        XCTAssertFalse(list.isAnonymous(path: "/api/Order/Cancel"))
        XCTAssertFalse(list.isAnonymous(path: "/api/Order/CancellationPreview"))
    }

    /// Guest booking is web-only: the customer mobile host takes an order from a signed-in customer and
    /// has no second create route, so a create is an ordinary authed call.
    func testOrderCreationIsNeverAnonymousOrDualUse() {
        let list = AnonymousAllowList.customer
        for path in ["/api/Order/CreateOrder", "/api/Payment/CreateOrder"] {
            XCTAssertFalse(list.isAnonymous(path: path), "\(path) must not be on the guest allow-list")
            XCTAssertFalse(list.isDualUse(path: path), "\(path) must not be dual-use")
        }
    }

    func testPaymentCreateIntentIsNeverAnonymousOrDualUse() {
        let list = AnonymousAllowList.customer
        XCTAssertFalse(list.isAnonymous(path: "/api/Payment/CreatePaymentIntent"))
        XCTAssertFalse(list.isDualUse(path: "/api/Payment/CreatePaymentIntent"))
    }

    func testPartnerHasNoDualUsePaths() {
        let list = AnonymousAllowList.partner
        for path in dualUsePaths {
            XCTAssertFalse(list.isDualUse(path: path), "partner must have no dual-use paths")
        }
    }
}
