import XCTest

/// Upload and Replace ask where the document comes from (a photo taken now, one from the library, or a
/// file) before the type dialog. A dialog's buttons are not observable in a unit test, so this reads the
/// source, the `JobPhotoCallSiteTests` idiom.
final class DocumentSourceCallSiteTests: XCTestCase {
    private static let view = "CleansiaPartner/Sources/Features/Profile/Documents/DocumentsSectionView.swift"

    func testUploadAndReplaceOpenTheSourceChoiceRatherThanTheFilesBrowser() throws {
        let source = try read(Self.view)

        XCTAssertEqual(occurrences(of: "choosingSource = true", in: source), 2, "Upload and Replace ask first")
        XCTAssertEqual(occurrences(of: "importerOpen = true", in: source), 1, "only Choose File opens Files")
    }

    func testTheChoiceOffersTheCameraTheLibraryAndAFile() throws {
        let source = try read(Self.view)

        XCTAssertTrue(source.contains("UIImagePickerController.isSourceTypeAvailable(.camera)"))
        XCTAssertTrue(source.contains("AVCaptureDevice.authorizationStatus(for: .video)"))
        XCTAssertTrue(source.contains("pickerSource = .photoLibrary"))
        XCTAssertTrue(source.contains("CameraOrLibraryPicker("))
        XCTAssertTrue(source.contains("Button(L10n.Profile.documentChooseFile)"))
        XCTAssertTrue(source.contains("allowedContentTypes: DocumentPresentation.importableTypes"))
    }

    /// A cancelled camera or library pick leaves no Replace target behind for the next plain upload.
    func testThePickerSheetConsumesTheReplaceTargetOnEveryDismissal() throws {
        let source = try read(Self.view)

        XCTAssertTrue(source.contains(".sheet(item: $pickerSource, onDismiss: stagePickedPhoto)"))
        let staging = try XCTUnwrap(source.range(of: "private func stagePickedPhoto()"))
        let body = source[staging.upperBound...].prefix(240)
        XCTAssertTrue(body.contains("replacingDocumentId = nil"), String(body))
    }

    private func occurrences(of needle: String, in source: String) -> Int {
        source.components(separatedBy: needle).count - 1
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
