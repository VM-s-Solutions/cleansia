import CleansiaCore
import Combine
import Foundation

@MainActor
final class CustomerDevicesViewModel: ViewModel {
    @Published private(set) var state: UiState<[UserDevice]> = .loading
    @Published private(set) var revokeAction: ActionState = .idle

    let revoked = PassthroughSubject<Void, Never>()
    let signedOut = PassthroughSubject<Void, Never>()

    private let client: CustomerDevicesClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: CustomerDevicesClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        switch await client.myDevices() {
        case let .success(devices):
            state = .loaded(devices)
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func revoke(_ device: UserDevice) async {
        guard !revokeAction.isSubmitting else { return }
        revokeAction = .submitting
        switch await client.revoke(rowId: device.id) {
        case .success:
            snackbar.showSuccess(L10n.Devices.revokeSuccess)
            revokeAction = .idle
            revoked.send()
            if isCurrentDevice(device) {
                signedOut.send()
            } else if case let .loaded(devices) = state {
                state = .loaded(devices.filter { $0.id != device.id })
            }
        case let .failure(error):
            snackbar.showError(localizer.message(for: error))
            revokeAction = .error(L10n.Devices.revokeRetryHint)
        }
    }

    private func isCurrentDevice(_ device: UserDevice) -> Bool {
        if let deviceId = device.deviceId, deviceId == client.currentDeviceId {
            return true
        }
        return device.isCurrent
    }
}
