import Foundation

/// The seam the auth spine calls the instant the server names an account, kept narrow so
/// nothing in `Auth` can reach the parking half.
public protocol SignupConsentDelivering: Sendable {
    /// - Parameter sessionEmail: the address the SERVER named in its token response. Never a
    /// form field: the form is what an unauthenticated caller typed, so matching a parked
    /// tick on it would let one account's session record another account's consent.
    func deliver(sessionEmail: String?) async
}

/// The seam a signup screen parks through.
public protocol SignupConsentRecording: Sendable {
    /// `accepted` carries no default on purpose: a consent flag that can be omitted at a
    /// call site is a consent record nobody agreed to.
    func recordSignupTick(email: String, accepted: Bool) async
}

/// Carries the consent ticked at signup to the first session that can record it.
///
/// Neither app's registration issues a session — both return a bare boolean and route to
/// email confirmation — while `GrantConsent` needs an authenticated caller. So the tick is
/// parked against the address it was ticked for and delivered only once the server itself
/// has named that same account in a token response.
///
/// Every path here is best-effort and silent: a refusal, a 5xx or airplane mode must leave
/// signup and sign-in working. Whatever did not land stays parked and is retried at the next
/// session, so nothing is ever surfaced as a signup error.
public actor SignupConsentRepository: SignupConsentDelivering, SignupConsentRecording {
    private let store: SignupConsentStore
    private let client: SignupConsentClient

    public init(store: SignupConsentStore, client: SignupConsentClient) {
        self.store = store
        self.client = client
    }

    public func recordSignupTick(email: String, accepted: Bool) {
        guard accepted else { return }
        let normalized = Self.normalize(email)
        guard !normalized.isEmpty else { return }
        store.save(PendingSignupConsent(email: normalized, types: SignupConsentType.signupTick))
    }

    public func deliver(sessionEmail: String?) async {
        guard let pending = store.read() else { return }
        guard let sessionEmail, Self.normalize(sessionEmail) == pending.email else { return }
        guard let answered = await client.answeredTypes() else { return }

        for type in pending.types {
            // Dropped rather than re-granted when the account has already answered:
            // otherwise a delayed retry resurrects a consent since withdrawn.
            var delivered = answered.contains(type)
            if !delivered {
                delivered = await client.grant(type) != .failed
            }
            if delivered {
                store.settle(type)
            }
        }
    }

    private static func normalize(_ email: String) -> String {
        email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }
}
