import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// VoiceOver cannot drag: on the booking's last step the slide is one button named by its words, and activating it
/// books, as the slide does.
@MainActor
final class BookingSlideVoiceOverTests: XCTestCase {
    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

    func testVoiceOverBooksFromTheSlide() async throws {
        let vm = await CustomerCaptureFixture.booking()
        // The captures' day lies past the days the When step offers, and a booking refuses a time it no longer offers.
        let day = Calendar.current.date(byAdding: .day, value: 3, to: Date()) ?? Date()
        vm.update { draft in
            var next = draft
            next.selectedInstant = BookingTimeSlots.instant(date: day, timeLabel: "10:00")
            return next
        }
        await vm.refreshQuoteForTest()
        let payment = FakePaymentSheetPresenter()
        let canvas = try HostedCanvas(size: CGSize(width: 393, height: 852)) {
            BookingSheetView(
                vm: vm,
                geocoding: FixedGeocodingService(),
                mapProvider: PreviewMapProvider(),
                paymentSheet: payment,
                orderClient: FakeOrderClient(),
                onDismiss: {}
            )
        }
        defer { canvas.close() }
        await canvas.settle()
        let label = L10n.Booking.slideToConfirmPrice(BookingPricing.formatTotal(1150, currencyCode: "CZK"))
        let slide = try XCTUnwrap(Self.elements(under: canvas.window).first { $0.accessibilityLabel == label })

        XCTAssertTrue(slide.accessibilityTraits.contains(.button))
        XCTAssertFalse(slide.accessibilityTraits.contains(.notEnabled))
        slide.accessibilityActivate()
        await canvas.settle(timeout: 5) { !payment.presentations.isEmpty }
        XCTAssertEqual(payment.presentations.count, 1, "the card is asked for once")
    }

    private static func elements(under node: NSObject, depth: Int = 0) -> [NSObject] {
        guard depth < 80 else { return [] }
        if node.isAccessibilityElement { return [node] }
        if let children = node.accessibilityElements as? [NSObject], !children.isEmpty {
            return children.flatMap { elements(under: $0, depth: depth + 1) }
        }
        let count = node.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            return (0 ..< count).compactMap { node.accessibilityElement(at: $0) as? NSObject }
                .flatMap { elements(under: $0, depth: depth + 1) }
        }
        return ((node as? UIView)?.subviews ?? []).flatMap { elements(under: $0, depth: depth + 1) }
    }
}
