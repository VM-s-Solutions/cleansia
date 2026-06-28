import CleansiaCore
import SwiftUI

struct CustomerRootView: View {
    private let container: CustomerAppContainer
    @EnvironmentObject private var sessionManager: SessionManager
    @State private var route: Route

    init(container: CustomerAppContainer) {
        self.container = container
        _route = State(initialValue: Route.seed())
    }

    var body: some View {
        NavigationStack {
            content
        }
        .task {
            for await _ in sessionManager.forcedSignOutStream {
                route = .login
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch route {
        case .splash:
            SplashGateView(hasValidSession: container.hasValidSession) { outcome in
                route = Route.afterSplash(outcome)
            }
        case .login:
            SignInView(
                makeViewModel: { makeAuthViewModel() },
                onForgotPassword: { route = .forgotPassword },
                onSignUp: { route = .register },
                onOutcome: { route = Route.afterAuth($0) }
            )
        case .register:
            SignUpView(
                makeViewModel: { makeAuthViewModel() },
                onSignIn: { route = .login },
                onOutcome: { route = Route.afterAuth($0) }
            )
        case .forgotPassword:
            ForgotPasswordView(
                makeViewModel: { makeAuthViewModel() },
                onBack: { route = .login },
                onOutcome: { route = Route.afterAuth($0) }
            )
        case let .verifyEmail(email):
            EmailVerifyView(
                makeViewModel: { makeAuthViewModel(pendingEmail: email) },
                onBack: { route = .login },
                onOutcome: { route = Route.afterAuth($0) }
            )
        case .home:
            CustomerShellView(onSignedOut: { route = .login })
        }
    }

    private func makeAuthViewModel(pendingEmail: String? = nil) -> CustomerAuthViewModel {
        CustomerAuthViewModel(
            loginClient: container.loginClient,
            registrationClient: container.registrationAuthClient,
            emailConfirmationClient: container.emailConfirmationClient,
            passwordResetClient: container.passwordResetClient,
            socialAuthClient: container.socialAuthClient,
            socialProvider: container.socialSignInProvider,
            settings: container.appSettings,
            snackbar: container.snackbar,
            pendingEmail: pendingEmail
        )
    }

    enum Route: Equatable {
        case splash
        case login
        case register
        case forgotPassword
        case verifyEmail(email: String?)
        case home

        static func seed() -> Route {
            .splash
        }

        static func afterAuth(_ outcome: AuthOutcome) -> Route {
            switch outcome {
            case .signedIn: .home
            case let .needsEmailConfirm(email): .verifyEmail(email: email)
            case .passwordReset: .login
            }
        }

        static func afterSplash(_ outcome: CustomerSplashOutcome) -> Route {
            switch outcome {
            case .authenticated: .home
            case .unauthenticated: .login
            }
        }
    }
}
