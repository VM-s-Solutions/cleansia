import CleansiaCore
import SwiftUI

struct PartnerRootView: View {
    private let container: PartnerAppContainer
    @ObservedObject private var preferences: PreferencesModel
    @EnvironmentObject private var sessionManager: SessionManager
    @State private var route: Route

    init(container: PartnerAppContainer, preferences: PreferencesModel) {
        self.container = container
        self.preferences = preferences
        _route = State(initialValue: Route.seed())
    }

    var body: some View {
        // ZStack (not the switch view itself) keeps the forced-sign-out
        // subscription alive across route swaps — a task on `content`
        // restarts whenever the route case changes identity.
        ZStack {
            content
        }
        .task {
            for await _ in sessionManager.forcedSignOutStream {
                route = .login
            }
        }
        .onChange(of: route) { _ in
            container.updatePushSession(hasSession: container.hasValidSession)
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
            PartnerShellView(
                container: container,
                preferences: preferences,
                onSignedOut: { route = .login }
            )
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
