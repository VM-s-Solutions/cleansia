import CleansiaCore
import SwiftUI

struct SplashGateView: View {
    @StateObject private var vm: CustomerSplashViewModel
    @State private var fadedIn = false
    let onResolved: (CustomerSplashOutcome) -> Void

    init(hasValidSession: Bool, onResolved: @escaping (CustomerSplashOutcome) -> Void) {
        _vm = StateObject(wrappedValue: CustomerSplashViewModel(hasValidSession: hasValidSession))
        self.onResolved = onResolved
    }

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
        .onAppear {
            withAnimation(.easeInOut(duration: 0.6)) { fadedIn = true }
        }
        .task { await vm.resolve() }
        .onChange(of: vm.outcome) { outcome in
            if let outcome { onResolved(outcome) }
        }
    }
}

#if DEBUG
    struct SplashGateView_Previews: PreviewProvider {
        static var previews: some View {
            SplashGateView(hasValidSession: false, onResolved: { _ in })
        }
    }
#endif
