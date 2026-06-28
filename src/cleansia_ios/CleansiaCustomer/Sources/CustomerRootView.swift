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
            AuthPlaceholderView(systemImage: "person.crop.circle", title: L10n.Auth.signIn)
        case .register:
            AuthPlaceholderView(systemImage: "person.badge.plus", title: L10n.Auth.signUp)
        case .forgotPassword:
            AuthPlaceholderView(systemImage: "key", title: L10n.Auth.forgotPassword)
        case .verifyEmail:
            AuthPlaceholderView(systemImage: "envelope.badge", title: L10n.Auth.verifyEmail)
        case .home:
            CustomerShellView(onSignedOut: { route = .login })
        }
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

        static func afterLogin(_ success: LoginSuccess) -> Route {
            success.requiresEmailConfirmation ? .verifyEmail(email: success.email) : .splash
        }

        static func afterSplash(_ outcome: CustomerSplashOutcome) -> Route {
            switch outcome {
            case .authenticated: .home
            case .unauthenticated: .login
            }
        }
    }
}

private struct AuthPlaceholderView: View {
    let systemImage: String
    let title: String

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: systemImage)
                .font(.system(size: 48))
                .foregroundColor(CleansiaColors.primary)
            Text(verbatim: title)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}
