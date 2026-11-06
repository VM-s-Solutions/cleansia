import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// A glyph beside text sits on the cap band of the line its stack aligns on: its centre at the baseline less half
/// the face's cap height, the visual centre of a capitalised word. A zero-height mark in the same stack lands on
/// that baseline, so the test reads the line's baseline from it. The glyphs are a 22 symbol and a plain shape: a
/// symbol at its text's own size already sits near the band on its own baseline, so it could not tell.
@MainActor
final class CapBandAlignmentTests: XCTestCase {
    private let style = CleansiaTypography.scale.bodyMedium

    func testALeadingGlyphSitsOnTheCapBandOfTheFirstLine() async {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: CGSize(width: 160, height: 300)) {
            HStack(alignment: .firstTextBaseline, spacing: 8) {
                Image(systemName: "square")
                    .font(.system(size: 22))
                    .capBandAligned(of: style)
                    .probed("glyph", in: probes)
                Text(verbatim: "Kitchen, two bathrooms and the hall")
                    .font(CleansiaTypography.bodyMedium)
                    .frame(width: 110, alignment: .leading)
                    .probed("text", in: probes)
                Color.clear.frame(width: 1, height: 0).probed("baseline", in: probes)
            }
            .frame(width: 160)
            .dynamicTypeSize(.large)
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames.count == 3 }

        let glyph = probes.frames["glyph"] ?? .zero
        let text = probes.frames["text"] ?? .zero
        let baseline = probes.frames["baseline"]?.minY ?? 0
        let capHeight = style.uiFont(for: .large).capHeight

        XCTAssertGreaterThan(text.height, 40, "the label takes more than one line")
        XCTAssertLessThan(baseline, text.minY + text.height / 2, "the stack aligns on the first line")
        XCTAssertEqual(glyph.midY, baseline - capHeight / 2, accuracy: 0.5)
    }

    func testATrailingGlyphSitsOnTheCapBandOfTheLastLine() async {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: CGSize(width: 160, height: 300)) {
            HStack(alignment: .lastTextBaseline, spacing: 8) {
                Text(verbatim: "Payouts are in Pay and Earnings")
                    .font(CleansiaTypography.labelLarge)
                    .frame(width: 110, alignment: .leading)
                    .probed("text", in: probes)
                RoundedRectangle(cornerRadius: 2)
                    .frame(width: 8, height: 16)
                    .capBandAligned(.lastTextBaseline, of: CleansiaTypography.scale.labelLarge)
                    .probed("glyph", in: probes)
                Color.clear.frame(width: 1, height: 0).probed("baseline", in: probes)
            }
            .frame(width: 160)
            .dynamicTypeSize(.large)
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames.count == 3 }

        let glyph = probes.frames["glyph"] ?? .zero
        let text = probes.frames["text"] ?? .zero
        let baseline = probes.frames["baseline"]?.minY ?? 0
        let capHeight = CleansiaTypography.scale.labelLarge.uiFont(for: .large).capHeight

        XCTAssertGreaterThan(text.height, 30, "the label takes more than one line")
        XCTAssertGreaterThan(baseline, text.minY + text.height / 2, "the stack aligns on the last line")
        XCTAssertEqual(glyph.midY, baseline - capHeight / 2, accuracy: 0.5)
    }
}
