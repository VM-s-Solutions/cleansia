import CleansiaCore
import Foundation

@MainActor
final class CustomerSocialSignInProvider: SocialSignInProviding {
    private let google: GoogleSignInController
    private let apple: AppleSignInController

    init(googleClientID: String, googleServerClientID: String) {
        google = GoogleSignInController(clientID: googleClientID, serverClientID: googleServerClientID)
        apple = AppleSignInController()
    }

    func signInWithGoogle() async -> SocialSignInResult {
        await google.signIn()
    }

    func signInWithApple() async -> SocialSignInResult {
        await apple.signIn()
    }
}
