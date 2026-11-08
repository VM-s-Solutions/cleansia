import SwiftUI

/// What the devices screen says. Each app words it in its own catalog.
public protocol DevicesCopy {
    static var title: String { get }
    static var intro: String { get }
    static var empty: String { get }
    static var errorMessage: String { get }
    static var retry: String { get }
    static var cancel: String { get }
    static var thisDevice: String { get }
    static func lastActive(_ value: String) -> String
    static var revokeButton: String { get }
    static var revokeDialogTitle: String { get }
    static func revokeDialogMessage(_ platform: String) -> String
    static var revokeDialogConfirm: String { get }
    static var selfRevokeButton: String { get }
    static var selfRevokeDialogTitle: String { get }
    static var selfRevokeDialogMessage: String { get }
    static var selfRevokeDialogConfirm: String { get }
    static var revokeSuccess: String { get }
    static var platformAndroid: String { get }
    static var platformIos: String { get }
    static var platformWeb: String { get }
    /// What a device that names no platform is called.
    static var platformUnknown: String { get }
}

public struct DevicesView: View {
    @StateObject private var vm: DevicesViewModel
    @State private var deviceToRevoke: UserDevice?
    /// The device a confirmed revoke is running for, so its row can show the spinner the dialog used to.
    @State private var revokingId: String?

    private let authClient: AuthClient
    private let copy: DevicesCopy.Type
    private let onSignedOut: () -> Void

    public init(
        client: DevicesClient,
        authClient: AuthClient,
        snackbar: SnackbarController,
        copy: DevicesCopy.Type,
        onSignedOut: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: DevicesViewModel(
            client: client,
            snackbar: snackbar,
            revokedMessage: copy.revokeSuccess
        ))
        self.authClient = authClient
        self.copy = copy
        self.onSignedOut = onSignedOut
    }

    public var body: some View {
        DevicesContent(
            state: vm.state,
            revokingId: vm.revokeAction.isSubmitting ? revokingId : nil,
            copy: copy,
            onRetry: { Task { await vm.load() } },
            onRevokeRequested: { deviceToRevoke = $0 }
        )
        .navigationTitle(copy.title)
        .navigationBarTitleDisplayMode(.inline)
        // Self-revoke gets distinct copy: revoking THIS device signs you out now. The system confirm closes
        // on the tap; the row shows the revoke running and a refusal is the snackbar's.
        .alert(
            deviceToRevoke?.isCurrent == true ? copy.selfRevokeDialogTitle : copy.revokeDialogTitle,
            isPresented: Binding(get: { deviceToRevoke != nil }, set: { if !$0 { deviceToRevoke = nil } }),
            presenting: deviceToRevoke
        ) { device in
            Button(
                device.isCurrent ? copy.selfRevokeDialogConfirm : copy.revokeDialogConfirm,
                role: .destructive
            ) {
                revokingId = device.id
                Task { await vm.revoke(device) }
            }
            Button(copy.cancel, role: .cancel) {}
        } message: { device in
            Text(
                device.isCurrent
                    ? copy.selfRevokeDialogMessage
                    : copy.revokeDialogMessage(platformLabel(device.platform, copy))
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

struct DevicesContent: View {
    let state: UiState<[UserDevice]>
    let revokingId: String?
    let copy: DevicesCopy.Type
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
            DevicesErrorState(copy: copy, onRetry: onRetry)
        case let .loaded(devices):
            if devices.isEmpty {
                Text(copy.empty)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    VStack(alignment: .leading, spacing: Spacing.m) {
                        Text(copy.intro)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        ForEach(devices) { device in
                            DeviceCard(
                                device: device,
                                revoking: revokingId == device.id,
                                canRevoke: revokingId == nil,
                                copy: copy,
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
    let copy: DevicesCopy.Type
    let onRevoke: () -> Void

    var body: some View {
        HStack(spacing: Spacing.m) {
            ZStack {
                Circle()
                    .fill(CleansiaColors.primaryContainer)
                    .frame(width: 44, height: 44)
                Image(systemName: platformIcon(device.platform))
                    .foregroundColor(CleansiaColors.primaryIconOnContainer)
            }
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: Spacing.s) {
                    Text(platformLabel(device.platform, copy))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if device.isCurrent {
                        CurrentDeviceChip(copy: copy)
                    }
                }
                if let lastActive = formatLastActive(device.lastActiveAt) {
                    Text(copy.lastActive(lastActive))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer()
            // The current row is revocable too — it reads as "sign out this device now" (distinct
            // icon + dialog copy) and ends the session at 0s instead of leaving a zombie session
            // for the ≤30s revocation directory to catch.
            if revoking {
                ProgressView()
            } else {
                Button(action: onRevoke) {
                    Image(systemName: device.isCurrent ? "rectangle.portrait.and.arrow.right" : "trash")
                        .foregroundColor(CleansiaColors.error.opacity(canRevoke ? 1 : 0.4))
                }
                .buttonStyle(.plain)
                .disabled(!canRevoke)
                .accessibilityLabel(device.isCurrent ? copy.selfRevokeButton : copy.revokeButton)
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
    let copy: DevicesCopy.Type

    var body: some View {
        Text(copy.thisDevice)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.primaryTextOnContainer)
            .padding(.horizontal, 10)
            .padding(.vertical, 3)
            .background(CleansiaColors.primaryContainer)
            .clipShape(Capsule())
    }
}

private struct DevicesErrorState: View {
    let copy: DevicesCopy.Type
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "laptopcomputer.and.iphone")
                .font(.system(size: 44))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(copy.errorMessage)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(copy.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private func platformLabel(_ platform: String?, _ copy: DevicesCopy.Type) -> String {
    switch platform?.lowercased() {
    case "android": copy.platformAndroid
    case "ios": copy.platformIos
    case "web": copy.platformWeb
    default: platform ?? copy.platformUnknown
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
    private enum PreviewDevicesCopy: DevicesCopy {
        static let title = "Your devices"
        static let intro = "Devices signed in to your account."
        static let empty = "No devices registered yet."
        static let errorMessage = "Couldn't load your devices."
        static let retry = "Retry"
        static let cancel = "Cancel"
        static let thisDevice = "This device"
        static func lastActive(_ value: String) -> String {
            "Last active \(value)"
        }

        static let revokeButton = "Remove device"
        static let revokeDialogTitle = "Remove this device?"
        static func revokeDialogMessage(_ platform: String) -> String {
            "\(platform) will be signed out."
        }

        static let revokeDialogConfirm = "Remove"
        static let selfRevokeButton = "Sign out this device"
        static let selfRevokeDialogTitle = "Sign out this device?"
        static let selfRevokeDialogMessage = "You will be signed out now."
        static let selfRevokeDialogConfirm = "Sign out"
        static let revokeSuccess = "Device removed"
        static let platformAndroid = "Android"
        static let platformIos = "iOS"
        static let platformWeb = "Web"
        static let platformUnknown = "Device"
    }

    struct DevicesContent_Previews: PreviewProvider {
        static var previews: some View {
            DevicesContent(
                state: .loaded([
                    UserDevice(
                        id: "row-1",
                        platform: "ios",
                        deviceId: "device-current",
                        lastActiveAt: Date(),
                        isCurrent: true
                    ),
                    UserDevice(
                        id: "row-2",
                        platform: "android",
                        deviceId: "device-other",
                        lastActiveAt: Date().addingTimeInterval(-86400),
                        isCurrent: false
                    )
                ]),
                revokingId: nil,
                copy: PreviewDevicesCopy.self,
                onRetry: {},
                onRevokeRequested: { _ in }
            )
        }
    }
#endif
