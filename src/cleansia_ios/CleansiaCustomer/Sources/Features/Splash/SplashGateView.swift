import CleansiaCore
import SwiftUI

struct SplashGateView: View {
    @StateObject private var vm: CustomerSplashViewModel
    let onResolved: (CustomerSplashOutcome) -> Void

    init(hasValidSession: Bool, onResolved: @escaping (CustomerSplashOutcome) -> Void) {
        _vm = StateObject(wrappedValue: CustomerSplashViewModel(hasValidSession: hasValidSession))
        self.onResolved = onResolved
    }

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "sparkles")
                .font(.system(size: 56))
                .foregroundColor(CleansiaColors.primary)
            Text(verbatim: "Cleansia")
                .font(CleansiaTypography.displayLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Text(verbatim: L10n.Splash.tagline)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            ProgressView()
                .padding(.top, Spacing.m)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
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
