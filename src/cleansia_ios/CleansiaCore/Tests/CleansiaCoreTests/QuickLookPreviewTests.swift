#if canImport(UIKit)
    import XCTest
    @testable import CleansiaCore

    final class QuickLookPreviewTests: XCTestCase {
        func testRemoveFileDeletesAnExistingFile() throws {
            let url = FileManager.default.temporaryDirectory
                .appendingPathComponent("ql-test-\(UUID().uuidString).pdf")
            try Data("invoice".utf8).write(to: url)
            XCTAssertTrue(FileManager.default.fileExists(atPath: url.path))

            QuickLookPreview.removeFile(at: url)

            XCTAssertFalse(FileManager.default.fileExists(atPath: url.path))
        }

        func testRemoveFileIsNoOpForMissingFile() {
            let url = FileManager.default.temporaryDirectory
                .appendingPathComponent("ql-missing-\(UUID().uuidString).pdf")
            // Must not throw / crash when the file is already gone.
            QuickLookPreview.removeFile(at: url)
            XCTAssertFalse(FileManager.default.fileExists(atPath: url.path))
        }

        func testRemoveFileIgnoresNonFileURL() throws {
            // A real on-disk file exists; calling removeFile with a NON-file
            // (remote) URL must hit the isFileURL guard and leave disk untouched.
            let onDisk = FileManager.default.temporaryDirectory
                .appendingPathComponent("ql-guard-\(UUID().uuidString).pdf")
            try Data("invoice".utf8).write(to: onDisk)
            defer { try? FileManager.default.removeItem(at: onDisk) }

            guard let remote = URL(string: "https://example.com/x.pdf") else {
                return XCTFail("expected a valid URL")
            }
            QuickLookPreview.removeFile(at: remote)

            XCTAssertTrue(
                FileManager.default.fileExists(atPath: onDisk.path),
                "the on-disk file must survive — removeFile must skip non-file URLs"
            )
        }
    }
#endif
