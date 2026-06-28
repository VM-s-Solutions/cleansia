#if canImport(UIKit)
    import QuickLook
    import SwiftUI
    import UIKit

    /// QuickLook preview for an on-disk file, the second member of the
    /// imperative-UIKit-behind-a-SwiftUI-seam family (alongside
    /// `CameraOrLibraryPicker`). Present it from a `.sheet`/`.fullScreenCover`
    /// with a local file `URL`.
    ///
    /// E4: a previewed file may be PII-bearing (an invoice PDF carries bank /
    /// payment details). When `deleteOnDismiss` is set, the coordinator removes
    /// the file from disk on dismissal so it never stays resident in caches/temp.
    public struct QuickLookPreview: UIViewControllerRepresentable {
        private let url: URL
        private let deleteOnDismiss: Bool
        private let onDismiss: () -> Void

        public init(
            url: URL,
            deleteOnDismiss: Bool = true,
            onDismiss: @escaping () -> Void = {}
        ) {
            self.url = url
            self.deleteOnDismiss = deleteOnDismiss
            self.onDismiss = onDismiss
        }

        public func makeUIViewController(context: Context) -> QLPreviewController {
            let controller = QLPreviewController()
            controller.dataSource = context.coordinator
            controller.delegate = context.coordinator
            return controller
        }

        public func updateUIViewController(_: QLPreviewController, context _: Context) {}

        public func makeCoordinator() -> Coordinator {
            Coordinator(url: url, deleteOnDismiss: deleteOnDismiss, onDismiss: onDismiss)
        }

        public final class Coordinator: NSObject, QLPreviewControllerDataSource, QLPreviewControllerDelegate {
            private let url: URL
            private let deleteOnDismiss: Bool
            private let onDismiss: () -> Void

            init(url: URL, deleteOnDismiss: Bool, onDismiss: @escaping () -> Void) {
                self.url = url
                self.deleteOnDismiss = deleteOnDismiss
                self.onDismiss = onDismiss
            }

            public func numberOfPreviewItems(in _: QLPreviewController) -> Int {
                1
            }

            public func previewController(_: QLPreviewController, previewItemAt _: Int) -> QLPreviewItem {
                url as QLPreviewItem
            }

            public func previewControllerWillDismiss(_: QLPreviewController) {
                if deleteOnDismiss {
                    QuickLookPreview.removeFile(at: url)
                }
                onDismiss()
            }
        }

        /// Best-effort delete of a previewed file. Exposed so callers that present
        /// without QuickLook (or need to clean up on a cancelled present) can run
        /// the same E4 cleanup.
        public static func removeFile(at url: URL) {
            guard url.isFileURL else { return }
            try? FileManager.default.removeItem(at: url)
        }
    }
#endif
