import Foundation

/// The stored avatar. `fileName` is the content-addressed blob name — the backend mints a fresh one
/// on every upload — so it is the image's identity and its cache key. `blobURL` is a SAS link that is
/// re-signed on every fetch and expires within the hour: it is a credential, never persisted and
/// never used to key anything.
///
/// The URL is optional because it is only how the image is fetched, never whether it exists: a fetch
/// that returns the name without a signature still describes a photo the account holds and can delete.
public struct ProfilePhoto: Equatable {
    public let fileName: String
    public let blobURL: URL?

    public init(fileName: String, blobURL: URL?) {
        self.fileName = fileName
        self.blobURL = blobURL
    }
}

public struct ProfilePhotoUpload: Equatable {
    public let base64: String
    public let contentType: String
    public let fileName: String

    public init(base64: String, contentType: String, fileName: String) {
        self.base64 = base64
        self.contentType = contentType
        self.fileName = fileName
    }
}
