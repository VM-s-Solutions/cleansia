import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// A widget extension has no test target, so the card's pieces are rendered HERE — through `ImageRenderer`,
/// on the real views, at the widths the lock screen and the Dynamic Island actually give them.
///
/// This proves the views produce pixels and that the pixels differ where the design says they must. It does
/// NOT prove ActivityKit hands them the right content-state, nor that the system-drawn timer advances on a
/// locked device — those need a device (see the report's device matrix).
@MainActor
final class LiveActivityCardViewsTests: XCTestCase {
    private let lockScreenWidth: CGFloat = 340
    private let compactWidth: CGFloat = 52

    private let finish = Date(timeIntervalSince1970: 1_700_000_000)

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    private func model(_ status: String, live: Bool = true) -> LiveActivityCardModel {
        let card = LiveActivityCard.forStatus(status)
        return LiveActivityCardModel(
            card: card,
            orderNumber: "ORD-AB12CD34",
            finish: finish,
            liveRange: live && card.showsFinishTime ? finish.addingTimeInterval(-1800) ... finish : nil
        )
    }

    // MARK: - The whole card

    func testTheCardRendersForEveryStatusTheBackendCanSend() throws {
        for status in ["onTheWay", "inProgress", "completed", "cancelled", "something_new"] {
            let image = try render(LiveActivityCleanCard(model: model(status)), width: lockScreenWidth)

            XCTAssertGreaterThan(image.size.height, 40, "\(status) rendered an all-but-empty card")
        }
    }

    func testEveryStatusProducesAVisiblyDifferentCard() throws {
        let statuses = ["onTheWay", "inProgress", "completed", "cancelled", "something_new"]
        let rendered = try statuses.map { try png(LiveActivityCleanCard(model: model($0)), width: lockScreenWidth) }

        XCTAssertEqual(Set(rendered).count, statuses.count, "two statuses render the same card")
    }

    /// A cancelled clean has no journey and no promise: no stepper, no finish time. It is the shortest card
    /// the design produces, and it must stay that way — a dead order showing a finish time is the single
    /// worst thing this surface can do.
    func testTheCancelledCardIsShorterThanAnyInServiceOne() throws {
        let cancelled = try height(LiveActivityCleanCard(model: model("cancelled")), width: lockScreenWidth)

        for status in ["onTheWay", "inProgress", "completed"] {
            let inService = try height(LiveActivityCleanCard(model: model(status)), width: lockScreenWidth)
            XCTAssertLessThan(cancelled, inService, "the cancelled card is as tall as \(status)")
        }
    }

    func testTheCardHoldsItsHeightInEveryLanguage() throws {
        let english = try height(LiveActivityCleanCard(model: model("inProgress")), width: lockScreenWidth)

        for region in ["en", "cs", "sk", "uk", "ru"] {
            CoreL10n.apply(languageTag: region)
            let measured = try height(LiveActivityCleanCard(model: model("inProgress")), width: lockScreenWidth)

            XCTAssertEqual(measured, english, accuracy: 1, "\(region) reflowed the card onto more rows")
        }
    }

    // MARK: - Stepper

    func testTheStepperRendersAtEveryPosition() throws {
        for position in LiveActivityStep.allCases {
            let image = try render(
                LiveActivityStepperBar(position: position, liveRange: nil),
                width: lockScreenWidth
            )

            XCTAssertGreaterThan(image.size.width, 0, "\(position) rendered nothing")
            XCTAssertGreaterThan(image.size.height, 0, "\(position) rendered nothing")
        }
    }

    /// The stepper's whole job is to look different at each leg. Compared over the BAR ROW ALONE — the top
    /// strip, above the labels — because the labels change at every position anyway and would make this
    /// pass while the bar stayed frozen.
    func testTheBarItselfFillsOneSegmentFurtherAtEveryPosition() throws {
        var bars: [LiveActivityStep: Data] = [:]
        for position in LiveActivityStep.allCases {
            let image = try render(LiveActivityStepperBar(position: position, liveRange: nil), width: lockScreenWidth)
            bars[position] = try topStrip(of: image)
        }

        for outer in LiveActivityStep.allCases {
            for inner in LiveActivityStep.allCases where inner.rawValue > outer.rawValue {
                XCTAssertNotEqual(bars[outer], bars[inner], "the bar is identical at \(outer) and \(inner)")
            }
        }
    }

    /// Each leg label gets a quarter of the card and must fit it OUTRIGHT, at full size, in every language.
    /// Measured rather than rendered: the labels are capped to one line, so a label that outgrows its
    /// segment does not wrap — it silently shrinks or truncates, and a rendered height never notices.
    func testEveryLegLabelFitsItsQuarterOfTheCardInEveryLanguage() {
        let padding: CGFloat = 32
        let gaps = CGFloat(LiveActivityStep.count - 1) * 4
        let segment = (lockScreenWidth - padding - gaps) / CGFloat(LiveActivityStep.count)
        let font = UIFont.systemFont(ofSize: 9, weight: .semibold)

        for region in ["en", "cs", "sk", "uk", "ru"] {
            CoreL10n.apply(languageTag: region)

            for step in LiveActivityStep.allCases {
                let width = (step.label as NSString).size(withAttributes: [.font: font]).width

                XCTAssertLessThanOrEqual(
                    width, segment,
                    "\(region) · \(step.label) needs \(width)pt of a \(segment)pt segment"
                )
            }
        }
    }

    // MARK: - Headline

