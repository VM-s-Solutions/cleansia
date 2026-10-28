import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

@MainActor
final class OrderReviewLayoutTests: XCTestCase {
    private let size = CGSize(width: 320, height: 568)

    func testThePendingRatingFooterFitsAtMapFocusInEveryLanguageAndBothSchemes() async throws {
        for language in ["en", "cs", "sk", "ru", "uk"] {
            for scheme in [ColorScheme.light, .dark] {
                try await assertPendingFooterFits(language: language, scheme: scheme, textSize: .large)
            }
        }
    }

    func testThePendingRatingFooterFitsAtMapFocusWithAccessibilityText() async throws {
        for scheme in [ColorScheme.light, .dark] {
            try await assertPendingFooterFits(language: "cs", scheme: scheme, textSize: .accessibility3)
        }
    }

    func testTheReviewSheetReservesSpaceForItsActionsBelowTheScrollView() async throws {
        let restore = L10n.bundle
        L10n.bundle = try localeBundle("en")
        defer { L10n.bundle = restore }
        for scheme in [ColorScheme.light, .dark] {
            let canvas = try HostedCanvas(size: size) {
                SubmitReviewSheet(
                    existingReview: nil,
                    isSubmitting: false,
                    errorMessage: nil,
                    onConfirm: { _, _, _, _ in },
                    onDismiss: {},
                    dismissLabel: L10n.OrderReview.promptNotNow,
                    titleOverride: L10n.OrderReview.promptTitle
                )
                .environment(\.colorScheme, scheme)
            }
            defer { canvas.close() }
            await canvas.settle { !canvas.views(of: UITextView.self).isEmpty }
            let scroll = try XCTUnwrap(canvas.views(of: UIScrollView.self).first { !($0 is UITextView) })
            let frame = canvas.frame(of: scroll)

            XCTAssertGreaterThan(frame.height, 100)
            XCTAssertLessThan(frame.maxY, size.height - 100, "the pinned actions have their own visible space")
            attach(canvas, name: "Rating prompt 320pt \(scheme)")
        }
    }

    func testTheRatingPromptPresentsAsANativeSheet() async throws {
        let restore = L10n.bundle
        L10n.bundle = try localeBundle("en")
        defer { L10n.bundle = restore }
        for scheme in [ColorScheme.light, .dark] {
            let canvas = try HostedCanvas(size: UIScreen.main.bounds.size) {
                PresentedReviewLayout()
                    .preferredColorScheme(scheme)
            }
            defer {
                canvas.host.dismiss(animated: false)
                canvas.close()
            }
            var previousFrame: CGRect?
            var stablePasses = 0
            await canvas.settle(timeout: 5) {
                guard let presented = canvas.host.presentedViewController,
                      presented.viewIfLoaded?.window != nil,
                      !presented.isBeingPresented,
                      presented.transitionCoordinator == nil
                else {
                    stablePasses = 0
                    return false
                }
                let frame = presented.view.convert(presented.view.bounds, to: canvas.window)
                stablePasses = frame == previousFrame ? stablePasses + 1 : 0
                previousFrame = frame
                return stablePasses >= 3
            }
            let presented = try XCTUnwrap(canvas.host.presentedViewController)
            XCTAssertNotNil(presented.sheetPresentationController)
            XCTAssertFalse(presented.isBeingPresented)
            XCTAssertNil(presented.transitionCoordinator)
            XCTAssertGreaterThanOrEqual(stablePasses, 3, "the native sheet has finished presenting")
            let frame = presented.view.convert(presented.view.bounds, to: canvas.window)
            XCTAssertGreaterThan(frame.height, canvas.window.bounds.height * 0.75)
            XCTAssertGreaterThanOrEqual(frame.minY, -0.5)
            XCTAssertLessThanOrEqual(frame.maxY, canvas.window.bounds.height + 0.5)
            attach(canvas, name: "Presented rating prompt \(scheme)")
        }
    }

