import CleansiaCore
import SwiftUI
#if canImport(UIKit)
    import AVFoundation
    import UIKit
    import UniformTypeIdentifiers
#endif

extension View {
    /// The Add-evidence flow shared by the create form and the detail thread: a source dialog
    /// (Take Photo / Choose image / Choose PDF) over the Core `CameraOrLibraryPicker` plus a native
    /// PDF importer. Each pick lands in `onPicked` with the name its row shows.
    func evidencePicker(
        isPresented: Binding<Bool>,
        onPicked: @escaping (EvidenceSource, _ fileName: String) -> Void
    ) -> some View {
        modifier(EvidencePicker(isPresented: isPresented, onPicked: onPicked))
    }
}

private struct EvidencePicker: ViewModifier {
    @Binding var isPresented: Bool
    let onPicked: (EvidenceSource, String) -> Void

    @Environment(\.snackbarController) private var snackbar
    @State private var showImporter = false
    #if canImport(UIKit)
        @State private var pickerSource: UIImagePickerController.SourceType?
        @State private var showPermissionAlert = false
    #endif

    func body(content: Content) -> some View {
        content
            .confirmationDialog(
                L10n.Disputes.evidenceAddButton,
                isPresented: $isPresented,
                titleVisibility: .visible
            ) {
                Button(L10n.Disputes.addEvidenceTakePhoto, action: takePhoto)
                Button(L10n.Disputes.addEvidenceChooseImage, action: chooseImage)
                Button(L10n.Disputes.addEvidenceChoosePdf) { showImporter = true }
                Button(L10n.cancel, role: .cancel) {}
            }
            .fileImporter(
                isPresented: $showImporter,
                allowedContentTypes: pdfTypes,
                allowsMultipleSelection: false,
                onCompletion: importPdf
            )
        #if canImport(UIKit)
            .sheet(item: $pickerSource) { source in
                CameraOrLibraryPicker(
                    sourceType: source,
                    onImagePicked: { image in
                        pickerSource = nil
                        onPicked(.image(image), Self.photoFileName())
                    },
                    onCancel: { pickerSource = nil }
                )
                .ignoresSafeArea()
            }
            .alert(L10n.Disputes.cameraPermissionTitle, isPresented: $showPermissionAlert) {
                Button(L10n.Disputes.openSettings, action: openSettings)
                Button(L10n.cancel, role: .cancel) {}
            } message: {
                Text(L10n.Disputes.cameraPermissionMessage)
            }
        #endif
    }

    private var pdfTypes: [UTType] {
        #if canImport(UIKit)
            [.pdf]
        #else
            []
        #endif
    }

    private func importPdf(_ result: Result<[URL], Error>) {
        guard case let .success(urls) = result, let url = urls.first else { return }
        let accessed = url.startAccessingSecurityScopedResource()
        defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        guard let data = try? Data(contentsOf: url) else {
            snackbar.showError(L10n.Disputes.evidenceOpenError)
            return
        }
        onPicked(.pdf(data), url.lastPathComponent)
    }

    /// The camera and the library hand back pixels, not a file, so the row needs a name of its own.
    private static func photoFileName() -> String {
        "evidence-\(Int(Date().timeIntervalSince1970 * 1000)).jpg"
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
}

#if canImport(UIKit)
    extension UIImagePickerController.SourceType: Identifiable {
        public var id: Int {
            rawValue
        }
    }
#endif

struct AddEvidenceButton: View {
    var uploading = false
    var enabled = true
    let action: () -> Void

    var body: some View {
        CleansiaOutlinedButton(
            uploading ? L10n.Disputes.evidenceUploading : L10n.Disputes.evidenceAddButton,
            leadingIcon: "paperclip",
            enabled: enabled && !uploading,
            action: action
        )
    }
}
