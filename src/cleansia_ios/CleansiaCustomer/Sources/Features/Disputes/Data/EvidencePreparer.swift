#if canImport(UIKit)
    import CleansiaCore
    import Foundation
    import UIKit

    /// A prepared evidence file on disk, ready for one multipart upload. The temp
    /// URL's extension drives the multipart MIME (`.jpg` → image/jpeg, `.pdf` →
    /// application/pdf). `byteCount`/`contentType` feed the fail-closed validator.
    /// The caller MUST `cleanUp()` after the upload (success or failure).
    struct PreparedEvidence: Equatable {
        let url: URL
        let contentType: String
        let byteCount: Int

        func cleanUp() {
            try? FileManager.default.removeItem(at: url)
        }
    }

    enum EvidencePreparationError: Error, Equatable {
        case rejected(EvidenceRejection)
        case encodingFailed
        case ioFailed
    }

    /// Turns a picked source into a temp file ready for `disputeUploadEvidence`:
    ///  - image → `ImageCompressor.encode` (1920 / 0.7 + EXPLICIT EXIF/GPS strip)
    ///    → write a temp `.jpg`;
    ///  - PDF   → copy the bytes to a temp `.pdf`.
    /// Validation is fail-closed and runs on the FINAL bytes (after compression
    /// for images) — an oversize/wrong-type file is rejected before any temp file
    /// is left around (Gate-SEC R11). The blob name is server-controlled; only the
    /// extension travels (Gate-SEC R12).
    enum EvidencePreparer {
        static func prepareImage(
            _ image: UIImage,
            directory: URL = FileManager.default.temporaryDirectory
        ) -> Result<PreparedEvidence, EvidencePreparationError> {
            guard let encoded = ImageCompressor.encode(image),
                  let data = Data(base64Encoded: encoded.base64)
            else {
                return .failure(.encodingFailed)
            }
            if let rejection = EvidenceFileValidator.validate(
                byteCount: data.count,
                contentType: encoded.contentType
            ) {
                return .failure(.rejected(rejection))
            }
            return write(data, ext: "jpg", contentType: encoded.contentType, into: directory)
        }

        static func preparePdf(
            _ data: Data,
            directory: URL = FileManager.default.temporaryDirectory
        ) -> Result<PreparedEvidence, EvidencePreparationError> {
            let contentType = "application/pdf"
            if let rejection = EvidenceFileValidator.validate(byteCount: data.count, contentType: contentType) {
                return .failure(.rejected(rejection))
            }
            return write(data, ext: "pdf", contentType: contentType, into: directory)
        }

        private static func write(
            _ data: Data,
            ext: String,
            contentType: String,
            into directory: URL
        ) -> Result<PreparedEvidence, EvidencePreparationError> {
            let url = directory.appendingPathComponent("dispute-evidence-\(UUID().uuidString).\(ext)")
            do {
                try data.write(to: url, options: .atomic)
            } catch {
                return .failure(.ioFailed)
            }
            return .success(PreparedEvidence(url: url, contentType: contentType, byteCount: data.count))
        }
    }
#endif
