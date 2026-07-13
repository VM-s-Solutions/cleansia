import CleansiaCore
import SwiftUI

@main
struct CleansiaCustomerApp: App {
    @UIApplicationDelegateAdaptor(CustomerAppDelegate.self) private var appDelegate
    @StateObject private var snackbar: SnackbarController
    @StateObject private var sessionManager: SessionManager
    @StateObject private var preferences: CustomerPreferencesModel
    private let container: CustomerAppContainer

    init() {
        let snackbar = SnackbarController()
        _snackbar = StateObject(wrappedValue: snackbar)
        let container = CustomerAppContainer(snackbar: snackbar)
        container.installGeneratedClientAuth()
        StripeLaunch.applyPublishableKey()
        _sessionManager = StateObject(wrappedValue: container.sessionManager)
        _preferences = StateObject(wrappedValue: CustomerPreferencesModel(settings: container.appSettings))
        self.container = container
    }

    var body: some Scene {
        WindowGroup {
            CustomerRootView(container: container, preferences: preferences)
                .environmentObject(sessionManager)
                .environment(\.snackbarController, container.snackbar)
                .environment(\.savedAddressRepository, container.savedAddressRepository)
                .environment(\.locale, preferences.locale)
                .preferredColorScheme(preferences.theme.colorScheme)
                .snackbarHost(container.snackbar)
                .task {
                    appDelegate.registrar = container.pushRegistrar
                    container.startPush()
                    // The registration-token delegate does not fire for a cached token
                    // (re-install), so pull it explicitly once APNs has had a moment to settle.
                    try? await Task.sleep(nanoseconds: 3_000_000_000)
                    appDelegate.requestFcmToken()
                }
        }
    }
}
