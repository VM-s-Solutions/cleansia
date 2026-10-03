import CleansiaCore
import SwiftUI

struct CustomerDevicesView: View {
    @StateObject private var vm: CustomerDevicesViewModel
    @State private var deviceToRevoke: UserDevice?
    /// The device a confirmed revoke is running for, so its row can show the spinner the dialog used to.
    @State private var revokingId: String?

    private let authClient: AuthClient
    private let onSignedOut: () -> Void

    init(
        client: CustomerDevicesClient,
        authClient: AuthClient,
        snackbar: SnackbarController,
        onSignedOut: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: CustomerDevicesViewModel(client: client, snackbar: snackbar))
        self.authClient = authClient
        self.onSignedOut = onSignedOut
    }

    var body: some View {
        DevicesContent(
            state: vm.state,
            revokingId: vm.revokeAction.isSubmitting ? revokingId : nil,
            onRetry: { Task { await vm.load() } },
            onRevokeRequested: { deviceToRevoke = $0 }
        )
        .navigationTitle(L10n.Devices.title)
        .navigationBarTitleDisplayMode(.inline)
        // Self-revoke gets distinct copy: revoking THIS device signs you out now. The system confirm closes
        // on the tap; the row shows the revoke running and a refusal is the snackbar's.
        .alert(
            deviceToRevoke?.isCurrent == true ? L10n.Devices.selfRevokeDialogTitle : L10n.Devices.revokeDialogTitle,
            isPresented: Binding(get: { deviceToRevoke != nil }, set: { if !$0 { deviceToRevoke = nil } }),
            presenting: deviceToRevoke
        ) { device in
            Button(
                device.isCurrent ? L10n.Devices.selfRevokeDialogConfirm : L10n.Devices.revokeDialogConfirm,
                role: .destructive
            ) {
                revokingId = device.id
                Task { await vm.revoke(device) }
            }
            Button(L10n.cancel, role: .cancel) {}
        } message: { device in
            Text(
                device.isCurrent
                    ? L10n.Devices.selfRevokeDialogMessage
                    : L10n.Devices.revokeDialogMessage(platformLabel(device.platform))
            )
        }
        .task { await vm.load() }
        .onReceive(vm.signedOut) {
            Task {
                // Local wipe only: the self-revoke already killed this device's session
                // server-side, so the full logout()'s server call would be a redundant round
                // trip that only delays landing on the login screen.
                await authClient.signOutLocal()
                onSignedOut()
            }
        }
    }
}

private struct DevicesContent: View {
    let state: UiState<[UserDevice]>
    let revokingId: String?
    let onRetry: () -> Void
    let onRevokeRequested: (UserDevice) -> Void

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            content
        }
    }

    @ViewBuilder
    private var content: some View {
        switch state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            DevicesErrorState(onRetry: onRetry)
        case let .loaded(devices):
            if devices.isEmpty {
                Text(L10n.Devices.empty)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    VStack(alignment: .leading, spacing: Spacing.m) {
                        Text(L10n.Devices.intro)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        ForEach(devices) { device in
                            DeviceCard(
                                device: device,
                                revoking: revokingId == device.id,
                                canRevoke: revokingId == nil,
                                onRevoke: { onRevokeRequested(device) }
                            )
                        }
                    }
                    .padding(Spacing.m)
                }
            }
        }
    }
}

private struct DeviceCard: View {
    let device: UserDevice
    let revoking: Bool
    /// One revoke at a time: the view model drops a second while the first is in flight.
    let canRevoke: Bool
    let onRevoke: () -> Void

    var body: some View {
        HStack(spacing: Spacing.m) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.primaryContainer)
                    .frame(width: 44, height: 44)
                Image(systemName: platformIcon(device.platform))
                    .foregroundColor(CleansiaColors.primary)
            }
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: Spacing.s) {
                    Text(platformLabel(device.platform))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if device.isCurrent {
                        CurrentDeviceChip()
                    }
                }
                if let lastActive = formatLastActive(device.lastActiveAt) {
                    Text(L10n.Devices.lastActive(lastActive))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer()
            // The current device is revocable too: it reads as "sign out this device now"
            // (distinct icon + dialog copy) and ends the session at 0s instead of leaving a zombie
            // session for the ≤30s revocation directory to catch.
            if revoking {
                ProgressView()
            } else {
                Button(action: onRevoke) {
                    Image(systemName: device.isCurrent ? "rectangle.portrait.and.arrow.right" : "trash")
                        .foregroundColor(CleansiaColors.error.opacity(canRevoke ? 1 : 0.4))
                }
                .buttonStyle(.plain)
                .disabled(!canRevoke)
                .accessibilityLabel(device.isCurrent ? L10n.Devices.selfRevokeButton : L10n.Devices.revokeButton)
            }
        }
        .padding(Spacing.l)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outline, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }
}

private struct CurrentDeviceChip: View {
    var body: some View {
        Text(L10n.Devices.thisDevice)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.primary)
            .padding(.horizontal, 10)
            .padding(.vertical, 3)
            .background(CleansiaColors.primaryContainer)
            .clipShape(Capsule())
    }
}

private struct DevicesErrorState: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "laptopcomputer.and.iphone")
                .font(.system(size: 44))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Devices.errorMessage)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private func platformLabel(_ platform: String?) -> String {
    switch platform?.lowercased() {
    case "android": L10n.Devices.platformAndroid
    case "ios": L10n.Devices.platformIos
    case "web": L10n.Devices.platformWeb
    default: platform ?? L10n.Devices.platformUnknown
    }
}

private func platformIcon(_ platform: String?) -> String {
    switch platform?.lowercased() {
    case "android": "candybarphone"
    case "ios": "iphone"
    case "web": "laptopcomputer"
    default: "desktopcomputer"
    }
}

private let lastActiveFormatter: DateFormatter = {
    let formatter = DateFormatter()
    formatter.dateStyle = .medium
    formatter.timeStyle = .short
    return formatter
}()

private func formatLastActive(_ date: Date?) -> String? {
    guard let date else { return nil }
    return lastActiveFormatter.string(from: date)
}

#if DEBUG
    struct CustomerDevicesContent_Previews: PreviewProvider {
        static var previews: some View {
            DevicesContent(
                state: .loaded([
                    UserDevice(id: "row-1", platform: "ios", deviceId: "d1", lastActiveAt: Date(), isCurrent: true),
                    UserDevice(id: "row-2", platform: "android", deviceId: "d2", lastActiveAt: Date(), isCurrent: false)
                ]),
                revokingId: nil,
                onRetry: {},
                onRevokeRequested: { _ in }
            )
        }
    }
#endif
