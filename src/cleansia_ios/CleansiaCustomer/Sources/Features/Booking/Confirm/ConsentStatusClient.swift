import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The consents currently in force for the signed-in account — granted and not since withdrawn, the
/// web wizard's own predicate. Nil when the read failed; the caller treats that as "ask", never as
/// "none".
protocol ConsentStatusClient {
    func grantedTypes() async -> Set<SignupConsentType>?
}

struct LiveConsentStatusClient: ConsentStatusClient {
    func grantedTypes() async -> Set<SignupConsentType>? {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerGdprAPI.gdprGetMyConsents()
        }
        guard case let .success(consents) = result else { return nil }
        let inForce = consents.filter { $0.isGranted == true && $0.withdrawnAt == nil }
        return Set(inForce.compactMap { $0.consentType.flatMap { SignupConsentType(rawValue: $0.rawValue) } })
    }
}
