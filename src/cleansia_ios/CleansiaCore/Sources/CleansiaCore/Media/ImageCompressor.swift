#if canImport(UIKit)
    import ImageIO
    import UIKit
    import UniformTypeIdentifiers

    public struct EncodedImage: Equatable {
        public let base64: String
        public let contentType: String
        public let fileName: String

        public init(base64: String, contentType: String, fileName: String) {
            self.base64 = base64
            self.contentType = contentType
            self.fileName = fileName
        }
    }

    /// Pure, synchronous image-to-base64 encoder for the partner photo upload
    /// (the JSON base64 shape). Downscales the longest side to `maxDimension`
    /// (aspect-preserved, never upscaling), JPEG-encodes at `quality`, and
    /// base64-encodes the bytes. No UIKit view state — safe to call off the main
    /// thread; the caller awaits it off-main.
    ///
    /// The encoded JPEG carries no EXIF/GPS metadata: the bitmap is re-rendered
    /// into a fresh context and re-encoded through ImageIO with an empty
    /// properties dictionary, so capture-location and other source metadata are
    /// dropped by construction rather than incidentally (security P3).
    public enum ImageCompressor {
        public static func encode(
            _ image: UIImage,
            maxDimension: CGFloat = 1920,
            quality: CGFloat = 0.7
        ) -> EncodedImage? {
            guard let bitmap = redrawnBitmap(from: image, maxDimension: maxDimension) else { return nil }
            guard let data = jpegData(from: bitmap, quality: quality) else { return nil }
            return EncodedImage(
                base64: data.base64EncodedString(),
                contentType: "image/jpeg",
                fileName: "photo.jpg"
            )
        }

        /// Re-renders the source into a fresh `CGImage` at the target size. A new
        /// bitmap context produces pixels with no attached metadata, which is the
        /// explicit EXIF/GPS-strip step (the re-encode below also writes empty
        /// properties as a second guarantee).
        private static func redrawnBitmap(from image: UIImage, maxDimension: CGFloat) -> CGImage? {
            guard let source = image.cgImage else { return nil }
            let width = CGFloat(source.width)
            let height = CGFloat(source.height)
            guard width > 0, height > 0, maxDimension > 0 else { return nil }

            let longest = max(width, height)
            let scale = longest > maxDimension ? maxDimension / longest : 1 // never upscale
            let targetWidth = max(1, Int((width * scale).rounded()))
            let targetHeight = max(1, Int((height * scale).rounded()))

            let colorSpace = CGColorSpaceCreateDeviceRGB()
            let bitmapInfo = CGImageAlphaInfo.noneSkipLast.rawValue
            guard let context = CGContext(
                data: nil,
                width: targetWidth,
                height: targetHeight,
                bitsPerComponent: 8,
                bytesPerRow: 0,
                space: colorSpace,
                bitmapInfo: bitmapInfo
            ) else { return nil }

            context.interpolationQuality = .high
            context.draw(source, in: CGRect(x: 0, y: 0, width: targetWidth, height: targetHeight))
            return context.makeImage()
        }

        private static func jpegData(from bitmap: CGImage, quality: CGFloat) -> Data? {
            let mutableData = NSMutableData()
            let type = UTType.jpeg.identifier as CFString
            guard let destination = CGImageDestinationCreateWithData(mutableData, type, 1, nil) else { return nil }

            // Only the compression quality is written — no EXIF, GPS, TIFF or
            // maker-note dictionaries — so the output is metadata-free.
            let properties: [CFString: Any] = [kCGImageDestinationLossyCompressionQuality: quality]
            CGImageDestinationAddImage(destination, bitmap, properties as CFDictionary)
            guard CGImageDestinationFinalize(destination) else { return nil }
            return mutableData as Data
        }
    }
#endif
