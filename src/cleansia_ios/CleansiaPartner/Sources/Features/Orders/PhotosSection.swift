import AVFoundation
import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The job's photos: *Before · 2* and *After · 0* in two fixed columns, so After starts at the same place on every
/// state; each photo with the time it was taken. A rail whose server window is open ends in the dashed Add slot; the
/// empty after slot says it is needed before she can complete. Kept on a finished job, read-only.
struct DetailPhotos: View {
    let photos: [OrderPhoto]
    let canUploadBefore: Bool
    let canUploadAfter: Bool
    var afterSlotNote = false
    @Binding var afterPhotoRequested: Bool
    @ObservedObject var photosVM: OrderPhotosViewModel
    @StateObject private var imageCache = RemoteImageCache()

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            DetailEyebrow(text: L10n.Orders.photosSectionTitle)
            HStack(alignment: .top, spacing: Spacing.s) {
                before.frame(maxWidth: .infinity, alignment: .leading)
                after.frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }

    private var before: some View {
        PhotoColumn(
            title: L10n.Orders.photosBefore,
            type: ._1,
            photos: photos.filter { $0.photoType == ._1 },
            canUpload: canUploadBefore,
            mutation: photosVM.mutation,
            imageCache: imageCache,
            request: .constant(false),
            onPick: pick,
            onDelete: delete
        )
    }

    private var after: some View {
        PhotoColumn(
            title: L10n.Orders.photosAfter,
            type: ._2,
            photos: photos.filter { $0.photoType == ._2 },
            canUpload: canUploadAfter,
            slotNote: afterSlotNote ? L10n.Orders.photosAfterNeeded : nil,
            mutation: photosVM.mutation,
            imageCache: imageCache,
            request: $afterPhotoRequested,
            onPick: pick,
            onDelete: delete
        )
    }

    private func pick(_ type: PhotoType, _ image: UIImage) {
        Task { await photosVM.upload(type: type, image: image) }
    }

    private func delete(_ id: String) {
        Task { await photosVM.delete(photoId: id) }
    }
}

/// One kind of photo: "Before · 2", the photos with their times, and while the server takes more the dashed Add
/// slot. The camera only: a gallery photo of a customer's home is already a copy on the device.
struct PhotoColumn: View {
    @Environment(\.locale) private var locale
    let title: String
    let type: PhotoType
    let photos: [OrderPhoto]
    let canUpload: Bool
    var slotNote: String?
    let mutation: PhotoMutationState
    let imageCache: RemoteImageCache
    @Binding var request: Bool
    var onPick: (PhotoType, UIImage) -> Void
    var onDelete: (String) -> Void

