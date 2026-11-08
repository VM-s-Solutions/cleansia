import CleansiaCore
import SwiftUI

/// The partner's devices screen, in the partner's words.
extension DevicesView {
    init(
        client: DevicesClient,
        authClient: AuthClient,
        snackbar: SnackbarController,
        onSignedOut: @escaping () -> Void
    ) {
        self.init(
            client: client,
            authClient: authClient,
            snackbar: snackbar,
            copy: L10n.Devices.self,
            onSignedOut: onSignedOut
        )
    }
}
