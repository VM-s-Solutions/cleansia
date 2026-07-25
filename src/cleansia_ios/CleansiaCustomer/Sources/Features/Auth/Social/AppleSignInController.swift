import CleansiaCore
import Foundation

#if canImport(AuthenticationServices) && canImport(UIKit)
    import AuthenticationServices
    import UIKit

    @MainActor
    final class AppleSignInController: NSObject {
        private var continuation: CheckedContinuation<SocialSignInResult, Never>?
        private var rawNonce = ""

        func signIn() async -> SocialSignInResult {
            await withCheckedContinuation { continuation in
                self.continuation = continuation
                let raw = Nonce.randomRaw()
                self.rawNonce = raw

                let provider = ASAuthorizationAppleIDProvider()
                let request = provider.createRequest()
                request.requestedScopes = [.fullName, .email]
                request.nonce = Nonce.sha256(raw)

                let controller = ASAuthorizationController(authorizationRequests: [request])
                controller.delegate = self
                controller.presentationContextProvider = self
                controller.performRequests()
            }
        }

        private func finish(_ result: SocialSignInResult) {
            let continuation = continuation
            self.continuation = nil
            rawNonce = ""
            continuation?.resume(returning: result)
        }
    }

    extension AppleSignInController: ASAuthorizationControllerDelegate {
        func authorizationController(
            controller _: ASAuthorizationController,
            didCompleteWithAuthorization authorization: ASAuthorization
        ) {
            guard
                let credential = authorization.credential as? ASAuthorizationAppleIDCredential,
                let tokenData = credential.identityToken,
                let identityToken = String(data: tokenData, encoding: .utf8),
                !identityToken.isEmpty
            else {
                finish(.failure)
                return
            }
            let name = credential.fullName
            finish(.apple(SocialSignInResult.AppleCredential(
                identityToken: identityToken,
                rawNonce: rawNonce,
                // Apple hands the name over EXACTLY ONCE, in the first authorization's response — the
                // identity token carries no name claim and no API can fetch it later, so anything dropped
                // here is dropped permanently and the account falls back to an email-derived placeholder.
                // givenName/familyName are what Apple's consent sheet edits and are the normal case;
                // nickname and middleName are the remaining PersonNameComponents fields that can carry a
                // real name when the user cleared the two main ones. Whatever arrives is sent — the
                // backend's SplitSuppliedName handles a single field holding a full name.
                firstName: name?.givenName ?? name?.nickname ?? name?.middleName,
                lastName: name?.familyName
            )))
        }

        func authorizationController(
            controller _: ASAuthorizationController,
            didCompleteWithError error: Error
        ) {
            if let authError = error as? ASAuthorizationError, authError.code == .canceled {
                finish(.cancelled)
            } else {
                finish(.failure)
            }
        }
    }

    extension AppleSignInController: ASAuthorizationControllerPresentationContextProviding {
        func presentationAnchor(for _: ASAuthorizationController) -> ASPresentationAnchor {
            let scene = UIApplication.shared.connectedScenes
                .compactMap { $0 as? UIWindowScene }
                .first { $0.activationState == .foregroundActive }
            let window = scene?.windows.first { $0.isKeyWindow } ?? scene?.windows.first
            return window ?? ASPresentationAnchor()
        }
    }

#else

    @MainActor
    final class AppleSignInController {
        func signIn() async -> SocialSignInResult {
            .notConfigured
        }
    }

#endif
