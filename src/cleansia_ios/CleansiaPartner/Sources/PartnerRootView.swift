import CleansiaCore
import SwiftUI

struct PartnerRootView: View {
    private let container: PartnerAppContainer
    @EnvironmentObject private var sessionManager: SessionManager
    @State private var route: Route

    init(container: PartnerAppContainer) {
        self.container = container
        _route = State(initialValue: container.hasValidSession ? .dashboard : .login)
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
        case .dashboard:
            PlaceholderDashboardView()
        case .verifyEmail:
            PlaceholderVerifyEmailView()
        }
    }

    enum Route: Equatable {
        case login
        case dashboard
        case verifyEmail

        static func afterLogin(_ success: LoginSuccess) -> Route {
            success.requiresEmailConfirmation ? .verifyEmail : .dashboard
        }
    }
}

private struct PlaceholderDashboardView: View {
    var body: some View {
        PlaceholderDestination(systemImage: "square.grid.2x2", text: "Dashboard — Slice B")
    }
}

private struct PlaceholderVerifyEmailView: View {
    var body: some View {
        PlaceholderDestination(systemImage: "envelope.badge", text: "Verify your email — coming in T-0305")
    }
}

private struct PlaceholderDestination: View {
    let systemImage: String
    let text: String

    var body: some View {
        VStack(spacing: Spacing.xs) {
            Image(systemName: systemImage)
                .font(.system(size: 48))
                .foregroundColor(CleansiaColors.primary)
            Text(verbatim: text)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}
