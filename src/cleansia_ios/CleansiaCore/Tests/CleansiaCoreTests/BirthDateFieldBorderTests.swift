import SwiftUI
import XCTest
@testable import CleansiaCore

/// A stroke straddles its path, so a clip applied after it cuts its outer half away and the border shows at
/// half its width. The field clips its box first and strokes over it, as every other field does.
@MainActor
final class BirthDateFieldBorderTests: XCTestCase {
    func testTheErrorBorderIsDrawnAtItsFullOnePoint() throws {
        let field = BirthDateField(birthDate: .constant(nil), label: "L", placeholder: "P", errorText: "E")

        XCTAssertEqual(try topBorderThickness(field), 3, accuracy: 0.5, "1 pt at 3x")
    }

    /// The error ink across the top edge of `view`, read down its centre column at 3x and summed, so a row the
    /// stroke half covers counts a half: the border's thickness in pixels.
    private func topBorderThickness(_ view: some View) throws -> Double {
        let renderer = ImageRenderer(content: view
            .frame(width: 320)
            .padding(8)
            .background(Color.white)
            .environment(\.colorScheme, .light))
        renderer.scale = 3
        let image = try XCTUnwrap(renderer.cgImage, "the view rendered no image")
        let context = try XCTUnwrap(CGContext(
            data: nil,
            width: image.width,
            height: image.height,
            bitsPerComponent: 8,
            bytesPerRow: image.width * 4,
            space: XCTUnwrap(CGColorSpace(name: CGColorSpace.sRGB)),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ))
        context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
        let pixels = try XCTUnwrap(context.data?.assumingMemoryBound(to: UInt8.self))
        let column = image.width / 2
        let ink = (0 ..< image.height).map { row in
            1 - Double(pixels[row * context.bytesPerRow + column * 4 + 1]) / 255
        }
        let top = try XCTUnwrap(ink.firstIndex { $0 > 0.02 }, "no border crosses the centre column")
        let edge = ink[top...].prefix { $0 > 0.02 }
        let full = try XCTUnwrap(edge.max())
        return edge.reduce(0) { $0 + $1 } / full
    }
}
