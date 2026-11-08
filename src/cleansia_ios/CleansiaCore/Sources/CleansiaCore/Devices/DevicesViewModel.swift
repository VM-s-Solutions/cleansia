import Combine
import Foundation

@MainActor
public final class DevicesViewModel: ViewModel {
    @Published public private(set) var state: UiState<[UserDevice]> = .loading
    @Published public private(set) var revokeAction: ActionState = .idle

    /// Fires when the CURRENT device was revoked — the View forces
    /// logout + routes to login; the access token would otherwise survive
    /// ~15min on a server-killed session.
    public let signedOut = PassthroughSubject<Void, Never>()

    private let client: DevicesClient
    private let snackbar: SnackbarController
    private let revokedMessage: String
    private let localizer = ApiErrorLocalizer()

    public init(client: DevicesClient, snackbar: SnackbarController, revokedMessage: String) {
        self.client = client
        self.snackbar = snackbar
        self.revokedMessage = revokedMessage
    }

    public func load() async {
        state = .loading
        switch await client.myDevices() {
        case let .success(devices):
            state = .loaded(devices)
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    public func revoke(_ device: UserDevice) async {
        guard !revokeAction.isSubmitting else { return }
        revokeAction = .submitting
        switch await client.revoke(rowId: device.id) {
        case .success:
            snackbar.showSuccess(revokedMessage)
            revokeAction = .idle
            if isCurrentDevice(device) {
                // The caller revoked its own device — sign out so the
                // surviving access token can't strand a dead session.
                signedOut.send()
            } else if case let .loaded(devices) = state {
                state = .loaded(devices.filter { $0.id != device.id })
            }
        case let .failure(error):
            // The confirm closed on the tap, so the snackbar is where a refusal is said; the row stays.
            snackbar.showError(localizer.message(for: error))
            revokeAction = .idle
        }
    }

    private func isCurrentDevice(_ device: UserDevice) -> Bool {
        if let deviceId = device.deviceId, deviceId == client.currentDeviceId {
            return true
        }
        return device.isCurrent
    }
}
