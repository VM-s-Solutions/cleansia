import AVFoundation
import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct PhotosSection: View {
    @ObservedObject var vm: OrderPhotosViewModel
    let canUploadBefore: Bool
    let canUploadAfter: Bool

    var body: some View {
        OrderSectionCard(title: L10n.Orders.photosSectionTitle, systemImage: "camera") {
            switch vm.state {
            case .loading:
                ProgressView()
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, Spacing.m)
            case .error:
                PhotoRailsContent(
                    photos: [],
                    mutation: vm.mutation,
                    canUploadBefore: false,
                    canUploadAfter: false
                ) { _, _ in } onDelete: { _ in }
            case let .loaded(photos):
                PhotoRailsContent(
                    photos: photos,
                    mutation: vm.mutation,
                    canUploadBefore: canUploadBefore,
                    canUploadAfter: canUploadAfter,
                    onPick: { type, image in Task { await vm.upload(type: type, image: image) } },
                    onDelete: { id in Task { await vm.delete(photoId: id) } }
                )
            }
        }
    }
}

private struct PhotoRailsContent: View {
    let photos: [OrderPhoto]
    let mutation: PhotoMutationState
    let canUploadBefore: Bool
    let canUploadAfter: Bool
    var onPick: (PhotoType, UIImage) -> Void
    var onDelete: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            PhotoRail(
                title: L10n.Orders.photosBefore,
                type: ._1,
                photos: photos.filter { $0.photoType == ._1 },
                isReadOnly: !canUploadBefore,
                mutation: mutation,
                onPick: onPick,
                onDelete: onDelete
            )
            PhotoRail(
                title: L10n.Orders.photosAfter,
                type: ._2,
                photos: photos.filter { $0.photoType == ._2 },
                isReadOnly: !canUploadAfter,
                mutation: mutation,
                onPick: onPick,
                onDelete: onDelete
            )
        }
    }
}

private struct PhotoRail: View {
    let title: String
    let type: PhotoType
    let photos: [OrderPhoto]
    let isReadOnly: Bool
    let mutation: PhotoMutationState
    var onPick: (PhotoType, UIImage) -> Void
    var onDelete: (String) -> Void

    @State private var showSourceDialog = false
    @State private var pickerSource: UIImagePickerController.SourceType?
    @State private var showPermissionAlert = false

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(title)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)

            if isReadOnly, photos.isEmpty {
                Text(L10n.Orders.photosNoneRecorded)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else {
                rail
            }
        }
        .confirmationDialog(L10n.Orders.addPhoto, isPresented: $showSourceDialog, titleVisibility: .visible) {
            Button(L10n.Orders.takePhoto) { requestCamera() }
            Button(L10n.Orders.chooseFromLibrary) { pickerSource = .photoLibrary }
            Button(L10n.cancel, role: .cancel) {}
        }
        .sheet(item: $pickerSource) { source in
            CameraOrLibraryPicker(
                sourceType: source,
                onImagePicked: { image in
                    pickerSource = nil
                    onPick(type, image)
                },
                onCancel: { pickerSource = nil }
            )
            .ignoresSafeArea()
        }
        .alert(L10n.Orders.cameraPermissionTitle, isPresented: $showPermissionAlert) {
            Button(L10n.Orders.openSettings) { openSettings() }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.Orders.cameraPermissionMessage)
        }
    }

    private var rail: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: Spacing.s) {
                if !isReadOnly {
                    AddPhotoTile(isUploading: mutation.isUploading) { showSourceDialog = true }
                }
                ForEach(photos) { photo in
                    PhotoTile(
                        photo: photo,
                        isDeleting: mutation.deletingId == photo.id,
                        isReadOnly: isReadOnly,
                        onDelete: { onDelete(photo.id) }
                    )
                }
            }
        }
    }

    private func requestCamera() {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            pickerSource = .camera
        case .notDetermined:
            AVCaptureDevice.requestAccess(for: .video) { granted in
                Task { @MainActor in
                    if granted { pickerSource = .camera } else { showPermissionAlert = true }
                }
            }
        default:
            showPermissionAlert = true
        }
    }

    private func openSettings() {
        guard let url = URL(string: UIApplication.openSettingsURLString) else { return }
        UIApplication.shared.open(url)
    }
}

extension UIImagePickerController.SourceType: Identifiable {
    public var id: Int {
        rawValue
    }
}

private struct AddPhotoTile: View {
    let isUploading: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            ZStack {
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .fill(CleansiaColors.primary.opacity(0.08))
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .strokeBorder(CleansiaColors.primary.opacity(0.5), lineWidth: 1)
                if isUploading {
                    ProgressView().tint(CleansiaColors.primary)
                } else {
                    VStack(spacing: 2) {
                        Image(systemName: "camera.fill")
                            .font(.system(size: 20))
                        Text(L10n.Orders.addPhoto)
                            .font(CleansiaTypography.labelSmall)
                    }
                    .foregroundColor(CleansiaColors.primary)
                }
            }
            .frame(width: 80, height: 80)
        }
        .buttonStyle(.plain)
        .disabled(isUploading)
        .accessibilityLabel(L10n.Orders.addPhoto)
    }
}

private struct PhotoTile: View {
    let photo: OrderPhoto
    let isDeleting: Bool
    let isReadOnly: Bool
    let onDelete: () -> Void

    var body: some View {
        ZStack(alignment: .topTrailing) {
            thumbnail
            if !isReadOnly {
                deleteButton
            }
        }
    }

    private var thumbnail: some View {
        AsyncImage(url: photo.blobUrl.flatMap(URL.init(string:))) { phase in
            switch phase {
            case .empty:
                ZStack {
                    CleansiaColors.surfaceVariant
                    ProgressView()
                }
            case let .success(image):
                image.resizable().scaledToFill()
            case .failure:
                ZStack {
                    CleansiaColors.surfaceVariant
                    Image(systemName: "photo")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            @unknown default:
                CleansiaColors.surfaceVariant
            }
        }
        .frame(width: 80, height: 80)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }

    private var deleteButton: some View {
        Button(action: onDelete) {
            ZStack {
                Circle().fill(CleansiaColors.surface)
                if isDeleting {
                    ProgressView().scaleEffect(0.6)
                } else {
                    Image(systemName: "xmark")
                        .font(.system(size: 11, weight: .bold))
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
            .frame(width: 24, height: 24)
            .padding(4)
        }
        .buttonStyle(.plain)
        .disabled(isDeleting)
        .accessibilityLabel(L10n.Orders.deletePhoto)
    }
}

#if DEBUG
    struct PhotosSection_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                PhotoRailsContent(
                    photos: [
                        OrderPhoto(id: "1", photoType: ._1, blobUrl: nil),
                        OrderPhoto(id: "2", photoType: ._2, blobUrl: nil)
                    ],
                    mutation: PhotoMutationState(),
                    canUploadBefore: true,
                    canUploadAfter: true,
                    onPick: { _, _ in },
                    onDelete: { _ in }
                )
                .padding()
                .previewDisplayName("Active · both rails")

                PhotoRailsContent(
                    photos: [],
                    mutation: PhotoMutationState(),
                    canUploadBefore: false,
                    canUploadAfter: false,
                    onPick: { _, _ in },
                    onDelete: { _ in }
                )
                .padding()
                .previewDisplayName("Read-only · empty")
            }
            .background(CleansiaColors.surface)
        }
    }
#endif
