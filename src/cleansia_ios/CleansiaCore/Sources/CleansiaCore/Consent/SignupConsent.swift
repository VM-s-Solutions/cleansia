import Foundation

/// The GDPR consent kinds the backend records, by their on-the-wire integer
/// (`Cleansia.Core.Domain.Enums.ConsentType`). The customer app maps this onto its
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
