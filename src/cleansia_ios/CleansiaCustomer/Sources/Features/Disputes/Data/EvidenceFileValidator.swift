import Foundation

enum EvidenceRejection: Equatable {
    case tooLarge
    case unsupportedType
}

/// Pure, fail-closed client validation mirroring the backend
/// `UploadDisputeEvidenceCommand` validator EXACTLY (Gate-SEC R11): the size cap
/// is checked AFTER compression against `MaxFileSize` (10 MiB), the content type
/// must be in the accepted set. Anything else is rejected BEFORE the network
/// call. An unknown/empty content type fails closed (`.unsupportedType`).
enum EvidenceFileValidator {
    static func validate(byteCount: Int, contentType: String) -> EvidenceRejection? {
        if byteCount > DisputeFormConstants.maxEvidenceBytes {
            return .tooLarge
        }
        if !DisputeFormConstants.allowedEvidenceContentTypes.contains(contentType.lowercased()) {
            return .unsupportedType
        }
        return nil
    }
}
