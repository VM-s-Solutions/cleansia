import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// The profile and onboarding sections, the job's action footer and the orders and invoices lists keep their content
/// at most a readable width, centred, decided by the width they are given: on every iPhone today (440pt at most) and
/// on the iPhone Duo's outer display they lay out exactly as before, and only a wider container, such as the Duo's
/// inner display, centres them.
@MainActor
final class PartnerReadableWidthTests: XCTestCase {
    private let cap: CGFloat = 640
    private let narrow = [
        CGSize(width: 393, height: 852),
        CGSize(width: 440, height: 956),
        CGSize(width: 466, height: 678)
    ]
    private let wide = [CGSize(width: 669, height: 951), CGSize(width: 951, height: 669)]
    private let phoneSafeArea = EdgeInsets(top: 59, leading: 0, bottom: 34, trailing: 0)

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

    func testTheOrdersListSpansAPhoneAsBefore() async throws {
        for size in narrow {
            let pane = try await activeOrdersPane(in: size)
            XCTAssertEqual(pane.minX, 0, accuracy: 0.5, "\(size)")
            XCTAssertEqual(pane.width, size.width, accuracy: 0.5, "\(size)")
        }
    }

    /// The Active pane, its hero and the hero's slide inside the pane's margins, is held to the readable width and
    /// centred on a wider canvas.
    func testTheOrdersListHoldsTheActiveJobsSlideToTheReadableWidthOnAWiderCanvas() async throws {
        for size in wide {
            let pane = try await activeOrdersPane(in: size)
            XCTAssertEqual(pane.width, cap, accuracy: 0.5, "\(size)")
            XCTAssertEqual(pane.midX, size.width / 2, accuracy: 0.5, "\(size)")
        }
    }

    func testTheInvoicesListSpansAPhoneAsBefore() async throws {
        for size in narrow {
            let list = try await invoicesListFrame(in: size)
            XCTAssertEqual(list.minX, 0, accuracy: 0.5, "\(size)")
            XCTAssertEqual(list.width, size.width, accuracy: 0.5, "\(size)")
        }
    }

    func testTheInvoicesListIsCentredAtTheReadableWidthOnAWiderCanvas() async throws {
        for size in wide {
            let list = try await invoicesListFrame(in: size)
            XCTAssertEqual(list.width, cap, accuracy: 0.5, "\(size)")
            XCTAssertEqual(list.midX, size.width / 2, accuracy: 0.5, "\(size)")
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
        let canvas = try HostedCanvas(size: size, safeArea: phoneSafeArea) {
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

    /// The Orders screen on its Active tab with one job on the way, which its hero offers as a slide: the pane's
    /// scroll view, where it lays out.
    private func activeOrdersPane(in size: CGSize) async throws -> CGRect {
        let client = FakePartnerOrderClient()
        client.pagedResult = .success([.sample(id: "job", status: ._3, cleaningDateTime: Date())])
        let vm = OrdersListViewModel(
            client: client,
            staleness: OrdersStaleness(window: 30, now: { Date() }),
            snackbar: SnackbarController()
        )
        await vm.selectTab(.active)
        XCTAssertEqual(OrdersBoardRules.heroControl(.sample(id: "job", status: ._3), now: Date()), .start)
        let canvas = try HostedCanvas(size: size, safeArea: phoneSafeArea) {
            OrdersListView(vm: vm)
        }
        defer { canvas.close() }
        await canvas.settle { !canvas.views(of: UIScrollView.self).isEmpty }
        let pane = try XCTUnwrap(canvas.views(of: UIScrollView.self).first, "no Active pane at \(size)")
        return canvas.frame(of: pane)
    }

    private func invoicesListFrame(in size: CGSize) async throws -> CGRect {
        let client = FakePayrollClient()
        client.invoicesResult = .success([.stub(id: "inv-1", totalAmount: 4200)])
        let canvas = try HostedCanvas(size: size, safeArea: phoneSafeArea) {
            InvoicesListView(
                client: client,
                staleness: InvoicesStaleness(),
                snackbar: SnackbarController(),
                onOpenInvoice: { _ in }
            )
        }
        defer { canvas.close() }
        await canvas.settle { !canvas.views(of: UIScrollView.self).isEmpty }
        let list = try XCTUnwrap(canvas.views(of: UIScrollView.self).first, "no invoices list at \(size)")
        return canvas.frame(of: list)
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

/// A view in a window of the given size in the app's own scene, so a screen's scroll views lay out as in the app,
/// whose safe area is exactly the one given: the system's own is dropped and the given one laid in, so every
/// simulator reads the same insets. The window that was key before is key again on `close()`.
@MainActor
private final class HostedCanvas {
    let window: UIWindow
    let host: UIViewController
    private let previous: UIWindow?

    init(size: CGSize, safeArea: EdgeInsets = EdgeInsets(), @ViewBuilder content: () -> some View) throws {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        previous = scenes.flatMap(\.windows).first { $0.isKeyWindow }
        let root = content()
            .safeAreaInset(edge: .top, spacing: 0) { Color.clear.frame(height: safeArea.top) }
            .safeAreaInset(edge: .bottom, spacing: 0) { Color.clear.frame(height: safeArea.bottom) }
        let host = UIHostingController(rootView: root)
        if #available(iOS 16.4, *) {
            host.safeAreaRegions = []
        }
        let window = try UIWindow(windowScene: XCTUnwrap(scenes.first, "the test host has no scene"))
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
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
        previous?.makeKeyAndVisible()
    }

    func views<V: UIView>(of _: V.Type) -> [V] {
        Self.views(under: host.view).compactMap { $0 as? V }
    }

    func frame(of view: UIView) -> CGRect {
        view.convert(view.bounds, to: window)
    }

    private func pause() async {
        try? await Task.sleep(nanoseconds: 30_000_000)
        host.view.layoutIfNeeded()
    }

    private static func views(under view: UIView) -> [UIView] {
        [view] + view.subviews.flatMap { views(under: $0) }
    }
}
