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
        WordmarkSplashView(subtitle: L10n.Splash.tagline)
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