    private func assertPendingFooterFits(
        language: String,
        scheme: ColorScheme,
        textSize: DynamicTypeSize
    ) async throws {
        let restore = L10n.bundle
        L10n.bundle = try localeBundle(language)
        defer { L10n.bundle = restore }
        let probes = LayoutProbes()
        let canvas = try HostedCanvas(size: size) {
            PendingReviewLayout(probes: probes)
                .environment(\.locale, Locale(identifier: language))
                .environment(\.colorScheme, scheme)
                .environment(\.dynamicTypeSize, textSize)
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["footer"] != nil && probes.frames["header"] != nil }
        let header = try XCTUnwrap(probes.frames["header"])
        let footer = try XCTUnwrap(probes.frames["footer"])

        XCTAssertGreaterThanOrEqual(header.minY, -0.5)
        XCTAssertGreaterThanOrEqual(footer.minY, header.maxY)
        XCTAssertLessThanOrEqual(footer.maxY, size.height + 0.5)
        XCTAssertGreaterThan(footer.height, 4 * 44, "all four actions retain their touch targets")
        if textSize == .large {
            let body = try XCTUnwrap(probes.frames["body"])
            let hero = try XCTUnwrap(probes.frames["hero"])
            XCTAssertLessThanOrEqual(hero.maxY, body.maxY + 0.5, "the completion heading is fully visible")
        }
        attach(canvas, name: "Pending rating MapFocus 320pt \(language) \(scheme) \(textSize)")
    }

    private func attach(_ canvas: HostedCanvas, name: String) {
        let renderer = UIGraphicsImageRenderer(bounds: canvas.window.bounds)
        let image = renderer.image { _ in
            canvas.window.drawHierarchy(in: canvas.window.bounds, afterScreenUpdates: true)
        }
        let attachment = XCTAttachment(image: image)
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    private func localeBundle(_ language: String) throws -> Bundle {
        let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
        let url = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"))
        return try XCTUnwrap(Bundle(url: url))
    }
}

private struct PendingReviewLayout: View {
    @State private var headerHeight: CGFloat = 0
    @State private var footerHeight: CGFloat = 0
    let probes: LayoutProbes

    private let order = OrderFixtures.detail(
        statusCode: .init(value: 5),
        displayOrderNumber: "A-1041",
        cleaningDateTime: Date(timeIntervalSince1970: 1_792_008_000)
    )

    var body: some View {
        SnapSheet(
            anchor: .constant(.mapFocus),
            minimumSheetHeight: headerHeight + footerHeight + OrderDetailReviewLayout.handleAndBodyClearance
        ) {
            CleansiaColors.background
        } content: {
            VStack(spacing: 0) {
                OrderDetailCompactHeader(order: order)
                    .probed("header", in: probes)
                ScrollView {
                    OrderStatusHero(order: order)
                        .padding(.horizontal, Spacing.ml)
                        .padding(.top, Spacing.xs)
                        .probed("hero", in: probes)
                }
                .frame(maxHeight: .infinity)
                .probed("body", in: probes)
                OrderDetailActionsFooter(
                    showReview: order.needsReview,
                    showRebook: true,
                    showMakeRecurring: true,
                    showCancel: false,
                    showCleanerDidNotArrive: false,
                    showReportIssue: true,
                    cancelEnabled: true,
                    onReview: {},
                    onRebook: {},
                    onMakeRecurring: {},
                    onCancel: {},
                    onCleanerDidNotArrive: {},
                    onReportIssue: {}
                )
                .probed("footer", in: probes)
            }
            .onPreferenceChange(OrderDetailHeaderHeight.self) { headerHeight = $0 }
            .onPreferenceChange(OrderDetailFooterHeight.self) { footerHeight = $0 }
        }
    }
}

private struct PresentedReviewLayout: View {
    @State private var showingReview = false

    var body: some View {
        CleansiaColors.background
            .onAppear { showingReview = true }
            .sheet(isPresented: $showingReview) {
                SubmitReviewSheet(
                    existingReview: nil,
                    isSubmitting: false,
                    errorMessage: nil,
                    onConfirm: { _, _, _, _ in },
                    onDismiss: { showingReview = false },
                    dismissLabel: L10n.OrderReview.promptNotNow,
                    titleOverride: L10n.OrderReview.promptTitle
                )
            }
    }
}
