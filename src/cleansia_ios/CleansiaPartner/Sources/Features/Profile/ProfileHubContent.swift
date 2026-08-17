import AVFoundation
import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct ProfileHubContent: View {
    let data: ProfileData
    @ObservedObject var avatar: ProfileAvatarViewModel
    let avatarCache: RemoteImageCache
    let languageSummary: String
    let themeSummary: String
    let onOpen: (ProfileRoute) -> Void
    let onLogout: () -> Void

    @State private var showPhotoSourceDialog = false
    @State private var pickerSource: UIImagePickerController.SourceType?
    @State private var showCameraPermissionAlert = false

    private var employee: EmployeeItem {
        data.employee
    }

    var body: some View {
        GeometryReader { proxy in
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                ScrollView {
                    VStack(spacing: Spacing.l) {
                        ProfileHero(
                            employee: employee,
                            contractStatus: data.contractStatus,
                            topInset: proxy.safeAreaInsets.top,
                            display: avatar.display,
                            avatarCache: avatarCache,
                            onTapAvatar: { showPhotoSourceDialog = true },
                            onAvatarLoadFailure: { photo in
                                Task { await avatar.loadFailed(fileName: photo.fileName) }
                            },
                            onAvatarLoadSuccess: avatar.loadSucceeded
                        )
                        if avatar.hasPendingEdit {
                            PendingAvatarBar(
                                isSubmitting: avatar.action.isSubmitting,
                                onSave: { Task { await avatar.save() } },
                                onCancel: avatar.discard
                            )
                            .padding(.horizontal, Spacing.m)
                        }
                        sectionGroup(title: L10n.Profile.groupAccount, rows: accountRows)
                        sectionGroup(title: L10n.Profile.groupWorkLegal, rows: workLegalRows)
                        sectionGroup(title: L10n.Profile.groupPreferences, rows: preferenceRows)
                        LogoutRow(onTap: onLogout)
                            .padding(.horizontal, Spacing.m)
                            .padding(.bottom, Spacing.xxl)
                    }
                }
                .ignoresSafeArea(.container, edges: .top)
            }
        }
        .confirmationDialog(L10n.Profile.photoAdd, isPresented: $showPhotoSourceDialog, titleVisibility: .visible) {
            Button(L10n.Profile.photoTake) { requestCamera() }
            Button(L10n.Profile.photoLibrary) { pickerSource = .photoLibrary }
            if avatar.canRemove {
                Button(L10n.Profile.photoRemove, role: .destructive) { avatar.remove() }
            }
            Button(L10n.cancel, role: .cancel) {}
        }
        .sheet(item: $pickerSource) { source in
            CameraOrLibraryPicker(
                sourceType: source,
                onImagePicked: { image in
                    pickerSource = nil
                    avatar.pick(image)
                },
                onCancel: { pickerSource = nil }
            )
            .ignoresSafeArea()
        }
        .alert(L10n.Profile.cameraPermissionTitle, isPresented: $showCameraPermissionAlert) {
            Button(L10n.Profile.openSettings) { openSettings() }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.Profile.cameraPermissionMessage)
        }
    }

    private func requestCamera() {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            pickerSource = .camera
        case .notDetermined:
            AVCaptureDevice.requestAccess(for: .video) { granted in
                Task { @MainActor in
                    if granted { pickerSource = .camera } else { showCameraPermissionAlert = true }
                }
            }
        default:
            showCameraPermissionAlert = true
        }
    }

    private func openSettings() {
        guard let url = URL(string: UIApplication.openSettingsURLString) else { return }
        UIApplication.shared.open(url)
    }

    private var accountRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "person",
                title: L10n.Profile.personal,
                summary: displayName,
                route: .personal(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "mappin.and.ellipse",
                title: L10n.Profile.address,
                summary: displayAddress,
                route: .address(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "phone",
                title: L10n.Profile.emergencyContact,
                summary: displayEmergency,
                route: .emergency
            )
        ]
    }

    private var workLegalRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "person.text.rectangle",
                title: L10n.Profile.identification,
                summary: employee.passportId.nonBlankOrNil ?? L10n.Profile.noData,
                route: .identification(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "building.columns",
                title: L10n.Profile.bankDetails,
                summary: data.payoutSummary.nonBlankOrNil ?? L10n.Profile.noData,
                route: .bank(onboarding: false)
            ),
            ProfileHubRowItem(
                icon: "doc.text",
                title: L10n.Profile.myDocuments,
                summary: L10n.Profile.documentsSummary,
                route: .documents
            )
        ]
    }

    private var preferenceRows: [ProfileHubRowItem] {
        [
            ProfileHubRowItem(
                icon: "location.circle",
                title: L10n.JobRadius.title,
                summary: JobRadiusSelection(radiusKm: data.jobRadiusKm).summary,
                route: .jobRadius
            ),
            ProfileHubRowItem(icon: "globe", title: L10n.Profile.language, summary: languageSummary, route: .language),
            ProfileHubRowItem(icon: "moon", title: L10n.Profile.theme, summary: themeSummary, route: .theme),
            ProfileHubRowItem(
                icon: "laptopcomputer.and.iphone",
                title: L10n.Devices.title,
                summary: L10n.Profile.devicesSummary,
                route: .devices
            )
        ]
    }

    private func sectionGroup(title: String, rows: [ProfileHubRowItem]) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(title.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.horizontal, Spacing.m)
            VStack(spacing: 0) {
                ForEach(rows.indices, id: \.self) { index in
                    ProfileSectionRow(item: rows[index], onTap: { onOpen(rows[index].route) })
                    if index < rows.count - 1 {
                        Divider().padding(.leading, Spacing.xl)
                    }
                }
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
            .padding(.horizontal, Spacing.m)
        }
    }

    private var displayName: String {
        let value = [employee.firstName, employee.lastName]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: " ")
        return value.isEmpty ? L10n.Profile.noData : value
    }

    private var displayAddress: String {
        let value = [employee.street, employee.city, employee.zipCode]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: ", ")
        return value.isEmpty ? L10n.Profile.noData : value
    }

    private var displayEmergency: String {
        let value = [employee.emergencyContactName, employee.emergencyContactPhone]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: " · ")
        return value.isEmpty ? L10n.Profile.noData : value
    }
}

