import CleansiaCore
import SwiftUI
#if canImport(UIKit)
    import AVFoundation
    import UIKit
    import UniformTypeIdentifiers
#endif

struct FullscreenImageURL: Identifiable {
    let url: URL
    var id: String {
        url.absoluteString
    }
}

struct EvidencePdfPreview: Identifiable {
    let url: URL
    var id: String {
        url.path
    }
}

struct DisputeDetailView: View {
    @StateObject private var vm: DisputeDetailViewModel
    @Environment(\.snackbarController) private var snackbar

    @State private var draft = ""
    @State private var showSourceDialog = false
    @State private var showImporter = false
    #if canImport(UIKit)
        @State private var pickerSource: UIImagePickerController.SourceType?
        @State private var showPermissionAlert = false
    #endif
    @State private var fullscreenImage: FullscreenImageURL?
    @State private var pdfPreview: EvidencePdfPreview?

    init(disputeId: String, repository: DisputeRepository, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: DisputeDetailViewModel(
            disputeId: disputeId,
            repository: repository,
            snackbar: snackbar
        ))
    }

    var body: some View {
        VStack(spacing: 0) {
            content
            replyBar
        }
        .navigationTitle(L10n.Disputes.detailTitle)
        .navigationBarTitleDisplayMode(.inline)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.load() }
        .modifier(EvidencePickers(
            showSourceDialog: $showSourceDialog,
            showImporter: $showImporter,
            onTakePhoto: takePhoto,
            onChooseImage: chooseImage,
            onChoosePdf: { showImporter = true },
            onImportPdf: handleImport
        ))
        #if canImport(UIKit)
        .sheet(item: $pickerSource) { source in
            CameraOrLibraryPicker(
                sourceType: source,
                onImagePicked: { image in
                    pickerSource = nil
                    Task { await vm.uploadEvidence([.image(image)]) }
                },
                onCancel: { pickerSource = nil }
            )
            .ignoresSafeArea()
        }
        .alert(L10n.Disputes.cameraPermissionTitle, isPresented: $showPermissionAlert) {
            Button(L10n.Disputes.openSettings) { openSettings() }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(L10n.Disputes.cameraPermissionMessage)
        }
        #endif
        .fullScreenCover(item: $fullscreenImage) { item in
            FullscreenSingleImage(url: item.url) { fullscreenImage = nil }
        }
        .sheet(item: $pdfPreview) { item in
            pdfPreviewView(item.url)
        }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            DisputeDetailErrorView { Task { await vm.load() } }
        case let .loaded(detail):
            DisputeThread(
                detail: detail,
                uploading: vm.uploadState.isSubmitting,
                onAddEvidence: { showSourceDialog = true },
                onImageTap: openImage,
                onPdfTap: openPdf,
                onUnknownTap: { _ in snackbar.showError(L10n.Disputes.evidenceOpenError) }
            )
        }
    }

    @ViewBuilder
    private var replyBar: some View {
        if case let .loaded(detail) = vm.state {
            if detail.allowsMessages {
                ReplyInputBar(
                    draft: $draft,
                    sending: vm.sendState.isSubmitting,
                    onSend: {
                        let body = draft
                        draft = ""
                        Task { await vm.sendMessage(body) }
                    }
                )
            } else {
                Text(L10n.Disputes.detailClosedNote)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity)
                    .padding(Spacing.m)
                    .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
            }
        }
    }

    private func openImage(_ evidence: DisputeEvidence) {
        guard let url = evidence.blobURL.flatMap(URL.init(string:)) else {
            snackbar.showError(L10n.Disputes.evidenceOpenError)
            return
        }
        fullscreenImage = FullscreenImageURL(url: url)
    }

    private func openPdf(_ evidence: DisputeEvidence) {
        guard let url = evidence.blobURL.flatMap(URL.init(string:)) else {
            snackbar.showError(L10n.Disputes.evidenceOpenError)
            return
        }
        pdfPreview = EvidencePdfPreview(url: url)
    }

    @ViewBuilder
    private func pdfPreviewView(_ url: URL) -> some View {
        #if canImport(UIKit)
            QuickLookPreview(url: url, deleteOnDismiss: false) { pdfPreview = nil }
                .ignoresSafeArea()
        #else
            EmptyView()
        #endif
    }

    #if canImport(UIKit)
        private func takePhoto() {
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

        private func chooseImage() {
            pickerSource = .photoLibrary
        }

        private func openSettings() {
            guard let url = URL(string: UIApplication.openSettingsURLString) else { return }
            UIApplication.shared.open(url)
        }
    #else
        private func takePhoto() {}
        private func chooseImage() {}
    #endif

    private func handleImport(_ result: Result<[URL], Error>) {
        guard case let .success(urls) = result, let url = urls.first else { return }
        let accessed = url.startAccessingSecurityScopedResource()
        defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        guard let data = try? Data(contentsOf: url) else {
            snackbar.showError(L10n.Disputes.evidenceOpenError)
            return
        }
        Task { await vm.uploadEvidence([.pdf(data)]) }
    }
}

#if canImport(UIKit)
    extension UIImagePickerController.SourceType: Identifiable {
        public var id: Int {
            rawValue
        }
    }
#endif
