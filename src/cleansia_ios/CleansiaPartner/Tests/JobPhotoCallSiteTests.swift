import XCTest

final class JobPhotoCallSiteTests: XCTestCase {
    private static let photosSection = "CleansiaPartner/Sources/Features/Orders/PhotosSection.swift"
    private static let detailContent = "CleansiaPartner/Sources/Features/Orders/OrderDetailContent.swift"

    func testJobPhotosComeFromTheCameraOnly() throws {
        let source = try read(Self.photosSection)
        XCTAssertFalse(
            source.contains(".photoLibrary"),
            "the job-photo rails must not offer the photo library — a gallery photo of a customer's home "
                + "is already a copy on the device"
        )
        XCTAssertTrue(
            source.contains("sourceType: .camera"),
            "the job-photo picker must open the camera — a gallery photo is already a copy on the device"
        )
        XCTAssertTrue(
            source.contains("UIImagePickerController.isSourceTypeAvailable(.camera)"),
            "a device without a camera must be told why it cannot add a photo, not handed the library instead"
        )
    }

    func testJobPhotoTilesNeverReachADiskCache() throws {
        let source = try read(Self.photosSection)
        XCTAssertTrue(
            source.contains("CachedRemoteImage("),
            "the tiles must load through RemoteImageCache, whose ephemeral session keeps the photo and "
                + "its signed URL in memory"
        )
        XCTAssertFalse(
            source.contains("AsyncImage("),
            "AsyncImage loads through URLSession.shared and its URLCache, which can persist the photo of a "
                + "customer's home to Library/Caches"
        )
    }

    func testTheRailsOpenAndCloseOnTheServerPhotoWindows() throws {
        let source = try read(Self.detailContent)
        XCTAssertTrue(
            source.contains("canUploadBefore: order.photoWindowOpen(for: ._1)"),
            "the before rail must follow OrderPhoto.MayBeAddedAt through photoWindowOpen — a local status "
                + "list closed it at Confirmed while the server still accepts before photos there"
        )
        XCTAssertTrue(
            source.contains("canUploadAfter: order.photoWindowOpen(for: ._2)"),
            "the after rail must follow OrderPhoto.MayBeAddedAt through photoWindowOpen"
        )
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
