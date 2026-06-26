import CleansiaCore
import SwiftUI

@main
struct CleansiaPartnerApp: App {
    @StateObject private var snackbar: SnackbarController
    @StateObject private var sessionManager: SessionManager
    private let container: PartnerAppContainer

    init() {
        let snackbar = SnackbarController()
        _snackbar = StateObject(wrappedValue: snackbar)
        let container = PartnerAppContainer(snackbar: snackbar)
        _sessionManager = StateObject(wrappedValue: container.sessionManager)
        self.container = container
    }

    var body: some Scene {
        WindowGroup {
            PartnerRootView(container: container)
                .environmentObject(sessionManager)
                .environment(\.snackbarController, container.snackbar)
                .snackbarHost(container.snackbar)
        }
    }
}
