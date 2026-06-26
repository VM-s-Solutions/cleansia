import CleansiaCore
import SwiftUI

struct PartnerRootView: View {
    private let container: PartnerAppContainer
    @EnvironmentObject private var sessionManager: SessionManager
    @State private var route: Route

    init(container: PartnerAppContainer) {
        self.container = container
        _route = State(initialValue: Route.seed(hasValidSession: container.hasValidSession))
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
                snackbar: container.snackbar
            ) { success in
                route = Route.afterLogin(success)
            }
        case .splash:
            SplashGateView(
                hasValidSession: container.hasValidSession,
                client: container.registrationClient
            ) { outcome in
                route = Route.afterSplash(outcome)
            }
        case .registrationLock:
            RegistrationLockView(
                client: container.registrationClient,
                authClient: container.authClient,
                onCompleted: { route = .dashboard },
                onSignedOut: { route = .login }
            )
        case .dashboard:
            PartnerShellView(container: container)
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
        case verifyEmail(email: String?)
        case registrationLock
        case dashboard

        static func seed(hasValidSession: Bool) -> Route {
            hasValidSession ? .splash : .login
        }

        static func afterLogin(_ success: LoginSuccess) -> Route {
            success.requiresEmailConfirmation ? .verifyEmail(email: success.email) : .splash
        }

        static func afterSplash(_ outcome: SplashOutcome) -> Route {
            switch outcome {
            case .authenticated: .dashboard
            case .needsRegistrationLock: .registrationLock
            case .unauthenticated: .login
            }
        }
    }
}
