import AVFoundation
import CleansiaCore
import SwiftUI
import UIKit

/// The cleaner's photo and everything that changes it — the disc, the source dialog, the picker, the
/// camera-permission alert and the staged save — as one view. None of those five is useful without the
/// other four, so a host that mounted only the disc would end up re-declaring the rest.
///
/// It lives on the Personal data section, not the hub. The hub is a menu of places to go; the photo is
/// one of the person's own details, so it is edited beside the name and the birth date. The hero still
/// draws the same face, read-only.
struct ProfileAvatarField: View {
    @ObservedObject var avatar: ProfileAvatarViewModel
    let cache: RemoteImageCache

    /// False on the onboarding chain, where the section's own button says "Next" and
    /// pushes the photo with it - a Save/Cancel pair sitting directly above Next reads as
    /// two competing commits. Android folds the two the same way. True on the hub, where
    /// the section's button says "Save" and the two writes are genuinely independent.
    var showsPendingBar: Bool = true

    @State private var showSourceDialog = false
    @State private var pickerSource: UIImagePickerController.SourceType?
    @State private var showCameraPermissionAlert = false

    var body: some View {
        VStack(spacing: Spacing.s) {
            card
            if showsPendingBar, avatar.hasPendingEdit {
                PendingAvatarBar(
                    isSubmitting: avatar.action.isSubmitting,
                    onSave: { Task { await avatar.save() } },
                    onCancel: avatar.discard
                )
            }
        }
        .confirmationDialog(L10n.Profile.photoAdd, isPresented: $showSourceDialog, titleVisibility: .visible) {
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

    /// The whole card is the target, not the badge: the badge is 24pt of it and only says what the tap
    /// does. The label names the action rather than the picture, so it reads the same either way.
    ///
    /// A thin outline ring here, not the hero's translucent white one — that ring is drawn to read
    /// against the brand gradient and would vanish on a surface-coloured card.
    private var card: some View {
        Button {
            showSourceDialog = true
        } label: {
            HStack(spacing: Spacing.m) {
                disc
                Text(L10n.Profile.photoAdd)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                Spacer(minLength: 0)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(L10n.Profile.photoAdd)
    }

    private var disc: some View {
        ZStack(alignment: .bottomTrailing) {
            ProfileAvatar(
                display: avatar.display,
                initials: initials,
                cache: cache,
                diameter: 96,
                strokeWidth: 1,
                strokeColor: CleansiaColors.outlineVariant,
                onLoadFailure: { photo in
                    Task { await avatar.loadFailed(fileName: photo.fileName) }
                },
                onLoadSuccess: avatar.loadSucceeded
            )
            Image(systemName: "camera.fill")
                .font(.system(size: 12, weight: .semibold))
                .foregroundColor(CleansiaColors.onPrimary)
                .padding(Spacing.xs)
                .background(CleansiaColors.primary, in: Circle())
        }
    }

    /// Read off the USER row rather than the employee record the hero's initials come from — this view
    /// takes the avatar model and nothing else, and the photo it draws is stored on that same row.
    private var initials: String {
        let chars = [avatar.user?.firstName, avatar.user?.lastName]
            .compactMap { $0?.trimmedOrNil?.first }
            .map { String($0).uppercased() }
            .joined()
        return chars.isEmpty ? "?" : chars
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
}

/// The pick is not the save. It sits on the card until the cleaner says so — the customer's edit screen
/// stages its avatar behind the same explicit Save — because an upload fired by the tap that chose the
/// image leaves no way back from a wrong pick but a second upload.
///
/// Its own Save, separate from the section's: this one writes the user row, the section's writes the
/// employee record, and neither is waiting on the other.
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
