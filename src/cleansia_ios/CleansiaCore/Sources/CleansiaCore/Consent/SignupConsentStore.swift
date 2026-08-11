import Foundation

public protocol SignupConsentStore: AnyObject, Sendable {
    func save(_ pending: PendingSignupConsent)
    func read() -> PendingSignupConsent?
    func settle(_ type: SignupConsentType)
}

/// Persists the tick across the process death that can sit between signup and the first
/// sign-in — the user reads the confirmation mail on another device and comes back a day
/// later. Deliberately outside the session-wipe registry: the tick belongs to the account,
/// not the session, and the address it is keyed on is what stops it reaching another one.
public final class UserDefaultsSignupConsentStore: SignupConsentStore, @unchecked Sendable {
    private enum Key {
        static let email = "consent.pending_email"
        static let types = "consent.pending_types"
    }

    private let defaults: UserDefaults

    public init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    public func save(_ pending: PendingSignupConsent) {
        defaults.set(pending.email, forKey: Key.email)
        defaults.set(pending.types.map(\.rawValue), forKey: Key.types)
    }

    public func read() -> PendingSignupConsent? {
        guard let email = defaults.string(forKey: Key.email), !email.isEmpty else { return nil }
        let types = storedTypes()
        return types.isEmpty ? nil : PendingSignupConsent(email: email, types: types)
    }

    public func settle(_ type: SignupConsentType) {
        let remaining = storedTypes().filter { $0 != type }
        if remaining.isEmpty {
            defaults.removeObject(forKey: Key.email)
            defaults.removeObject(forKey: Key.types)
        } else {
            defaults.set(remaining.map(\.rawValue), forKey: Key.types)
        }
    }

    private func storedTypes() -> [SignupConsentType] {
        let raw = defaults.array(forKey: Key.types) as? [Int] ?? []
        return raw.compactMap(SignupConsentType.init(rawValue:))
    }
}
