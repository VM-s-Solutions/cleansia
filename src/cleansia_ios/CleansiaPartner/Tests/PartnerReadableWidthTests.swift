import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// The profile and onboarding sections and the job's action footer keep their content at most a readable width,
/// centred, decided by the width they are given: on every iPhone today (440pt at most) and on the iPhone Duo's outer
/// display they lay out exactly as before, and only a wider container, such as the Duo's inner display, centres them.
@MainActor
final class PartnerReadableWidthTests: XCTestCase {
    private let cap: CGFloat = 640
    private let narrow = [
        CGSize(width: 393, height: 852),
        CGSize(width: 440, height: 956),
        CGSize(width: 466, height: 678)
    ]
    private let wide = [CGSize(width: 669, height: 951), CGSize(width: 951, height: 669)]

    func testASectionsFormSpansAPhoneInsideItsMarginsAsBefore() async throws {
        for size in narrow {
            let form = try await sectionFormFrame(in: size)
            XCTAssertEqual(form.minX, Spacing.m, accuracy: 0.5, "\(size)")
            XCTAssertEqual(form.width, size.width - 2 * Spacing.m, accuracy: 0.5, "\(size)")
        }
    }

    func testASectionsFormIsCentredAtTheReadableWidthOnAWiderCanvas() async throws {
        for size in wide {
            let form = try await sectionFormFrame(in: size)
            XCTAssertEqual(form.width, cap - 2 * Spacing.m, accuracy: 0.5, "\(size)")
            XCTAssertEqual(form.midX, size.width / 2, accuracy: 0.5, "\(size)")
        }
    }

    func testTheJobsSlideSpansAPhoneInsideTheFootersMarginsAsBefore() throws {
        for size in narrow {
            let track = try slideTrack(width: size.width)
            XCTAssertEqual(track.lowerBound, Spacing.m, accuracy: 1, "\(size)")
            XCTAssertEqual(track.upperBound, size.width - Spacing.m, accuracy: 1, "\(size)")
        }
    }

    func testTheJobsSlideIsCentredAtTheReadableWidthOnAWiderCanvas() throws {
        for size in wide {
            let track = try slideTrack(width: size.width)
            XCTAssertEqual(track.upperBound - track.lowerBound, cap - 2 * Spacing.m, accuracy: 1, "\(size)")
            XCTAssertEqual((track.lowerBound + track.upperBound) / 2, size.width / 2, accuracy: 1, "\(size)")
        }
    }

    /// The hub's content is held to the readable width on a wide canvas, so its hero's gradient paints sideways past
    /// the hero's frame as it already paints upwards past it: the hero stays full-bleed at any width.
    func testTheProfileHubsHeroPaintsPastItsFrameOnEverySide() throws {
        let source = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/Features/Profile/ProfileHubContent.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()

        XCTAssertTrue(source.contains(
            "LinearGradient(colors:BrandGradient.blue.colors,startPoint:.top,endPoint:.bottom)}"
                + ".padding(.top,-heroBleed).padding(.horizontal,-heroBleed)"
        ))
    }

    private func sectionFormFrame(in size: CGSize) async throws -> CGRect {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: size, safeArea: EdgeInsets(top: 59, leading: 0, bottom: 34, trailing: 0)) {
            SectionScaffold(title: "Section", isLoading: false) {
                Color.clear
                    .frame(maxWidth: .infinity)
                    .frame(height: 300)
                    .probed("form", in: probes)
            }
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["form"] != nil }
        return try XCTUnwrap(probes.frames["form"], "the form never laid out at \(size)")
    }

    /// The slide's track along the footer's middle row, where its capsule is full height: from the first point that
    /// is not the footer's surface to the last.
    private func slideTrack(width: CGFloat) throws -> ClosedRange<CGFloat> {
        let footer = StickyActionFooter(action: .start, inFlightAction: nil, onConfirm: { _ in })
            .frame(width: width)
            .environment(\.colorScheme, .light)
        let renderer = ImageRenderer(content: footer)
        renderer.scale = 1
        let image = try XCTUnwrap(renderer.cgImage, "the footer rendered no image")
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
        let row = image.height / 2
        let inked = (0 ..< image.width).filter { column in
            let offset = row * context.bytesPerRow + column * 4
            return (0 ..< 3).contains { abs(Int(pixels[offset + $0]) - 255) > 6 }
        }
        let first = try XCTUnwrap(inked.first, "no track across the footer's middle row")
        let last = try XCTUnwrap(inked.last)
        return CGFloat(first) ... CGFloat(last + 1)
    }
}

/// The frames hosted views report about themselves, in the window's coordinates.
@MainActor
private final class LayoutProbes {
    private(set) var frames: [String: CGRect] = [:]

    func record(_ name: String, frame: CGRect) {
        frames[name] = frame
    }
}

private extension View {
    func probed(_ name: String, in probes: LayoutProbes) -> some View {
        background(GeometryReader { proxy in
            let frame = proxy.frame(in: .global)
            Color.clear
                .onAppear { probes.record(name, frame: frame) }
                .onChange(of: frame) { probes.record(name, frame: $0) }
        })
    }
}

/// A view in a window of the given size whose safe area is exactly the one given: the system's own is dropped and
/// the given one laid in, so every simulator reads the same insets.
@MainActor
private final class HostedCanvas {
    let window: UIWindow
    let host: UIViewController

    init(size: CGSize, safeArea: EdgeInsets = EdgeInsets(), @ViewBuilder content: () -> some View) {
        let root = content()
            .safeAreaInset(edge: .top, spacing: 0) { Color.clear.frame(height: safeArea.top) }
            .safeAreaInset(edge: .bottom, spacing: 0) { Color.clear.frame(height: safeArea.bottom) }
        let host = UIHostingController(rootView: root)
        if #available(iOS 16.4, *) {
            host.safeAreaRegions = []
        }
        let window = UIWindow(frame: CGRect(origin: .zero, size: size))
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.frame = window.bounds
        host.view.layoutIfNeeded()
        self.window = window
        self.host = host
    }

    /// Runs the layout passes and appear callbacks that follow the first pass until `condition` holds, then a
    /// few more, so a value that settles late is read settled.
    func settle(timeout: TimeInterval = 3, until condition: () -> Bool = { true }) async {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition(), Date() < deadline {
            await pause()
        }
        for _ in 0 ..< 3 {
            await pause()
        }
    }

    func close() {
        window.isHidden = true
        window.rootViewController = nil
    }

    private func pause() async {
        try? await Task.sleep(nanoseconds: 30_000_000)
        host.view.layoutIfNeeded()
    }
}
