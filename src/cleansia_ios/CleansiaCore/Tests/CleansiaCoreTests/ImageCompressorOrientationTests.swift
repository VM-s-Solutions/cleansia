#if canImport(UIKit)
    import UIKit
    import XCTest
    @testable import CleansiaCore

    /// TC-IOS-IMG-ORIENTATION — every upload path (customer avatar, dispute evidence, partner order
    /// photos) runs through `ImageCompressor`, and a camera capture reaches it as a landscape sensor
    /// buffer plus an orientation flag. The encoder strips all metadata by design, so unless the
    /// rotation is baked into the pixels here it is gone for good and the photo lands sideways.
    ///
    /// The assertions read the decoded `cgImage` rather than `UIImage.size`: a JPEG that merely
    /// carried an Orientation tag would report an oriented `size` while its pixels stayed wrong, and
    /// that is the fix this guards against being swapped back in.
    final class ImageCompressorOrientationTests: XCTestCase {
        func testPortraitCaptureIsRotatedIntoItsPixels() throws {
            let capture = try portraitCapture(bufferWidth: 1200, bufferHeight: 800, orientation: .right)
            XCTAssertEqual(capture.size, CGSize(width: 800, height: 1200)) // the oriented display size

            let pixels = try encodedPixels(of: capture)

            XCTAssertEqual(pixels.width, 800)
            XCTAssertEqual(pixels.height, 1200)
        }

        func testLeftRotatedCaptureIsUprightedToo() throws {
            let capture = try portraitCapture(bufferWidth: 1200, bufferHeight: 800, orientation: .left)

            let pixels = try encodedPixels(of: capture)

            XCTAssertEqual(pixels.width, 800)
            XCTAssertEqual(pixels.height, 1200)
        }

        /// The upright path is the common one and must not be re-drawn or transposed on the way past.
        func testUprightCaptureIsLeftAlone() throws {
            let capture = try portraitCapture(bufferWidth: 1200, bufferHeight: 800, orientation: .up)

            let pixels = try encodedPixels(of: capture)

            XCTAssertEqual(pixels.width, 1200)
            XCTAssertEqual(pixels.height, 800)
        }

        /// The oriented longest side is what the cap applies to: 3000x2000 flagged `.right` is a
        /// 2000x3000 photo, so the 1920 cap lands on its height.
        func testDownscaleMeasuresTheOrientedLongestSide() throws {
            let capture = try portraitCapture(bufferWidth: 3000, bufferHeight: 2000, orientation: .right)

            let pixels = try encodedPixels(of: capture, maxDimension: 1920)

            XCTAssertEqual(pixels.height, 1920)
            XCTAssertEqual(pixels.width, 1280) // 2000:3000 preserved
        }

        // MARK: Helpers

        /// What the picker hands back from a portrait shot: the sensor's landscape buffer, plus the
        /// flag that says how to stand it up.
        private func portraitCapture(
            bufferWidth: Int,
            bufferHeight: Int,
            orientation: UIImage.Orientation
        ) throws -> UIImage {
            let size = CGSize(width: bufferWidth, height: bufferHeight)
            let format = UIGraphicsImageRendererFormat.default()
            format.scale = 1
            let buffer = UIGraphicsImageRenderer(size: size, format: format).image { context in
                UIColor.systemTeal.setFill()
                context.fill(CGRect(origin: .zero, size: size))
            }
            return try UIImage(cgImage: XCTUnwrap(buffer.cgImage), scale: 1, orientation: orientation)
        }

        private func encodedPixels(of image: UIImage, maxDimension: CGFloat = 4000) throws -> CGImage {
            let encoded = try XCTUnwrap(ImageCompressor.encode(image, maxDimension: maxDimension))
            let data = try XCTUnwrap(Data(base64Encoded: encoded.base64))
            let decoded = try XCTUnwrap(UIImage(data: data))

            // Nothing may re-introduce the tag the EXIF strip removes — the rotation belongs in the
            // pixels, and an orientation-carrying output would mean it is being smuggled instead.
            XCTAssertEqual(decoded.imageOrientation, .up)
            return try XCTUnwrap(decoded.cgImage)
        }
    }
#endif
