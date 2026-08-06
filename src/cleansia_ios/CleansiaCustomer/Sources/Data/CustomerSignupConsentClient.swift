import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct CustomerSignupConsentClient: SignupConsentClient {
    func answeredTypes() async -> Set<SignupConsentType>? {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerGdprAPI.gdprGetMyConsents()
        }
        // Deliberately not filtered by `isGranted`: a withdrawn row is an answer, and
        // re-granting it would resurrect a consent the user has since taken back.
        switch result {
        case let .success(consents):
            return Set(consents.compactMap { $0.consentType.flatMap { SignupConsentType(rawValue: $0.rawValue) } })
        case .failure:
            return nil
        }
    }

    func grant(_ type: SignupConsentType) async -> ConsentGrantOutcome {
        guard let wireType = ConsentType(rawValue: type.rawValue) else { return .failed }
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerGdprAPI.gdprGrantConsent(
                grantConsentCommand: GrantConsentCommand(consentType: wireType)
            )
        }
        return result.consentGrantOutcome
    }
}
