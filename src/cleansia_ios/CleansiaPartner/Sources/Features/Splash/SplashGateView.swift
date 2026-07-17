import CleansiaCore
import SwiftUI

struct SplashGateView: View {
    @StateObject private var vm: SplashViewModel
    @State private var fadedIn = false
    let onResolved: (SplashOutcome) -> Void

    init(
        hasValidSession: Bool,
        settings: AppSettingsStore,
        client: PartnerRegistrationClient,
        onResolved: @escaping (SplashOutcome) -> Void
    ) {
        _vm = StateObject(wrappedValue: SplashViewModel(
            hasValidSession: hasValidSession,
            settings: settings,
            client: client
        ))
        self.onResolved = onResolved
    }

    var body: some View {
        SplashBrandingView(fadedIn: fadedIn)
            .onAppear {
                withAnimation(.easeInOut(duration: 0.6)) { fadedIn = true }
            }
            .task { await vm.resolve() }
            .onChange(of: vm.outcome) { outcome in
                if let outcome { onResolved(outcome) }
            }
    }
}

/// The branded splash visuals — the customer app's splash ported to the partner app
/// so both share one look. Extracted from the gate so the preview can render it
/// without constructing the resolve ViewModel; the fail-closed gate logic above is
/// untouched.
private struct SplashBrandingView: View {
    let fadedIn: Bool

    var body: some View {
        VStack(spacing: 0) {
            Mascot.waving.image
                .resizable()
                .scaledToFit()
                .frame(width: 180, height: 180)
            Text(verbatim: "Cleansia")
                .font(CleansiaFont.poppins(.bold, size: 44))
                .foregroundColor(.white)
                .padding(.top, Spacing.l)
            Text(verbatim: L10n.Splash.tagline)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(.white.opacity(0.9))
                .multilineTextAlignment(.center)
                .padding(.top, Spacing.xs)
            ProgressView()
                .progressViewStyle(.circular)
                .tint(.white)
                .padding(.top, 36)
        }
        .opacity(fadedIn ? 1 : 0)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(
            LinearGradient(
                colors: [CleansiaColors.splashGradientStart, CleansiaColors.splashGradientEnd],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            .ignoresSafeArea()
        )
    }
}

#if DEBUG
    struct SplashGateView_Previews: PreviewProvider {
        static var previews: some View {
            SplashBrandingView(fadedIn: true)
                .previewDisplayName("Resolving")
        }
    }
#endif
