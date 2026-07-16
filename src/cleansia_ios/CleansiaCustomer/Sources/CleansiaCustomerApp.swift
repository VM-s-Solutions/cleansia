import CleansiaCore
import SwiftUI

@main
struct CleansiaCustomerApp: App {
    @UIApplicationDelegateAdaptor(CustomerAppDelegate.self) private var appDelegate
    @StateObject private var snackbar: SnackbarController
    @StateObject private var sessionManager: SessionManager
    @StateObject private var preferences: CustomerPreferencesModel
    @StateObject private var pushNavigation = PushNavigationModel()
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
                .environmentObject(pushNavigation)
                .environment(\.snackbarController, container.snackbar)
                .environment(\.savedAddressRepository, container.savedAddressRepository)
                .environment(\.locale, preferences.locale)
                .preferredColorScheme(preferences.theme.colorScheme)
                .snackbarHost(container.snackbar)
                .task {
                    appDelegate.registrar = container.pushRegistrar
                    appDelegate.pushTap.onTap = { [weak pushNavigation] destination in
                        pushNavigation?.pendingDestination = destination
                    }
                    container.startPush()
                    // The registration-token delegate misses cached tokens; pull it
                    // explicitly and let it retry as the APNs token settles.
                    appDelegate.requestFcmToken()
                }
        }
    }
}