private struct ProfileHubRowItem {
    let icon: String
    let title: String
    let summary: String
    let route: ProfileRoute
}

/// The pick is not the save. It sits on the hero until the cleaner says so — the customer's edit screen
/// stages its avatar behind the same explicit Save — because an upload fired by the tap that chose the
/// image leaves no way back from a wrong pick but a second upload.
private struct PendingAvatarBar: View {
    let isSubmitting: Bool
    let onSave: () -> Void
    let onCancel: () -> Void

    var body: some View {
        HStack(spacing: Spacing.s) {
            CleansiaOutlinedButton(L10n.cancel, size: .medium, enabled: !isSubmitting, action: onCancel)
            CleansiaPrimaryButton(
                L10n.Profile.save,
                size: .medium,
                loading: isSubmitting,
                enabled: !isSubmitting,
                action: onSave
            )
        }
    }
}

private struct ProfileHero: View {
    let employee: EmployeeItem
    let contractStatus: ContractStatus?
    var topInset: CGFloat = 0
    let display: AvatarDisplay
    let avatarCache: RemoteImageCache
    let onTapAvatar: () -> Void
    let onAvatarLoadFailure: (ProfilePhoto) -> Void
    let onAvatarLoadSuccess: () -> Void

    var body: some View {
        HStack(spacing: 14) {
            avatarButton
            VStack(alignment: .leading, spacing: 2) {
                Text(name)
                    .cleansiaFont(CleansiaTypography.headlineSmall)
                    .foregroundColor(.white)
                    .lineLimit(1)
                if let email = employee.email.nonBlankOrNil {
                    Text(email)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(.white.opacity(0.85))
                        .lineLimit(1)
                }
                if let contractStatus {
                    ContractStatusChip(status: contractStatus)
                        .padding(.top, Spacing.xxs)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.top, Spacing.m + topInset)
        .padding(.bottom, Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            LinearGradient(colors: BrandGradient.blue.colors, startPoint: .top, endPoint: .bottom)
        )
    }

    /// The whole disc is the target, not the badge: the badge is 24pt of it and only says what the tap
    /// does. The label names the action rather than the picture, so it reads the same either way.
    private var avatarButton: some View {
        Button(action: onTapAvatar) {
            ZStack(alignment: .bottomTrailing) {
                ProfileAvatar(
                    display: display,
                    initials: initials,
                    cache: avatarCache,
                    onLoadFailure: onAvatarLoadFailure,
                    onLoadSuccess: onAvatarLoadSuccess
                )
                Image(systemName: "camera.fill")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(CleansiaColors.onPrimary)
                    .padding(Spacing.xs)
                    .background(CleansiaColors.primary, in: Circle())
            }
        }
        .buttonStyle(.plain)
        .accessibilityLabel(L10n.Profile.photoAdd)
    }

    private var name: String {
        let value = [employee.firstName, employee.lastName]
            .compactMap(\.nonBlankOrNil)
            .joined(separator: " ")
        return value.isEmpty ? L10n.Profile.noData : value
    }

    private var initials: String {
        let chars = [employee.firstName, employee.lastName]
            .compactMap { $0.nonBlankOrNil?.first }
            .map { String($0).uppercased() }
            .joined()
        return chars.isEmpty ? "?" : chars
    }
}

/// Unlike the customer TierBadge (white translucent capsule), the chip keeps
/// Android's semantic palette — color encodes the contract state.
private struct ContractStatusChip: View {
    let status: ContractStatus

    var body: some View {
        HStack(spacing: 6) {
            Circle()
                .fill(content)
                .frame(width: 6, height: 6)
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(content)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 4)
        .background(container)
        .clipShape(Capsule())
    }

    /// ContractStatus raw cases: _1 Pending, _2 Active, _3 Terminated,
    /// _4 Approved, _5 Rejected (matches RegistrationCompletion aliases).
    private var label: String {
        switch status {
        case ._1: L10n.Profile.contractStatusPending
        case ._2: L10n.Profile.contractStatusActive
        case ._3: L10n.Profile.contractStatusTerminated
        case ._4: L10n.Profile.contractStatusApproved
        case ._5: L10n.Profile.contractStatusRejected
        }
    }

    private var container: Color {
        switch status {
        case ._2, ._4: CleansiaColors.successBg
        case ._1: Self.amberContainer
        case ._3, ._5: CleansiaColors.errorContainer
        }
    }

    private var content: Color {
        switch status {
        case ._2, ._4: CleansiaColors.successText
        case ._1: Self.amberContent
        case ._3, ._5: CleansiaColors.onErrorContainer
        }
    }

    // Material ships no warning/amber slot; parity with the Android
    // StatusAmber* hardcode (ProfileScreen.kt:388) for the Pending chip.
    private static let amberContainer = Color(red: 1.0, green: 0.91, blue: 0.76)
    private static let amberContent = Color(red: 0.48, green: 0.30, blue: 0.0)
}

private struct ProfileSectionRow: View {
    let item: ProfileHubRowItem
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.12))
                        .frame(width: 32, height: 32)
                    Image(systemName: item.icon)
                        .font(.system(size: 16))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(item.title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(item.summary)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s + 2)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

private struct LogoutRow: View {
    let onTap: () -> Void

