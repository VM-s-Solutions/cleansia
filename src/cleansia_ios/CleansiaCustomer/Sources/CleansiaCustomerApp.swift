import CleansiaCore
import SwiftUI

@main
struct CleansiaCustomerApp: App {
    @StateObject private var snackbar: SnackbarController
    @StateObject private var sessionManager: SessionManager
    private let container: CustomerAppContainer

    init() {
        let snackbar = SnackbarController()
        _snackbar = StateObject(wrappedValue: snackbar)
        let container = CustomerAppContainer(snackbar: snackbar)
        container.installGeneratedClientAuth()
        StripeLaunch.applyPublishableKey()
        _sessionManager = StateObject(wrappedValue: container.sessionManager)
        self.container = container
    }

    var body: some Scene {
        WindowGroup {
            CustomerRootView(container: container)
                .environmentObject(sessionManager)
                .environment(\.snackbarController, container.snackbar)
                .snackbarHost(container.snackbar)
        }
    }
}
