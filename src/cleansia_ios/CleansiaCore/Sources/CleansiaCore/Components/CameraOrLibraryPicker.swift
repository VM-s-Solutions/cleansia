#if canImport(UIKit)
    import SwiftUI
    import UIKit

    /// The canonical "imperative-UIKit-controller-behind-a-SwiftUI-seam" idiom:
    /// a `UIViewControllerRepresentable` over `UIImagePickerController` for photo
    /// capture (camera or library). Parameterized by `sourceType` so the caller
    /// chooses the source; this seam only bridges UIKit's delegate callbacks back
    /// to SwiftUI closures. Present it from a `.sheet`/`.fullScreenCover`.
    ///
    /// `UIImagePickerController` is the one API that drives both camera and
    /// library, keeping the partner's single Add affordance at parity. Camera
    /// access needs `NSCameraUsageDescription`; the library needs
    /// `NSPhotoLibraryUsageDescription` — without them the controller silently
    /// denies and the prompt never appears.
    public struct CameraOrLibraryPicker: UIViewControllerRepresentable {
        private let sourceType: UIImagePickerController.SourceType
        private let onImagePicked: (UIImage) -> Void
        private let onCancel: () -> Void

        public init(
            sourceType: UIImagePickerController.SourceType,
            onImagePicked: @escaping (UIImage) -> Void,
            onCancel: @escaping () -> Void = {}
        ) {
            self.sourceType = CameraOrLibraryPicker.resolvedSourceType(sourceType)
            self.onImagePicked = onImagePicked
            self.onCancel = onCancel
        }

        /// Falls back to the photo library whenever the requested source is the
        /// camera but no camera is available (simulator, locked-down devices).
        public static func resolvedSourceType(
            _ requested: UIImagePickerController.SourceType
        ) -> UIImagePickerController.SourceType {
            if requested == .camera, !UIImagePickerController.isSourceTypeAvailable(.camera) {
                return .photoLibrary
            }
            return requested
        }

        public func makeUIViewController(context: Context) -> UIImagePickerController {
            let controller = UIImagePickerController()
            controller.sourceType = sourceType
            controller.delegate = context.coordinator
            controller.allowsEditing = false
            return controller
        }

        public func updateUIViewController(_: UIImagePickerController, context _: Context) {}

        public func makeCoordinator() -> Coordinator {
            Coordinator(onImagePicked: onImagePicked, onCancel: onCancel)
        }

        public final class Coordinator: NSObject, UIImagePickerControllerDelegate, UINavigationControllerDelegate {
            private let onImagePicked: (UIImage) -> Void
            private let onCancel: () -> Void

            init(onImagePicked: @escaping (UIImage) -> Void, onCancel: @escaping () -> Void) {
                self.onImagePicked = onImagePicked
                self.onCancel = onCancel
            }

            public func imagePickerController(
                _: UIImagePickerController,
                didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]
            ) {
                if let image = info[.originalImage] as? UIImage {
                    onImagePicked(image)
                } else {
                    onCancel()
                }
            }

            public func imagePickerControllerDidCancel(_: UIImagePickerController) {
                onCancel()
            }
        }
    }
#endif