    var body: some View {
        Button(role: .destructive, action: onTap) {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "rectangle.portrait.and.arrow.right")
                    .font(.system(size: 16, weight: .semibold))
                Text(L10n.Profile.logout)
                    .font(CleansiaTypography.labelLarge)
            }
            .foregroundColor(CleansiaColors.error)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 14)
            .background(CleansiaColors.error.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.large))
            .overlay {
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.error.opacity(0.4), lineWidth: 1)
            }
        }
        .buttonStyle(.plain)
    }
}

private extension String? {
    var nonBlankOrNil: String? {
        guard let value = self, !value.isBlank else { return nil }
        return value
    }
}

#if DEBUG
    struct ProfileHubContent_Previews: PreviewProvider {
        static var previews: some View {
            ProfileHubContent(
                data: ProfileData(
                    employee: EmployeeItem(
                        email: "jana@example.com",
                        firstName: "Jana",
                        lastName: "Nováková",
                        street: "Vinohradská 12",
                        city: "Praha",
                        zipCode: "120 00"
                    ),
                    contractStatus: .approved,
                    payoutSummary: "19-2000145399/0800"
                ),
                avatar: ProfileAvatarViewModel(client: LivePartnerUserClient(), snackbar: SnackbarController()),
                avatarCache: RemoteImageCache(),
                languageSummary: "Čeština",
                themeSummary: "Follow system",
                onOpen: { _ in },
                onLogout: {}
            )
        }
    }
#endif
