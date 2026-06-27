#if canImport(UIKit)
    import ImageIO
    import UIKit
    import UniformTypeIdentifiers
    import XCTest
    @testable import CleansiaCore

    final class ImageCompressorTests: XCTestCase {
        // MARK: TC-IOS-IMG-COMPRESS

        func testDownscalesLongestSideToMaxDimensionPreservingAspect() throws {
            let image = solidImage(width: 4000, height: 2000)

            let encoded = try XCTUnwrap(ImageCompressor.encode(image, maxDimension: 1920, quality: 0.7))
            let decoded = try XCTUnwrap(decodeJpeg(base64: encoded.base64))

            let width = decoded.size.width * decoded.scale
            let height = decoded.size.height * decoded.scale
            XCTAssertEqual(max(width, height), 1920, accuracy: 1)
            XCTAssertEqual(width / height, 2.0, accuracy: 0.01) // 4000:2000 preserved
        }

        func testDoesNotUpscaleSmallerImage() throws {
            let image = solidImage(width: 800, height: 600)

            let encoded = try XCTUnwrap(ImageCompressor.encode(image, maxDimension: 1920, quality: 0.7))
            let decoded = try XCTUnwrap(decodeJpeg(base64: encoded.base64))

            let width = decoded.size.width * decoded.scale
            let height = decoded.size.height * decoded.scale
            XCTAssertEqual(width, 800, accuracy: 1)
            XCTAssertEqual(height, 600, accuracy: 1)
        }

        func testBase64IsValidAndRoundTripsToDecodableJpeg() throws {
            let image = solidImage(width: 1000, height: 1000)

            let encoded = try XCTUnwrap(ImageCompressor.encode(image))
            let data = try XCTUnwrap(Data(base64Encoded: encoded.base64))
            XCTAssertFalse(data.isEmpty)

            let source = try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
            let utType = CGImageSourceGetType(source) as String?
            XCTAssertEqual(utType, UTType.jpeg.identifier)
            XCTAssertNotNil(CGImageSourceCreateImageAtIndex(source, 0, nil))
        }

        func testContentTypeAndFileNameAreJpeg() throws {
            let encoded = try XCTUnwrap(ImageCompressor.encode(solidImage(width: 100, height: 100)))
            XCTAssertEqual(encoded.contentType, "image/jpeg")
            XCTAssertEqual(encoded.fileName, "photo.jpg")
        }

        func testDegenerateImageReturnsNilWithoutCrashing() {
            XCTAssertNil(ImageCompressor.encode(UIImage()))
            XCTAssertNil(ImageCompressor.encode(solidImage(width: 100, height: 100), maxDimension: 0))
        }

        // MARK: TC-IOS-PHOTOS-EXIF-STRIP (security P3)

        func testEncodedOutputHasNoGpsOrExifMetadata() throws {
            let gpsTagged = try gpsTaggedJpegData(width: 1200, height: 900)

            // Sanity: the source genuinely carries GPS so the assertion is meaningful.
            let sourceProps = try imageProperties(of: gpsTagged)
            XCTAssertNotNil(sourceProps[kCGImagePropertyGPSDictionary as String])

            let image = try XCTUnwrap(UIImage(data: gpsTagged))
            let encoded = try XCTUnwrap(ImageCompressor.encode(image))
            let outputData = try XCTUnwrap(Data(base64Encoded: encoded.base64))

            let outputProps = try imageProperties(of: outputData)
            XCTAssertNil(outputProps[kCGImagePropertyGPSDictionary as String])

            if let exif = outputProps[kCGImagePropertyExifDictionary as String] as? [String: Any] {
                XCTAssertNil(exif["GPSLatitude"])
                XCTAssertNil(exif["GPSLongitude"])
            }
        }

        // MARK: Helpers

        private func solidImage(width: Int, height: Int) -> UIImage {
            let size = CGSize(width: width, height: height)
            let format = UIGraphicsImageRendererFormat.default()
            format.scale = 1
            let renderer = UIGraphicsImageRenderer(size: size, format: format)
            return renderer.image { context in
                UIColor.systemTeal.setFill()
                context.fill(CGRect(origin: .zero, size: size))
            }
        }

        private func decodeJpeg(base64: String) -> UIImage? {
            Data(base64Encoded: base64).flatMap { UIImage(data: $0) }
        }

        private func imageProperties(of data: Data) throws -> [String: Any] {
            let source = try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
            let props = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [String: Any]
            return try XCTUnwrap(props)
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
            let properties: [CFString: Any] = [kCGImagePropertyGPSDictionary: gps]
            CGImageDestinationAddImage(destination, base, properties as CFDictionary)
            XCTAssertTrue(CGImageDestinationFinalize(destination))
            return output as Data
        }
    }
#endif