    @State private var showCamera = false
    @State private var showPermissionAlert = false
    @State private var showNoCameraAlert = false

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(verbatim: "\(title)\(OrdersBoardRules.dot)\(photos.count)")
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            LazyVGrid(
                columns: [GridItem(.fixed(64), spacing: Spacing.xs), GridItem(.fixed(64))],
                alignment: .leading,
                spacing: Spacing.xs
            ) {
                ForEach(photos) { photo in
                    VStack(spacing: Spacing.hair) {
                        PhotoTile(
                            photo: photo,
                            imageCache: imageCache,
                            isDeleting: mutation.deletingId == photo.id,
                            isReadOnly: !canUpload,
                            onDelete: { onDelete(photo.id) }
                        )
                        if let captured = photo.capturedAt {
                            Text(OrdersFormat.timeOnly(captured, locale: locale))
                                .font(CleansiaTypography.labelMedium)
                                .monospacedDigit()
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        }
                    }
                }
                if canUpload {
                    AddPhotoSlot(isUploading: mutation.isUploading, onTap: openCamera)
                }
            }
            .frame(width: 136, alignment: .leading)
            if canUpload, photos.isEmpty, let slotNote {
                Text(L10n.tie(slotNote))
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .onChange(of: request) { asked in
            guard asked else { return }
            request = false
            openCamera()
        }
        .sheet(isPresented: $showCamera) {
            CameraOrLibraryPicker(
                sourceType: .camera,
                onImagePicked: { image in
                    showCamera = false
                    onPick(type, image)
                },
                onCancel: { showCamera = false }
            )
            .ignoresSafeArea()
        }
        .alert(L10n.Orders.cameraPermissionTitle, isPresented: $showPermissionAlert) {
            Button(L10n.Orders.openSettings) { openSettings() }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.Orders.cameraPermissionMessage)
        }
        .alert(L10n.Orders.cameraUnavailable, isPresented: $showNoCameraAlert) {
            Button(L10n.close, role: .cancel) {}
        } message: {
            Text(L10n.Orders.jobPhotosCameraOnly)
        }
    }

    private func openCamera() {
        guard UIImagePickerController.isSourceTypeAvailable(.camera) else {
            showNoCameraAlert = true
            return
        }
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            showCamera = true
        case .notDetermined:
            AVCaptureDevice.requestAccess(for: .video) { granted in
                Task { @MainActor in
                    if granted { showCamera = true } else { showPermissionAlert = true }
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

/// The one dashed box: a camera and *Add*.
private struct AddPhotoSlot: View {
    let isUploading: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            ZStack {
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .strokeBorder(CleansiaColors.quiet, style: StrokeStyle(lineWidth: 1.5, dash: [4, 3]))
                if isUploading {
                    ProgressView().tint(CleansiaColors.primaryText)
                } else {
                    VStack(spacing: Spacing.hair) {
                        Image(systemName: "camera").cleansiaFont(CleansiaTypography.scale.titleLarge)
                        Text(L10n.Orders.photosAdd).font(CleansiaTypography.labelLarge)
                    }
                    .foregroundColor(CleansiaColors.primaryText)
                }
            }
            .frame(width: 64, height: 64)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(isUploading)
        .accessibilityLabel(L10n.Orders.addPhoto)
    }
}

private struct PhotoTile: View {
    let photo: OrderPhoto
    let imageCache: RemoteImageCache
    let isDeleting: Bool
    let isReadOnly: Bool
    let onDelete: () -> Void

    @Environment(\.locale) private var locale
    @State private var loadFailed = false
    @State private var confirmsDeletion = false

    var body: some View {
        ZStack(alignment: .topTrailing) {
            thumbnail
            if !isReadOnly {
                deleteButton
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(photo.capturedAt.map { OrdersFormat.timeOnly($0, locale: locale) } ?? "")
        .alert(L10n.Orders.deletePhoto, isPresented: $confirmsDeletion) {
            Button(L10n.Orders.deletePhoto, role: .destructive) {
                guard !isReadOnly, !isDeleting else { return }
                onDelete()
            }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.Orders.deletePhotoConfirmation)
        }
    }

    private var thumbnail: some View {
        Group {
            if let url = photo.blobUrl.flatMap(URL.init(string:)) {
                CachedRemoteImage(
                    cacheKey: photo.id,
                    url: url,
                    cache: imageCache,
                    onLoadFailure: { loadFailed = true },
                    onLoadSuccess: { loadFailed = false },
                    placeholder: { placeholder }
                )
            } else {
                unavailable
            }
        }
        .frame(width: 64, height: 64)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
    }

    @ViewBuilder
    private var placeholder: some View {
        if loadFailed {
            unavailable
        } else {
            ZStack {
                CleansiaColors.surfaceVariant
                ProgressView()
            }
        }
    }

    private var unavailable: some View {
        ZStack {
            CleansiaColors.surfaceVariant
            Image(systemName: "photo")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    private var deleteButton: some View {
        Button { confirmsDeletion = true } label: {
            ZStack {
                Circle().fill(CleansiaColors.surface)
                if isDeleting {
                    ProgressView().scaleEffect(0.6)
                } else {
                    Image(systemName: "xmark")
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
            .frame(width: 22, height: 22)
            .padding(3)
        }
        .buttonStyle(.plain)
        .disabled(isDeleting)
        .accessibilityLabel(L10n.Orders.deletePhoto)
    }
}
