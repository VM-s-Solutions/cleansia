import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// Renders the avatar disc for real on whatever runtime the suite is running against, so the iOS-16
/// floor exercises the async image path and the picker API rather than only the view model. Reads the
/// rendered pixels: a view that fails to lay out or an image that never arrives leaves the initials
/// disc white, which is exactly what the assertions catch.
@MainActor
final class ProfileAvatarRenderingTests: XCTestCase {
    func testAPickedImageRendersInPlaceOfTheInitials() throws {
        let picked = ProfileFixtures.image()

        let rendered = try render(ProfileAvatar(display: .picked(picked), initials: "JD", cache: RemoteImageCache()))

        assertCentre(rendered, isCloseTo: UIColor.systemTeal)
    }

    func testAStoredPhotoIsFetchedDecodedAndRendered() throws {
        let bytes = Self.magentaJpeg()
        let cache = RemoteImageCache(loader: { _ in bytes })
        let view = ProfileAvatar(display: .remote(ProfileFixtures.photo()), initials: "JD", cache: cache)

        let rendered = try render(view, settleFor: 1.0)

        assertCentre(rendered, isCloseTo: UIColor.magenta)
    }

    func testARenderedPhotoReportsItsSuccess() throws {
        let bytes = Self.magentaJpeg()
        let cache = RemoteImageCache(loader: { _ in bytes })
        let successes = Box()
        let view = ProfileAvatar(
            display: .remote(ProfileFixtures.photo()),
            initials: "JD",
            cache: cache,
            onLoadSuccess: { successes.count += 1 }
        )

        let rendered = try render(view, settleFor: 1.0)

        assertCentre(rendered, isCloseTo: UIColor.magenta)
        XCTAssertEqual(successes.count, 1)
    }

    func testAFailedLoadFallsBackToTheInitialsDiscAndReportsTheFailure() throws {
        let cache = RemoteImageCache(loader: { _ in throw RemoteImageError.unreachable })
        let failures = Box()
        let view = ProfileAvatar(
            display: .remote(ProfileFixtures.photo()),
            initials: "JD",
            cache: cache,
            onLoadFailure: { _ in failures.count += 1 }
        )

        let rendered = try render(view, settleFor: 1.0)

        assertCentre(rendered, isCloseTo: .white)
        XCTAssertEqual(failures.count, 1)
    }

    /// `UIImagePickerController` is the repo's one picker for both camera and library; this pins that
    /// the library picker still builds on the floor runtime rather than presenting nothing.
    func testThePickerBuildsALibraryPickerOnThisRuntime() {
        let host = UIHostingController(rootView: CameraOrLibraryPicker(
            sourceType: .photoLibrary,
            onImagePicked: { _ in }
        ))
        let frame = CGRect(x: 0, y: 0, width: 320, height: 480)
        let window = UIWindow(frame: frame)
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.frame = frame
        host.view.layoutIfNeeded()

        let picker = Self.imagePicker(under: host)
        XCTAssertNotNil(picker, "no UIImagePickerController was built on this runtime")
        XCTAssertEqual(picker?.sourceType, .photoLibrary)
    }

    // MARK: - Rendering

    private static let side: CGFloat = 72

    private func render(_ view: some View, settleFor seconds: TimeInterval = 0) throws -> UIImage {
        let host = UIHostingController(rootView: view)
        let frame = CGRect(x: 0, y: 0, width: Self.side, height: Self.side)
        let window = UIWindow(frame: frame)
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.frame = frame
        host.view.layoutIfNeeded()
        if seconds > 0 {
            RunLoop.current.run(until: Date().addingTimeInterval(seconds))
            host.view.layoutIfNeeded()
        }
        return UIGraphicsImageRenderer(bounds: frame).image { _ in
            host.view.drawHierarchy(in: frame, afterScreenUpdates: true)
        }
    }

    private func assertCentre(
        _ image: UIImage,
        isCloseTo expected: UIColor,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        guard let actual = centrePixel(of: image) else {
            return XCTFail("could not read the rendered pixels", file: file, line: line)
        }
        var (expectedRed, expectedGreen, expectedBlue, alpha) = (CGFloat(0), CGFloat(0), CGFloat(0), CGFloat(0))
        expected.getRed(&expectedRed, green: &expectedGreen, blue: &expectedBlue, alpha: &alpha)
        let delta = abs(actual.0 - expectedRed) + abs(actual.1 - expectedGreen) + abs(actual.2 - expectedBlue)
        let wanted = [expectedRed, expectedGreen, expectedBlue]
        XCTAssertLessThan(
            delta,
            0.25,
            "rendered \([actual.0, actual.1, actual.2]), expected \(wanted)",
            file: file,
            line: line
        )
    }

    private func centrePixel(of image: UIImage) -> (CGFloat, CGFloat, CGFloat)? {
        guard let cgImage = image.cgImage else { return nil }
        var pixel = [UInt8](repeating: 0, count: 4)
        guard let context = CGContext(
            data: &pixel,
            width: 1,
            height: 1,
            bitsPerComponent: 8,
            bytesPerRow: 4,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return nil }
        let width = CGFloat(cgImage.width)
        let height = CGFloat(cgImage.height)
        context.draw(cgImage, in: CGRect(x: -width / 2 + 0.5, y: -height / 2 + 0.5, width: width, height: height))
        return (CGFloat(pixel[0]) / 255, CGFloat(pixel[1]) / 255, CGFloat(pixel[2]) / 255)
    }

    private static func imagePicker(under controller: UIViewController) -> UIImagePickerController? {
        if let picker = controller as? UIImagePickerController { return picker }
        return controller.children.lazy.compactMap { imagePicker(under: $0) }.first
    }

    private static func magentaJpeg() -> Data {
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = 1
        let size = CGSize(width: 64, height: 64)
        let image = UIGraphicsImageRenderer(size: size, format: format).image { context in
            UIColor.magenta.setFill()
            context.fill(CGRect(origin: .zero, size: size))
        }
        return image.jpegData(compressionQuality: 1) ?? Data()
    }

    private final class Box {
        var count = 0
    }
}
