import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The consents currently in force for the signed-in account — granted, not since withdrawn, and given to
/// the version of the text now in force, the web wizard's own predicate. Nil when the read failed; the
/// caller treats that as "ask", never as "none".
protocol ConsentStatusClient {
    func grantedTypes() async -> Set<SignupConsentType>?
}

extension ConsentStatusClient {
    /// Whether the account already holds both documents the terms tick names, which is what hides the tick at
    /// the booking review and at a recurring visit's confirm alike. A failed read asks.
    func holdsTermsTickConsents() async -> Bool {
        await grantedTypes()?.isSuperset(of: SignupConsentType.signupTick) == true
    }
}

struct LiveConsentStatusClient: ConsentStatusClient {
    func grantedTypes() async -> Set<SignupConsentType>? {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerGdprAPI.gdprGetMyConsents()
        }
        guard case let .success(consents) = result else { return nil }
        let inForce = consents.filter {
            $0.isGranted == true && $0.withdrawnAt == nil && $0.coversCurrentVersion == true
        }
        return Set(inForce.compactMap { $0.consentType.flatMap { SignupConsentType(rawValue: $0.rawValue) } })
    }
}
