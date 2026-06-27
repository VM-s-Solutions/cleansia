import CleansiaCore
import SwiftUI

@main
struct CleansiaPartnerApp: App {
    @StateObject private var snackbar: SnackbarController
    @StateObject private var sessionManager: SessionManager
    @StateObject private var preferences: PreferencesModel
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
                .environment(\.snackbarController, container.snackbar)
                .environment(\.locale, preferences.locale)
                .preferredColorScheme(preferences.theme.colorScheme)
                .snackbarHost(container.snackbar)
        }
    }
}
