import SwiftUI

private struct SnackbarControllerKey: EnvironmentKey {
    @MainActor static var defaultValue = SnackbarController()
}

public extension EnvironmentValues {
    var snackbarController: SnackbarController {
        get { self[SnackbarControllerKey.self] }
        set { self[SnackbarControllerKey.self] = newValue }
    }
}