    func testTheHeadlineRendersEveryFormTheCardCanTake() throws {
        let headlines: [LiveActivityHeadline] = [
            .transition(from: .confirmed, to: .onTheWay),
            .transition(from: .onTheWay, to: .cleaning),
            .completed,
            .cancelled,
            .generic
        ]

        var rendered: [Data] = []
        for headline in headlines {
            let data = try png(LiveActivityHeadlineLine(headline: headline), width: lockScreenWidth)
            XCTAssertFalse(data.isEmpty)
            rendered.append(data)
        }

        XCTAssertEqual(Set(rendered).count, headlines.count, "two headline forms render identically")
    }

    func testTheHeadlineStaysOnOneLineInEveryLanguage() throws {
        let headline = LiveActivityHeadline.transition(from: .onTheWay, to: .cleaning)
        let english = try height(LiveActivityHeadlineLine(headline: headline), width: lockScreenWidth)

        for region in ["en", "cs", "sk", "uk", "ru"] {
            CoreL10n.apply(languageTag: region)
            let measured = try height(LiveActivityHeadlineLine(headline: headline), width: lockScreenWidth)

            XCTAssertEqual(measured, english, accuracy: 1, "\(region) wrapped the headline")
        }
    }

    // MARK: - Compact island

    /// The compact slot is roughly this narrow. Dots that overflow it get clipped by the system, so their
    /// drawn width has to stay inside it — in every language, because the accessibility label changes but
    /// the drawing must not.
    func testTheStepDotsFitTheCompactSlot() throws {
        for region in ["en", "cs", "sk", "uk", "ru"] {
            CoreL10n.apply(languageTag: region)
            let image = try render(LiveActivityStepDots(position: .cleaning), width: compactWidth)

            XCTAssertLessThanOrEqual(image.size.width, compactWidth, "\(region) overflowed the compact slot")
            XCTAssertGreaterThan(image.size.width, 0)
        }
    }

    func testTheStepDotsLookDifferentAtEachPositionAndWhenUnplaced() throws {
        var rendered = try LiveActivityStep.allCases.map {
            try png(LiveActivityStepDots(position: $0), width: compactWidth)
        }
        try rendered.append(png(LiveActivityStepDots(position: nil), width: compactWidth))

        XCTAssertEqual(Set(rendered).count, rendered.count, "two step-dot states render identically")
    }

    // MARK: - Finish time

    /// The compact readout has to survive the Dynamic Island's one-row slot, so it drops the caption the
    /// lock screen carries. Bounded against the slot itself rather than against the full readout: a caption
    /// left in by accident is still shorter than the lock screen's, and would slip a relative check.
    func testTheCompactFinishTimeIsASingleRowInsideTheIslandSlot() throws {
        let islandRowHeight: CGFloat = 24
        let full = try render(LiveActivityFinishTime(finish: finish, compact: false), width: 120)
        let compact = try render(LiveActivityFinishTime(finish: finish, compact: true), width: compactWidth)

        XCTAssertLessThanOrEqual(compact.size.height, islandRowHeight, "the compact readout kept its caption")
        XCTAssertGreaterThan(full.size.height, islandRowHeight, "the lock-screen readout lost its caption")
        XCTAssertLessThanOrEqual(compact.size.width, compactWidth)
    }

    /// A live window counts down; without one the same slot shows the wall clock; a card with nothing left
    /// to finish shows neither. Three readouts, three different pictures.
    func testTheCompactReadoutDistinguishesCountingFromClockFromDone() throws {
        let counting = try png(LiveActivityCompactReadout(model: model("inProgress")), width: compactWidth)
        let clock = try png(LiveActivityCompactReadout(model: model("inProgress", live: false)), width: compactWidth)
        let done = try png(LiveActivityCompactReadout(model: model("completed")), width: compactWidth)

        XCTAssertEqual(Set([counting, clock, done]).count, 3, "two compact readouts are indistinguishable")
    }

    func testTheCompactReadoutFitsItsSlotInEveryLanguage() throws {
        for region in ["en", "cs", "sk", "uk", "ru"] {
            CoreL10n.apply(languageTag: region)
            let image = try render(LiveActivityCompactReadout(model: model("inProgress")), width: compactWidth)

            XCTAssertLessThanOrEqual(image.size.width, compactWidth, "\(region) overflowed the compact slot")
        }
    }

    // MARK: - Brand

    /// The lockup draws art out of Core's bundle. In the widget that bundle ships inside the .appex; here
    /// it just has to produce something other than an empty frame.
    func testTheBrandLockupRenders() throws {
        let image = try render(LiveActivityBrandLockup(), width: 120)

        XCTAssertGreaterThan(image.size.width, 22)
        XCTAssertGreaterThan(image.size.height, 0)
    }

    // MARK: - Rendering

    private func render(_ view: some View, width: CGFloat) throws -> UIImage {
        let renderer = ImageRenderer(content: view.frame(width: width).fixedSize(horizontal: false, vertical: true))
        renderer.scale = 2
        return try XCTUnwrap(renderer.uiImage, "the view rendered no image")
    }

    private func png(_ view: some View, width: CGFloat) throws -> Data {
        try XCTUnwrap(render(view, width: width).pngData(), "the rendered image produced no PNG")
    }

    private func height(_ view: some View, width: CGFloat) throws -> CGFloat {
        try render(view, width: width).size.height
    }

    /// The bar row on its own, clear of the labels underneath it.
    private func topStrip(of image: UIImage) throws -> Data {
        let source = try XCTUnwrap(image.cgImage, "the render produced no bitmap")
        let strip = try XCTUnwrap(
            source.cropping(to: CGRect(x: 0, y: 0, width: source.width, height: 8)),
            "the render was too short to hold a bar row"
        )
        return try XCTUnwrap(UIImage(cgImage: strip).pngData())
    }
}
