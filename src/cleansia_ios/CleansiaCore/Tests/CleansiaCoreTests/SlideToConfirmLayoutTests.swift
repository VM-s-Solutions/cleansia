import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The label's own frame is read from the accessibility tree and the track's from a probe, both in the window's
/// points. The Core test bundle ships no fonts, so the label is set in the system face here; the rules read the same.
@MainActor
final class SlideToConfirmLayoutTests: XCTestCase {
    private let long = "Swipe to accept the contract for work and every one of its terms"

    private func open(_ label: String, maxLabelLines: Int = 1, thumbLanes: Bool = false) -> SlideToConfirm {
        SlideToConfirm(
            idleLabel: label,
            busyLabel: label,
            isBusy: false,
            maxLabelLines: maxLabelLines,
            thumbLanes: thumbLanes,
            onConfirm: {}
        )
    }

    private func frames(
        _ slide: SlideToConfirm,
        label: String,
        width: CGFloat
    ) async -> (track: CGRect, label: CGRect) {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: CGSize(width: width + 40, height: 400)) {
            slide.frame(width: width).probed("track", in: probes)
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["track"] != nil }
        let element = Self.elements(under: canvas.window).first { $0.accessibilityLabel == label }
        return (probes.frames["track"] ?? .zero, element?.accessibilityFrame ?? .zero)
    }

    private func height(_ slide: SlideToConfirm, width: CGFloat) -> CGFloat {
        UIHostingController(rootView: slide.frame(width: width))
            .sizeThatFits(in: CGSize(width: width, height: .greatestFiniteMagnitude))
            .height
    }

    /// The lanes keep 60 clear at each end, the 48 thumb, its 4 inset and 8, so the words wrap between them and
    /// stay centred on the track instead of running under the thumb.
    func testWithLanesTheLabelKeepsSixtyClearAtEachEnd() async {
        let (track, label) = await frames(open(long, maxLabelLines: 3, thumbLanes: true), label: long, width: 335)

        XCTAssertGreaterThan(label.width, 150, "the label was found and wraps across the room it has")
        XCTAssertGreaterThanOrEqual(label.minX, track.minX + 59.5)
        XCTAssertLessThanOrEqual(label.maxX, track.maxX - 59.5)
        XCTAssertEqual(label.midX, track.midX, accuracy: 0.5)
    }

    /// Without lanes the same label takes the whole track.
    func testWithoutLanesTheLabelTakesTheWholeTrack() async {
        let (track, label) = await frames(open(long, maxLabelLines: 3, thumbLanes: false), label: long, width: 335)

        XCTAssertGreaterThan(label.width, track.width - 120 + 20)
    }

    /// The label wraps up to `maxLabelLines`, and the track grows from 56 to hold the lines it takes.
    func testTheLabelTakesUpToMaxLabelLinesAndTheTrackGrowsToHoldThem() async {
        let two = await frames(open(long, maxLabelLines: 2, thumbLanes: true), label: long, width: 300).label
        let three = await frames(open(long, maxLabelLines: 3, thumbLanes: true), label: long, width: 300).label

        XCTAssertEqual(three.height / two.height, 1.5, accuracy: 0.1, "three lines against two")
        XCTAssertGreaterThan(height(open(long, maxLabelLines: 3, thumbLanes: true), width: 300), 56)
    }

    /// With no opt-in the track keeps its fixed 56 whatever the label: the customer's booking slide, whose own line
    /// limit and scale factor govern its label.
    func testWithNoOptInTheTrackKeepsItsFixedHeight() {
        XCTAssertEqual(height(open(long + " " + long), width: 300), 56, accuracy: 0.5)
    }

    /// The locked slide is never cut short: its reason and detail wrap and the track grows to hold them.
    func testALockedSlideGrowsToHoldItsWholeReasonAndDetail() {
        let short = height(SlideToConfirm(lockedReason: "Not yet"), width: 335)
        let wrapped = height(SlideToConfirm(lockedReason: long, detail: "Monday · Nov 16"), width: 335)

        XCTAssertEqual(short, 56, accuracy: 0.5)
        XCTAssertGreaterThan(wrapped, 56)
    }

    private static func elements(under view: NSObject, depth: Int = 0) -> [NSObject] {
        guard depth < 60 else { return [] }
        if view.isAccessibilityElement { return [view] }
        if let children = view.accessibilityElements as? [NSObject], !children.isEmpty {
            return children.flatMap { elements(under: $0, depth: depth + 1) }
        }
        let count = view.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            return (0 ..< count).compactMap { view.accessibilityElement(at: $0) as? NSObject }
                .flatMap { elements(under: $0, depth: depth + 1) }
        }
        return ((view as? UIView)?.subviews ?? []).flatMap { elements(under: $0, depth: depth + 1) }
    }
}
