import AVFoundation
import CleansiaCore
import SwiftUI
import UIKit

struct EditProfileView: View {
    @ObservedObject var vm: ProfileViewModel
    let avatarCache: RemoteImageCache
    var showBookingHint = false
    let onSaved: () -> Void

    @State private var firstName = ""
    @State private var lastName = ""
    @State private var phone = ""
    @State private var birthDate: Date?
    @State private var loadedFor: String?
    @State private var showPhotoSourceDialog = false
    @State private var pickerSource: UIImagePickerController.SourceType?
    @State private var showCameraPermissionAlert = false

    private var canSave: Bool {
        !firstName.isBlank && !lastName.isBlank && !vm.saveState.isSubmitting
    }

    private func missingFieldError(_ value: String, message: String) -> String? {
        showBookingHint && value.isBlank ? message : nil
    }

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.m) {
                    if showBookingHint {
                        BookingHintBanner()
                    }
                    AvatarField(
                        display: vm.editorAvatar,
                        initials: vm.currentUser?.initials ?? "",
                        cache: avatarCache,
                        onTap: { showPhotoSourceDialog = true },
                        onLoadFailure: { photo in
                            Task { await vm.avatarLoadFailed(fileName: photo.fileName) }
                        }
                    )
                    CleansiaTextField(
                        value: $firstName,
                        label: L10n.EditProfile.firstName,
                        errorText: missingFieldError(firstName, message: L10n.Auth.errorFirstNameRequired),
                        textContentType: .givenName
                    )
                    CleansiaTextField(
                        value: $lastName,
                        label: L10n.EditProfile.lastName,
                        errorText: missingFieldError(lastName, message: L10n.Auth.errorLastNameRequired),
                        textContentType: .familyName
                    )
                    CleansiaTextField(
                        value: .constant(vm.currentUser?.email ?? ""),
                        label: L10n.EditProfile.email,
                        helper: L10n.EditProfile.emailReadonly,
                        textContentType: .emailAddress,
                        enabled: false
                    )
                    CleansiaPhoneInput(
                        value: $phone,
                        label: L10n.EditProfile.phone,
                        errorText: missingFieldError(phone, message: L10n.EditProfile.phoneRequired)
                    )
                    BirthDateField(
                        birthDate: $birthDate,
                        label: L10n.EditProfile.birthDate,
                        placeholder: L10n.EditProfile.birthDatePlaceholder
                    )

                    CleansiaPrimaryButton(
                        L10n.EditProfile.save,
                        loading: vm.saveState.isSubmitting,
                        enabled: canSave
                    ) {
                        Task {
                            await vm.save(
                                firstName: firstName,
                                lastName: lastName,
                                phoneNumber: phone,
                                birthDate: birthDate,
                                languageCode: vm.currentUser?.preferredLanguageCode
                            )
                        }
                    }
                    .padding(.top, Spacing.m)
                }
                .padding(Spacing.m)
            }
        }
        .navigationTitle(L10n.EditProfile.title)
        .navigationBarTitleDisplayMode(.inline)
        .task { await vm.refresh() }
        .onReceive(vm.repository.$currentUser) { user in seed(user) }
        .onReceive(vm.saved) { onSaved() }
        .confirmationDialog(L10n.EditProfile.photoLabel, isPresented: $showPhotoSourceDialog) {
            Button(L10n.EditProfile.photoTake) { takePhoto() }
            Button(L10n.EditProfile.photoLibrary) { chooseFromLibrary() }
            if vm.canRemoveAvatar {
                Button(L10n.EditProfile.photoRemove, role: .destructive) { vm.removeAvatar() }
            }
            Button(L10n.cancel, role: .cancel) {}
        }
        .sheet(item: $pickerSource) { source in
            CameraOrLibraryPicker(
                sourceType: source,
                onImagePicked: pick,
                onCancel: { pickerSource = nil }
            )
            .ignoresSafeArea()
        }
        .alert(L10n.EditProfile.cameraPermissionTitle, isPresented: $showCameraPermissionAlert) {
            Button(L10n.EditProfile.openSettings) { openSettings() }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.EditProfile.cameraPermissionMessage)
        }
    }

    private func seed(_ user: CurrentUserProfile?) {
        guard let user, loadedFor != user.email else { return }
        loadedFor = user.email
        firstName = user.firstName
        lastName = user.lastName
        phone = user.phoneNumber ?? ""
        birthDate = user.birthDate
    }

    private func pick(_ image: UIImage) {
        pickerSource = nil
        vm.pickAvatar(image)
    }

    private func takePhoto() {
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

    private func chooseFromLibrary() {
        pickerSource = .photoLibrary
    }

    private func openSettings() {
        guard let url = URL(string: UIApplication.openSettingsURLString) else { return }
        UIApplication.shared.open(url)
    }
}

/// The avatar, its caption and the tap target that opens the source dialog. Stateless — every decision
/// it renders is resolved by the view model.
private struct AvatarField: View {
    let display: AvatarDisplay
    let initials: String
    let cache: RemoteImageCache
    let onTap: () -> Void
    let onLoadFailure: (ProfilePhoto) -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                ZStack(alignment: .bottomTrailing) {
                    ProfileAvatar(
                        display: display,
                        initials: initials,
                        cache: cache,
                        diameter: 96,
                        strokeWidth: 1,
                        strokeColor: CleansiaColors.outlineVariant,
                        onLoadFailure: onLoadFailure
                    )
                    Image(systemName: "camera.fill")
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(CleansiaColors.onPrimary)
                        .padding(Spacing.xs)
                        .background(CleansiaColors.primary, in: Circle())
                }
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    Text(L10n.EditProfile.photoLabel)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(display.isImage ? L10n.EditProfile.photoChange : L10n.EditProfile.photoAdd)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.primary)
                }
                Spacer(minLength: 0)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

private struct BookingHintBanner: View {
    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: "info.circle.fill")
                .foregroundColor(CleansiaColors.primary)
            Text(L10n.EditProfile.bookingHint)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onPrimaryContainer)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.primaryContainer)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}
