import Foundation

enum DisputeFormConstants {
    static let descriptionMinLength = 10
    static let descriptionMaxLength = 2000
    static let messageMaxLength = 2000

    /// Mirrors the backend `UploadDisputeEvidenceCommand` validator exactly: the
    /// 10 MiB cap is checked AFTER compression, the type set is the accepted
    /// MIME whitelist. Both are FAIL-CLOSED on the client (Gate-SEC R11) — a
    /// doomed request is rejected before the network with a localized error.
    static let maxEvidenceBytes = 10 * 1024 * 1024
    static let allowedEvidenceContentTypes: Set<String> = [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    ]
}
