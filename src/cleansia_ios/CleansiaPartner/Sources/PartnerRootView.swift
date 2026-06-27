import CleansiaCore
import SwiftUI

struct PartnerRootView: View {
    private let container: PartnerAppContainer
    @EnvironmentObject private var sessionManager: SessionManager
    @State private var route: Route

    init(container: PartnerAppContainer) {
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
        case .login:
            LoginView(
                loginClient: container.loginClient,
                snackbar: container.snackbar,
                onForgotPassword: { route = .forgotPassword },
                onSignUp: { route = .register },
                onLoginSuccess: { success in route = Route.afterLogin(success) }
            )
        case .register:
            RegisterView(
                client: container.registrationAuthClient,
                settings: container.appSettings,
                snackbar: container.snackbar,
                onSignIn: { route = .login },
                onRegistered: { route = .login }
            )
        case .forgotPassword:
            ForgotPasswordView(
                client: container.passwordResetClient,
                settings: container.appSettings,
                snackbar: container.snackbar,
                onBack: { route = .login },
                onRequested: { route = .login }
            )
        case .onboarding:
            OnboardingView(
                settings: container.appSettings,
                onFinished: { route = .login }
            )
        case .splash:
            SplashGateView(
                hasValidSession: container.hasValidSession,
                settings: container.appSettings,
                client: container.registrationClient
            ) { outcome in
                route = Route.afterSplash(outcome)
            }
        case .registrationLock:
            RegistrationLockView(
                client: container.registrationClient,
                authClient: container.authClient,
                profileClient: container.profileClient,
                snackbar: container.snackbar,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                onCompleted: { route = .dashboard },
                onSignedOut: { route = .login }
            )
        case .dashboard:
            PartnerShellView(container: container, onSignedOut: { route = .login })
        case let .verifyEmail(email):
            ConfirmEmailView(
                email: email,
                client: container.emailConfirmationClient,
                settings: container.appSettings,
                snackbar: container.snackbar,
                onBack: { route = .login },
                onConfirmed: { route = .splash }
            )
        }
    }

    enum Route: Equatable {
        case splash
        case login
        case register
        case forgotPassword
        case onboarding
        case verifyEmail(email: String?)
        case registrationLock
        case dashboard

        static func seed() -> Route {
            .splash
        }

        static func afterLogin(_ success: LoginSuccess) -> Route {
            success.requiresEmailConfirmation ? .verifyEmail(email: success.email) : .splash
        }

        static func afterSplash(_ outcome: SplashOutcome) -> Route {
            switch outcome {
            case .authenticated: .dashboard
            case .needsRegistrationLock: .registrationLock
            case .needsOnboarding: .onboarding
            case .unauthenticated: .login
            }
        }
    }
}
