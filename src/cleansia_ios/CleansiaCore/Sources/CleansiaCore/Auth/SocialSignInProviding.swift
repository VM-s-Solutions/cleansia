import Foundation

public enum SocialSignInResult: Equatable, Sendable {
    case google(GoogleCredential)
    case apple(AppleCredential)
    case cancelled
    case noAccount
    case notConfigured
    case failure

    public struct GoogleCredential: Equatable, Sendable {
        public let idToken: String
        public let googleId: String
        public let email: String
        public let firstName: String
        public let lastName: String

        public init(idToken: String, googleId: String, email: String, firstName: String, lastName: String) {
            self.idToken = idToken
            self.googleId = googleId
            self.email = email
            self.firstName = firstName
            self.lastName = lastName
        }
    }

    public struct AppleCredential: Equatable, Sendable {
        public let identityToken: String
        public let rawNonce: String
        public let firstName: String?
        public let lastName: String?

        public init(identityToken: String, rawNonce: String, firstName: String?, lastName: String?) {
            self.identityToken = identityToken
            self.rawNonce = rawNonce
            self.firstName = firstName
            self.lastName = lastName
        }
    }
}

@MainActor
public protocol SocialSignInProviding: AnyObject {
    func signInWithGoogle() async -> SocialSignInResult
    func signInWithApple() async -> SocialSignInResult
}
