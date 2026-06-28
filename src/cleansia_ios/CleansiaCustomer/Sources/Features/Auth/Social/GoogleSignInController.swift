import CleansiaCore
import Foundation

#if canImport(GoogleSignIn) && canImport(UIKit)
    import GoogleSignIn
    import UIKit

    @MainActor
    final class GoogleSignInController {
        private let clientID: String
        private let serverClientID: String

        init(clientID: String, serverClientID: String) {
            self.clientID = clientID
            self.serverClientID = serverClientID
        }

        func signIn() async -> SocialSignInResult {
            guard !clientID.isEmpty, !serverClientID.isEmpty else {
                return .notConfigured
            }
            guard let presenter = Self.topViewController() else {
                return .failure
            }

            GIDSignIn.sharedInstance.configuration = GIDConfiguration(
                clientID: clientID,
                serverClientID: serverClientID
            )

            do {
                let result = try await GIDSignIn.sharedInstance.signIn(withPresenting: presenter)
                let user = result.user
                guard let idToken = user.idToken?.tokenString else {
                    return .failure
                }
                return .google(SocialSignInResult.GoogleCredential(
                    idToken: idToken,
                    googleId: user.userID ?? "",
                    email: user.profile?.email ?? "",
                    firstName: user.profile?.givenName ?? "",
                    lastName: user.profile?.familyName ?? ""
                ))
            } catch let error as GIDSignInError where error.code == .canceled {
                return .cancelled
            } catch {
                return .failure
            }
        }

        private static func topViewController() -> UIViewController? {
            let scene = UIApplication.shared.connectedScenes
                .compactMap { $0 as? UIWindowScene }
                .first { $0.activationState == .foregroundActive }
            var top = scene?.windows.first { $0.isKeyWindow }?.rootViewController
            while let presented = top?.presentedViewController {
                top = presented
            }
            return top
        }
    }

#else

    @MainActor
    final class GoogleSignInController {
        init(clientID _: String, serverClientID _: String) {}

        func signIn() async -> SocialSignInResult {
            .notConfigured
        }
    }

#endif
