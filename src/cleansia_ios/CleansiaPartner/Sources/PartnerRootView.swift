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
                signupConsent: container.signupConsent,
                onSignIn: { route = .login },
                onRegistered: { route = .verifyEmail(email: $0) }
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
                preferences: preferences,
                onFinished: { route = .login }
            )
        case let .splash(showsBrandHold):
            SplashGateView(
                hasValidSession: container.hasValidSession,
                settings: container.appSettings,
                client: container.registrationClient,
                showsBrandHold: showsBrandHold,
                onSignOut: { route = .login },
                // Passed as an argument rather than trailing: with two closures, trailing syntax hides
                // which one is which at the call site, and SwiftLint refuses it.
                onResolved: { outcome in route = Route.afterSplash(outcome) }
            )
        case .registrationLock:
            RegistrationLockView(
                client: container.registrationClient,
                authClient: container.authClient,
                profileClient: container.profileClient,
                preferences: preferences,
                snackbar: container.snackbar,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                serviceArea: container.serviceArea,
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
                // Same reasoning as afterLogin: they have been in the app for the whole
                // confirmation flow, so the reveal has nothing left to introduce.
                onConfirmed: { route = .splash(showsBrandHold: false) }
            )
        }
    }

    enum Route: Equatable {
        /// `showsBrandHold` is false on every entry that is NOT a cold start. The gate itself always
        /// runs — ADR-0020 D3 requires a verified login to bounce through it, and adr-0020.md:288
        /// makes bypassing that a blocking review finding — but replaying the ~1.8s branded reveal
        /// after the cleaner has just typed their password is what makes it look like the app
        /// refreshed itself.
        case splash(showsBrandHold: Bool)
        case login
        case register
        case forgotPassword
        case onboarding
        case verifyEmail(email: String?)
        case registrationLock
        case dashboard

        static func seed() -> Route {
            // The one true cold start, and the only place the reveal is what it was designed for.
            .splash(showsBrandHold: true)
        }

        static func afterLogin(_ success: LoginSuccess) -> Route {
            success.requiresEmailConfirmation
                ? .verifyEmail(email: success.email)
                : .splash(showsBrandHold: false)
        }

        static func afterSplash(_ outcome: SplashOutcome) -> Route {
            switch outcome {
            case .authenticated: .dashboard
            case .needsRegistrationLock: .registrationLock
            case .needsOnboarding: .onboarding
            case .unauthenticated: .login
            // Unreachable stays put — SplashGateView never hands it over, and mapping it back to
            // .splash keeps this function total without inventing a destination. This switch has
            // no `default` on purpose: a new outcome has to be routed deliberately.
            case .unreachable: .splash(showsBrandHold: false)
            }
        }
    }
}
