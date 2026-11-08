import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The label's own frame is read from the text view that draws it and the track's from a probe, both in the window's
/// points. The Core test bundle ships no fonts, so the label is set in the system face here; the rules read the same.
@MainActor
final class SlideToConfirmLayoutTests: XCTestCase {
    private let long = "Swipe to accept the contract for work and every one of its terms"

    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

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

    private func frames(_ slide: SlideToConfirm, width: CGFloat) async -> (track: CGRect, label: CGRect) {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: CGSize(width: width + 40, height: 400)) {
            slide.frame(width: width).probed("track", in: probes)
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["track"] != nil }
        return (probes.frames["track"] ?? .zero, Self.labelFrame(in: canvas.window))
    }

    /// VoiceOver reads the open slide as one button, so its words are found where they are drawn: the widest of the
    /// layers SwiftUI draws text into, on iOS 16 and on iOS 26 alike.
    private static func labelFrame(in window: UIWindow) -> CGRect {
        func texts(_ layer: CALayer) -> [CALayer] {
            (String(describing: type(of: layer)) == "CGDrawingLayer" ? [layer] : [])
                + (layer.sublayers ?? []).flatMap(texts)
        }
        return texts(window.layer).map { $0.convert($0.bounds, to: window.layer) }.max { $0.width < $1.width } ?? .zero
    }

    private func height(_ slide: SlideToConfirm, width: CGFloat) -> CGFloat {
        UIHostingController(rootView: slide.frame(width: width))
            .sizeThatFits(in: CGSize(width: width, height: .greatestFiniteMagnitude))
            .height
    }

    /// The lanes keep 60 clear at each end, the 48 thumb, its 4 inset and 8, so the words wrap between them and
    /// stay centred on the track instead of running under the thumb.
    func testWithLanesTheLabelKeepsSixtyClearAtEachEnd() async {
        let (track, label) = await frames(open(long, maxLabelLines: 3, thumbLanes: true), width: 335)

        XCTAssertGreaterThan(label.width, 150, "the label was found and wraps across the room it has")
        XCTAssertGreaterThanOrEqual(label.minX, track.minX + 59.5)
        XCTAssertLessThanOrEqual(label.maxX, track.maxX - 59.5)
        XCTAssertEqual(label.midX, track.midX, accuracy: 0.5)
    }

    /// Without lanes the same label takes the whole track.
    func testWithoutLanesTheLabelTakesTheWholeTrack() async {
        let (track, label) = await frames(open(long, maxLabelLines: 3, thumbLanes: false), width: 335)

        XCTAssertGreaterThan(label.width, track.width - 120 + 20)
    }

    /// The label wraps up to `maxLabelLines`, and the track grows from 56 to hold the lines it takes.
    func testTheLabelTakesUpToMaxLabelLinesAndTheTrackGrowsToHoldThem() async {
        let two = await frames(open(long, maxLabelLines: 2, thumbLanes: true), width: 300).label
        let three = await frames(open(long, maxLabelLines: 3, thumbLanes: true), width: 300).label

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

    /// VoiceOver cannot drag: the open slide is one button named by its words and detail, and activating it
    /// confirms. A disabled or busy slide is not enabled, and activating it confirms nothing.
    func testVoiceOverConfirmsAnOpenSlideAndNothingWhenItCannot() async throws {
        for (enabled, busy, confirms) in [(true, false, 1), (false, false, 0), (true, true, 0)] {
            var confirmed = 0
            let slide = SlideToConfirm(
                idleLabel: "Slide to confirm",
                busyLabel: "Booking",
                isBusy: busy,
                enabled: enabled,
                detail: "1 200 Kč"
            ) { confirmed += 1 }
            let canvas = HostedCanvas(size: CGSize(width: 375, height: 200)) { slide.frame(width: 335) }
            defer { canvas.close() }
            await canvas.settle()
            let elements = ApplicationAccessibility.elements(under: canvas.window)
            let element = try XCTUnwrap(elements.first)

            XCTAssertEqual(elements.count, 1)
            XCTAssertEqual(element.accessibilityLabel, "\(busy ? "Booking" : "Slide to confirm"), 1 200 Kč")
            XCTAssertTrue(element.accessibilityTraits.contains(.button))
            XCTAssertEqual(element.accessibilityTraits.contains(.notEnabled), confirms == 0)
            element.accessibilityActivate()
            await canvas.settle()
            XCTAssertEqual(confirmed, confirms, "enabled \(enabled), busy \(busy)")
        }
    }
}
