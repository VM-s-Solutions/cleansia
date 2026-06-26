import CleansiaCore
import SwiftUI

struct SplashGateView: View {
    @StateObject private var vm: SplashViewModel
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
        ProgressView()
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
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background)
                .previewDisplayName("Resolving")
        }
    }
#endif
