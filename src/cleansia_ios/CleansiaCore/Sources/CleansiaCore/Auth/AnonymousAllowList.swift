import Foundation

public struct AnonymousAllowList: Sendable {
    private let paths: [String]
    private let dualUsePaths: [String]

    public init(paths: [String], dualUsePaths: [String] = []) {
        self.paths = paths.map { $0.lowercased() }
        self.dualUsePaths = dualUsePaths.map { $0.lowercased() }
    }

    public func isAnonymous(path: String) -> Bool {
        let lower = path.lowercased()
        return paths.contains { lower.contains($0) }
    }

    public func isDualUse(path: String) -> Bool {
        let lower = path.lowercased()
        return dualUsePaths.contains { lower.contains($0) }
    }

    private static let sharedAuth = [
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/registeremployee",
        "/api/auth/googleauth",
        "/api/auth/appleauth",
        "/api/auth/confirmuseremail",
        "/api/auth/resendconfirmationemail",
        "/api/auth/forgotpassword",
        "/api/auth/refreshtoken",
        "/api/user/requestpasswordchange",
        "/api/user/changepassword"
    ]

    /// The register form reads the market directory before there is a session.
    private static let marketDirectory = [
        "/api/market/getoverview"
    ]

    /// Reads the customer app makes before (or regardless of) a session: the catalogue, the quote, the
    /// plans and the sign-up form's referral check. The app has no guest-booking surface — guest booking
    /// and its lookup live on the web only.
    private static let customerPreSession = [
        "/api/service/getoverview",
        "/api/package/getoverview",
        "/api/extra/getoverview",
        "/api/currency/getoverview",
        "/api/membership/getplans",
        "/api/order/quote",
        "/api/referral/validate"
    ]

    private static let customerDualUse = [
        "/api/order/quote"
    ]

    public static let partner = AnonymousAllowList(paths: sharedAuth + marketDirectory)
    public static let customer = AnonymousAllowList(
        paths: sharedAuth + marketDirectory + customerPreSession,
        dualUsePaths: customerDualUse
    )
}
