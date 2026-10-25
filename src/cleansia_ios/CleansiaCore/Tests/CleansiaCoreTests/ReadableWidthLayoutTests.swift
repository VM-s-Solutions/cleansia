import SwiftUI
import XCTest
@testable import CleansiaCore

/// The scroll scaffolds keep their content at most a readable width, centred, decided by the width they are given:
/// on every iPhone today (440pt at most) and on the iPhone Duo's outer display the content still spans the
/// container, exactly as before, and only a wider container, such as the Duo's inner display, centres it.
@MainActor
final class ReadableWidthLayoutTests: XCTestCase {
    private let cap: CGFloat = 640
    private let narrow = [
        CGSize(width: 393, height: 852),
        CGSize(width: 440, height: 956),
        CGSize(width: 466, height: 678)
    ]
    private let wide = [CGSize(width: 669, height: 951), CGSize(width: 951, height: 669)]

    func testTheAuthScrollSpansAPhonesWidthAsBefore() async throws {
        for size in narrow {
            let frame = try await contentFrame(in: size) { probes in
                CenteredAuthScroll { block(probes) }
            }
            XCTAssertEqual(frame.minX, 0, accuracy: 0.5, "\(size)")
            XCTAssertEqual(frame.width, size.width, accuracy: 0.5, "\(size)")
        }
    }

    func testTheAuthScrollCentresItsContentAtTheReadableWidthOnAWiderCanvas() async throws {
        for size in wide {
            let frame = try await contentFrame(in: size) { probes in
                CenteredAuthScroll { block(probes) }
            }
            XCTAssertEqual(frame.width, cap, accuracy: 0.5, "\(size)")
            XCTAssertEqual(frame.midX, size.width / 2, accuracy: 0.5, "\(size)")
        }
    }

    func testTheFadingScrollSpansAPhonesWidthAsBefore() async throws {
        for size in narrow {
            let frame = try await contentFrame(in: size) { probes in
                StatusBarFadeScrollView { block(probes) }
            }
            XCTAssertEqual(frame.minX, 0, accuracy: 0.5, "\(size)")
            XCTAssertEqual(frame.width, size.width, accuracy: 0.5, "\(size)")
        }
    }

    func testTheFadingScrollCentresItsContentAtTheReadableWidthOnAWiderCanvas() async throws {
        for size in wide {
            let frame = try await contentFrame(in: size) { probes in
                StatusBarFadeScrollView(heroTint: .black) { block(probes) }
            }
            XCTAssertEqual(frame.width, cap, accuracy: 0.5, "\(size)")
            XCTAssertEqual(frame.midX, size.width / 2, accuracy: 0.5, "\(size)")
        }
    }

    /// The content is held to the cap and centred, the frame around it still fills the container, so a background
    /// behind it is full-bleed; content narrower than the container stays centred at its own width, as it was.
    func testTheModifierHoldsTheContentToTheCapInAContainerItStillFills() async throws {
        for size in narrow + wide {
            let probes = LayoutProbes()
            let canvas = HostedCanvas(size: size) {
                VStack(spacing: 0) {
                    Color.clear
                        .frame(maxWidth: .infinity)
                        .frame(height: 100)
                        .probed("content", in: probes)
                        .readableWidth()
                        .probed("container", in: probes)
                    Color.clear
                        .frame(width: 200, height: 50)
                        .probed("fixed", in: probes)
                        .readableWidth()
                }
            }
            defer { canvas.close() }
            await canvas.settle { probes.frames.count == 3 }
            let content = try XCTUnwrap(probes.frames["content"])
            let container = try XCTUnwrap(probes.frames["container"])
            let fixed = try XCTUnwrap(probes.frames["fixed"])

            XCTAssertEqual(content.width, min(size.width, cap), accuracy: 0.5, "\(size)")
            XCTAssertEqual(content.midX, size.width / 2, accuracy: 0.5, "\(size)")
            XCTAssertEqual(container.width, size.width, accuracy: 0.5, "\(size)")
            XCTAssertEqual(fixed.width, 200, accuracy: 0.5, "\(size)")
            XCTAssertEqual(fixed.midX, size.width / 2, accuracy: 0.5, "\(size)")
        }
        XCTAssertEqual(ReadableWidth.cap, cap)
    }

    private func block(_ probes: LayoutProbes) -> some View {
        Color.clear
            .frame(maxWidth: .infinity)
            .frame(height: 240)
            .probed("content", in: probes)
    }

    private func contentFrame(
        in size: CGSize,
        @ViewBuilder _ content: (LayoutProbes) -> some View
    ) async throws -> CGRect {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: size, safeArea: EdgeInsets(top: 59, leading: 0, bottom: 34, trailing: 0)) {
            content(probes)
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["content"] != nil }
        return try XCTUnwrap(probes.frames["content"], "the content never laid out at \(size)")
    }
}
