import Foundation

public struct AnonymousAllowList: Sendable {
    private let paths: [String]

    public init(paths: [String]) {
        self.paths = paths.map { $0.lowercased() }
    }

    public func isAnonymous(path: String) -> Bool {
        let lower = path.lowercased()
        return paths.contains { lower.contains($0) }
    }

    private static let sharedAuth = [
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/registeremployee",
        "/api/auth/googleauth",
        "/api/auth/confirmuseremail",
        "/api/auth/resendconfirmationemail",
        "/api/auth/forgotpassword",
        "/api/auth/refreshtoken",
        "/api/user/requestpasswordchange",
        "/api/user/changepassword",
    ]

    private static let customerGuestBooking = [
        "/api/service/getoverview",
        "/api/package/getoverview",
        "/api/extra/getoverview",
        "/api/membership/getplans",
        "/api/order/quote",
        "/api/order/createorder",
        "/api/order/lookup",
        "/api/order/lookupbatch",
        "/api/payment/createorder",
        "/api/referral/validate",
    ]

    public static let partner = AnonymousAllowList(paths: sharedAuth)
    public static let customer = AnonymousAllowList(paths: sharedAuth + customerGuestBooking)
}
