import CleansiaCore
import SwiftUI

@main
struct CleansiaPartnerApp: App {
    @UIApplicationDelegateAdaptor(PartnerAppDelegate.self) private var appDelegate
    @StateObject private var snackbar: SnackbarController
    @StateObject private var sessionManager: SessionManager
    @StateObject private var preferences: PreferencesModel
    @StateObject private var pushNavigation = PushNavigationModel()
    private let container: PartnerAppContainer

    init() {
        let snackbar = SnackbarController()
        _snackbar = StateObject(wrappedValue: snackbar)
        let container = PartnerAppContainer(snackbar: snackbar)
        container.installGeneratedClientAuth()
        _sessionManager = StateObject(wrappedValue: container.sessionManager)
        _preferences = StateObject(wrappedValue: PreferencesModel(settings: container.appSettings))
        self.container = container
    }

    var body: some Scene {
        WindowGroup {
            PartnerRootView(container: container, preferences: preferences)
                .environmentObject(sessionManager)
                .environmentObject(pushNavigation)
                .environment(\.snackbarController, container.snackbar)
                .environment(\.locale, preferences.locale)
                .preferredColorScheme(preferences.theme.colorScheme)
                .snackbarHost(container.snackbar)
                .task {
                    appDelegate.registrar = container.pushRegistrar
                    appDelegate.pushTap.onTap = { [weak pushNavigation] destination in
                        pushNavigation?.pendingDestination = destination
                    }
                    appDelegate.onForegroundPush = { [weak badge = container.notificationBadge] eventKey in
                        badge?.notePushReceived(eventKey: eventKey)
                    }
                    container.startPush()
                    // The registration-token delegate misses cached tokens; pull it
                    // explicitly and let it retry as the APNs token settles.
                    appDelegate.requestFcmToken()
                }
        }
    }
}
