import CleansiaCore
import Combine
import Foundation

@MainActor
final class CustomerDevicesViewModel: ViewModel {
    @Published private(set) var state: UiState<[UserDevice]> = .loading
    @Published private(set) var revokeAction: ActionState = .idle

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
            if isCurrentDevice(device) {
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
