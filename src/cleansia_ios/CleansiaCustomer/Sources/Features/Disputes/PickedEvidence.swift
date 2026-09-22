import Foundation

enum EvidenceUploadState: Equatable {
    case pending
    case uploading
    case uploaded
    case failed
}

/// A file the customer attached before the dispute exists: prepared on pick (compressed, validated,
/// written to a temp file) and uploaded to the dispute the create call returns.
struct PickedEvidence: Identifiable, Equatable {
    let id: String
    let fileName: String
    let file: PreparedEvidence
    var upload: EvidenceUploadState = .pending

    var isPdf: Bool {
        file.contentType.caseInsensitiveCompare("application/pdf") == .orderedSame
    }
}
