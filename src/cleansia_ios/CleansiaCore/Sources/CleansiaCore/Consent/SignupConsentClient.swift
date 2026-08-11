import Foundation

/// Per-app binding seam for the GDPR consent endpoints, mirroring `DeviceRegistrationClient`:
/// each app implements it over its own generated Gdpr API so the parking and delivery rules
/// live once, in `SignupConsentRepository`.
public protocol SignupConsentClient: Sendable {
    /// Every consent type the account has already answered — granted **or withdrawn**.
    /// `nil` when the read itself failed, which is not the same as "answered nothing".
    func answeredTypes() async -> Set<SignupConsentType>?

    func grant(_ type: SignupConsentType) async -> ConsentGrantOutcome
}

public enum ConsentGrantOutcome: Equatable, Sendable {
    case recorded

    /// The backend refused a duplicate. The record it refuses to duplicate is the one we came to write.
    case alreadyOnFile

    case failed
}

public extension Result where Failure == ApiError {
    var consentGrantOutcome: ConsentGrantOutcome {
        switch self {
        case .success:
            .recorded
        case let .failure(error):
            error.namesConsentAlreadyGranted ? .alreadyOnFile : .failed
        }
    }
}

private extension ApiError {
    /// Read off the first VALUE in the ProblemDetails `errors` bag, which is what
    /// `ApiError.fromProblemDetails` prefers. `CleansiaApiController.CreateProblemDetails`
    /// keys that bag by `Error.Code` (the offending field, `ConsentType`) and values it with
    /// `Error.Message` (the business key). Matching the bag key or `type` instead would be
    /// matching the field name.
    var namesConsentAlreadyGranted: Bool {
        httpStatus == 400 && code == "gdpr.consent_already_granted"
    }
}
