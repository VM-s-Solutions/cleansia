import Foundation

/// The GDPR consent kinds the backend records, by their on-the-wire integer
/// (`Cleansia.Core.Domain.Enums.ConsentType`). Each app maps this onto its own
/// OpenAPI-generated enum, whose cases are named `_0`…`_3`.
public enum SignupConsentType: Int, CaseIterable, Sendable {
    case termsOfService = 0
    case privacyPolicy = 1
    case marketingEmails = 2
    case dataProcessing = 3
}

public extension SignupConsentType {
    /// One tick, two records: both signup sentences name the Terms of Service and the
    /// Privacy Policy by title. Neither form offers a marketing box, so `marketingEmails`
    /// must never appear here — a record nobody ticked is worse than no record at all.
    static let signupTick: [SignupConsentType] = [.termsOfService, .privacyPolicy]
}

/// A signup tick waiting for a session it can be recorded against.
public struct PendingSignupConsent: Equatable, Sendable {
    public let email: String
    public let types: [SignupConsentType]

    public init(email: String, types: [SignupConsentType]) {
        self.email = email
        self.types = types
    }
}
