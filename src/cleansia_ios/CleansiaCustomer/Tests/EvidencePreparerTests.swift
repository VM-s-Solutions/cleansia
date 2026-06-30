#if canImport(UIKit)
    import ImageIO
    import UIKit
    import UniformTypeIdentifiers
    import XCTest
    @testable import CleansiaCustomer

    final class EvidencePreparerTests: XCTestCase {
        private func tempDir() -> URL {
            let dir = FileManager.default.temporaryDirectory
                .appendingPathComponent("evidence-tests-\(UUID().uuidString)")
            try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            return dir
        }

        // MARK: - Image path

        func testPrepareImageWritesJpegTempFileWithCorrectExtensionAndType() throws {
            let dir = tempDir()
            let result = EvidencePreparer.prepareImage(solidImage(width: 800, height: 600), directory: dir)
            let prepared = try XCTUnwrap(result.success)

            XCTAssertEqual(prepared.url.pathExtension, "jpg")
            XCTAssertEqual(prepared.contentType, "image/jpeg")
            XCTAssertTrue(FileManager.default.fileExists(atPath: prepared.url.path))
            XCTAssertGreaterThan(prepared.byteCount, 0)
            prepared.cleanUp()
            XCTAssertFalse(FileManager.default.fileExists(atPath: prepared.url.path))
        }

        func testPreparedImageHasNoGpsMetadata() throws {
            let dir = tempDir()
            let gpsTagged = try gpsTaggedJpegData(width: 1200, height: 900)
            let sourceProps = try imageProperties(of: gpsTagged)
            XCTAssertNotNil(sourceProps[kCGImagePropertyGPSDictionary as String]) // sanity

            let image = try XCTUnwrap(UIImage(data: gpsTagged))
            let prepared = try XCTUnwrap(EvidencePreparer.prepareImage(image, directory: dir).success)
            defer { prepared.cleanUp() }

            let output = try Data(contentsOf: prepared.url)
            let outputProps = try imageProperties(of: output)
            XCTAssertNil(outputProps[kCGImagePropertyGPSDictionary as String])
        }

        func testPrepareImageEncodingFailureReturnsError() {
            let result = EvidencePreparer.prepareImage(UIImage(), directory: tempDir())
            XCTAssertEqual(result.failureValue, .encodingFailed)
        }

        // MARK: - PDF path

        func testPreparePdfWritesPdfTempFileWithCorrectExtension() throws {
            let dir = tempDir()
            let result = EvidencePreparer.preparePdf(Data("%PDF-1.4".utf8), directory: dir)
            let prepared = try XCTUnwrap(result.success)

            XCTAssertEqual(prepared.url.pathExtension, "pdf")
            XCTAssertEqual(prepared.contentType, "application/pdf")
            XCTAssertTrue(FileManager.default.fileExists(atPath: prepared.url.path))
            prepared.cleanUp()
            XCTAssertFalse(FileManager.default.fileExists(atPath: prepared.url.path))
        }

        func testPreparePdfRejectsOversizeBeforeWritingFile() throws {
            let dir = tempDir()
            let tooBig = Data(count: DisputeFormConstants.maxEvidenceBytes + 1)
            let result = EvidencePreparer.preparePdf(tooBig, directory: dir)

            XCTAssertEqual(result.failureValue, .rejected(.tooLarge))
            let contents = try FileManager.default.contentsOfDirectory(atPath: dir.path)
            XCTAssertTrue(contents.isEmpty) // fail-closed: nothing written
        }

        // MARK: - Helpers

        private func solidImage(width: Int, height: Int) -> UIImage {
            let size = CGSize(width: width, height: height)
            let format = UIGraphicsImageRendererFormat.default()
            format.scale = 1
            return UIGraphicsImageRenderer(size: size, format: format).image { context in
                UIColor.systemTeal.setFill()
                context.fill(CGRect(origin: .zero, size: size))
            }
        }

        private func imageProperties(of data: Data) throws -> [String: Any] {
            let source = try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
            return try XCTUnwrap(CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [String: Any])
        }

        private func gpsTaggedJpegData(width: Int, height: Int) throws -> Data {
            let base = try XCTUnwrap(solidImage(width: width, height: height).cgImage)
            let output = NSMutableData()
            let type = UTType.jpeg.identifier as CFString
            let destination = try XCTUnwrap(CGImageDestinationCreateWithData(output, type, 1, nil))
            let gps: [CFString: Any] = [
                kCGImagePropertyGPSLatitude: 50.0755,
                kCGImagePropertyGPSLatitudeRef: "N",
                kCGImagePropertyGPSLongitude: 14.4378,
                kCGImagePropertyGPSLongitudeRef: "E"
            ]
            CGImageDestinationAddImage(destination, base, [kCGImagePropertyGPSDictionary: gps] as CFDictionary)
            XCTAssertTrue(CGImageDestinationFinalize(destination))
            return output as Data
        }
    }

    private extension Result {
        var success: Success? {
            if case let .success(value) = self { return value }
            return nil
        }

        var failureValue: Failure? {
            if case let .failure(error) = self { return error }
            return nil
        }
    }
#endif
