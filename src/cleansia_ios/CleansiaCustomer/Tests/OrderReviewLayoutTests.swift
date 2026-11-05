import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

@MainActor
final class OrderReviewLayoutTests: XCTestCase {
    private let size = CGSize(width: 320, height: 568)

    func testMobilePolishDetailVisualEvidence() async throws {
        let order = OrderFixtures.detail(
            statusCode: .init(value: 5), displayOrderNumber: "ORD-1042", cleaningDateTime: .now,
            address: OrderAddress(
                street: "Example Street 12",
                city: "Praha",
                zipCode: "120 00",
                country: "Czech Republic"
            ),
            rooms: 2, bathrooms: 1, estimatedMinutes: 60,
            specialInstructions: "Please clean the kitchen first.", accessInstructions: "Synthetic access note",
            total: 4838.57, originalSubtotal: 5692.44, promoDiscountAmount: 853.87, appliedDiscountSource: ._3,
            currencyCode: "CZK",
            assignedEmployees: [AssignedEmployeeDto(fullName: "Alex"), AssignedEmployeeDto(fullName: "Tereza")],
            review: CustomerOrderReview(rating: 4, comment: "Thank you for the careful clean.", tags: [], lines: [])
        )
        let photos = OrderPhotos(photos: [
            GetOrderPhotosOrderPhotoDto(id: "before", photoType: ._1),
            GetOrderPhotosOrderPhotoDto(id: "after", photoType: ._2),
            GetOrderPhotosOrderPhotoDto(id: "entrance", photoType: ._3)
        ], beforeCount: 1, afterCount: 1)
        for scheme in [ColorScheme.light, .dark] {
            let canvas = try HostedCanvas(size: CGSize(width: 390, height: 1150)) {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    OrderDetailCompactHeader(order: order)
                    CustomerOrderTrackerHero(order: order)
                    OrderHeroFactsStrip(order: order)
                    if let address = order.address { OrderAddressCard(address: address) }
                    if let review = order.review { OrderReviewCard(review: review, onLeaveReview: {}) }
                    AssignedCleanersCard(employees: order.assignedEmployees)
                    OrderInstructionsCard(order: order)
                    OrderPhotosSection(gallery: photos, onViewPhotos: {})
                }
                .padding(Spacing.m)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                .background(CleansiaColors.surface)
                .environment(\.colorScheme, scheme)
            }
            await canvas.settle()
            attach(canvas, name: "Mobile detail completed \(scheme)")
            canvas.close()
        }
    }

    func testLongDiscountedPriceFitsANarrowPhoneInCzechAndUkrainian() async throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        let order = OrderFixtures.detail(
            statusCode: .init(value: 5), total: 4838.57, originalSubtotal: 5692.44,
            promoDiscountAmount: 853.87, appliedDiscountSource: ._3, currencyCode: "CZK"
        )
        for language in ["cs", "uk"] {
            L10n.bundle = try localeBundle(language)
            let canvas = try HostedCanvas(size: CGSize(width: 375, height: 160)) {
                OrderHeroFactsStrip(order: order)
                    .padding(Spacing.ml)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                    .background(CleansiaColors.surface)
                    .environment(\.locale, Locale(identifier: language))
            }
            await canvas.settle()
            attach(canvas, name: "Long discounted price 375pt \(language)")
            canvas.close()
        }
    }

    func testMobilePolishOverduePanelVisualEvidence() async throws {
        let order = OrderFixtures.detail(
            statusCode: .init(value: 4), displayOrderNumber: "ORD-1042", cleaningDateTime: .now,
            estimatedMinutes: 60, total: 1000, currencyCode: "CZK",
            statusHistory: [OrderFixtures.track(statusValue: 4, createdOn: Date().addingTimeInterval(-4500))]
        )
        for anchor in [SnapAnchor.peek, .expanded] {
            let canvas = try HostedCanvas(size: CGSize(width: 390, height: 844)) {
                SnapSheet(anchor: .constant(anchor)) {
                    CleansiaColors.primaryContainer
                } ornament: {
                    MascotPuck(OrderDetailMascotArt.art(for: order.status))
                } content: {
                    OrderDetailContent(
                        order: order,
                        markets: .unavailable,
                        photos: .idle,
                        isDownloadingReceipt: false,
                        onLeaveReview: {},
                        onDownloadReceipt: {},
                        onViewPhotos: {}
                    )
                }
            }
            await canvas.settle()
            attach(canvas, name: "Mobile overdue \(anchor)")
            canvas.close()
        }
    }

    func testBookingAndDisputeAndGalleryVisualEvidence() async throws {
        let order = OrderFixtures.detail(
            statusCode: .init(value: 0), displayOrderNumber: "ORD-1042", cleaningDateTime: .now,
            address: OrderAddress(
                street: "Example Street 12",
                city: "Praha",
                zipCode: "120 00",
                country: "Czech Republic"
            ),
            rooms: 3, bathrooms: 2, estimatedMinutes: 90, total: 4838.57, currencyCode: "CZK"
        )
        var draft = BookingState()
        draft.street = "Example Street 12, Praha 8"
        draft.rooms = 3
        draft.bathrooms = 2
        draft.dirtiness = .heavy
        draft.selectedDate = "Thursday, 29 October"
        draft.selectedTime = "12:15"
        draft.promoCode = "TEST15"
        let summary = BookingPriceSummary(
            subtotal: 5692.44, dirtiness: .heavy, dirtinessSurcharge: 0,
            expressSurcharge: 0, expressLine: .notExpress, total: 4838.57,
            creditApplied: 0, creditBalance: 0
        )
        let summaryCanvas = try HostedCanvas(size: CGSize(width: 375, height: 812)) {
            ScrollView {
                SummaryCard(
                    state: draft, summary: summary, promoDiscount: 853.87, membershipDiscount: 0,
                    tierDiscount: 0, combinedServerDiscount: 0, unmetTierDiscountFloor: nil, currencyCode: "CZK"
                ).padding(Spacing.m)
            }.background(CleansiaColors.background)
        }
        await summaryCanvas.settle()
        attach(summaryCanvas, name: "Booking final summary 375pt")
        summaryCanvas.close()

        let confirmation = try HostedCanvas(size: CGSize(width: 375, height: 812)) {
            BookingSuccessView(
                BookingSuccess(
                    orderId: "order-1", confirmationCode: "ABC123", cleaningAt: order.cleaningDateTime,
                    estimatedMinutes: order.estimatedMinutes, street: "Example Street 12", city: "Praha"
                ),
                loadOrder: { _ in order }, onViewOrder: {}, onDone: {}
            )
        }
        await confirmation.settle()
        attach(confirmation, name: "Booking confirmation 375pt")
        confirmation.close()
    }

    func testDisputeAndGalleryVisualEvidence() async throws {
        let order = OrderFixtures.detail(displayOrderNumber: "ORD-1042")
        let client = FakeOrderClient()
        client.detailResults = [.success(order)]
        client.photosResults = [.success(OrderPhotos(photos: [
            GetOrderPhotosOrderPhotoDto(id: "after-1", photoType: ._2),
            GetOrderPhotosOrderPhotoDto(id: "before-1", photoType: ._1),
            GetOrderPhotosOrderPhotoDto(id: "entrance-1", photoType: ._3),
            GetOrderPhotosOrderPhotoDto(id: "unknown-1")
        ], beforeCount: 1, afterCount: 1))]
        let dispute = try HostedCanvas(size: CGSize(width: 375, height: 812)) {
            NavigationStack {
                CreateDisputeView(
                    orderId: "order-1",
                    repository: DisputeRepository(client: FakeDisputeClient()),
                    orderClient: client, snackbar: SnackbarController(), onCreated: { _ in }
                )
            }
        }
        await dispute.settle()
        attach(dispute, name: "Dispute public order number 375pt")
        dispute.close()

        let gallery = try HostedCanvas(size: CGSize(width: 375, height: 812)) {
            NavigationStack {
                OrderPhotosScreen(orderId: "order-1", client: client, snackbar: SnackbarController())
            }
        }
        await gallery.settle()
        attach(gallery, name: "Gallery separated evidence phases 375pt")
        gallery.close()
    }

    func testPendingCardVisualEvidence() async throws {
        for size in [DynamicTypeSize.large, .accessibility3] {
            let canvas = try HostedCanvas(size: CGSize(width: 375, height: 812)) {
                PaymentsContent(
                    state: .loaded(PaymentsSnapshot(receivables: [], cards: [])),
                    removingCardId: nil, paying: false, offersCardCapture: false,
                    cardConsentAccepted: true, addingCard: false,
                    cardCapture: SavedCardCapture(
                        attempt: SavedCardCaptureAttempt(generation: 0),
                        stage: .pending("synthetic")
                    ),
                    onRetry: {}, onPay: { _ in }, onRemoveRequested: { _ in },
                    onCardConsentChanged: { _ in }, onAddCard: {}, onCheckCard: {},
                    onContinueCard: {}, onRestartCard: {}
                )
                .environment(\.dynamicTypeSize, size)
                .environment(\.colorScheme, size == .large ? .light : .dark)
            }
            await canvas.settle()
            attach(canvas, name: "Pending card 375pt \(size)")
            if size.isAccessibilitySize {
                let scroll = try XCTUnwrap(canvas.views(of: UIScrollView.self).first {
                    $0.contentSize.height > $0.bounds.height
                })
                scroll.setContentOffset(
                    CGPoint(
                        x: 0,
                        y: scroll.contentSize.height - scroll.bounds.height + scroll.adjustedContentInset.bottom
                    ),
                    animated: false
                )
                await canvas.settle()
                attach(canvas, name: "Pending card 375pt accessibility3 actions scrolled")
            }
            canvas.close()
        }
    }

    func testStatusMascotVisualEvidence() async throws {
        let states: [(String, OrderStatus)] = [
            ("New", ._0), ("Confirmed", ._2), ("On the way", ._3),
            ("In progress", ._4), ("Completed", ._5)
        ]
        let canvas = try HostedCanvas(size: CGSize(width: 375, height: 812)) {
            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: Spacing.m) {
                ForEach(Array(states.enumerated()), id: \.offset) { _, entry in
                    VStack {
                        MascotPuck(OrderDetailMascotArt.art(for: entry.1))
                            .frame(width: 128, height: 128)
                        Text(entry.0).font(CleansiaTypography.labelMedium)
                    }
                }
            }
            .padding(Spacing.m)
            .frame(maxHeight: .infinity, alignment: .top)
            .background(CleansiaColors.surface)
        }
        await canvas.settle()
        attach(canvas, name: "All status mascots 375pt")
        canvas.close()
    }

    func testCarouselPartialFillAndMissingTimingVisualEvidence() async throws {
        let diagnostics = CarouselCaptureDiagnostics()
        let carousel = try HostedCanvas(size: CGSize(width: 375, height: 350)) {
            CarouselCaptureFixture(diagnostics: diagnostics)
        }
        await carousel.settle()
        attach(carousel, name: "Home actual carousel interval start 375pt")
        try await Task.sleep(nanoseconds: 2_000_000_000)
        let environment = XCTAttachment(string: diagnostics.description)
        environment.name = "Home carousel actual hosted environment"
        environment.lifetime = .keepAlways
        add(environment)
        attach(carousel, name: "Home actual carousel partial timed dot 375pt")
        try await Task.sleep(nanoseconds: 4_600_000_000)
        attach(carousel, name: "Home actual carousel next slide after six seconds 375pt")
        diagnostics.scenePhase = .inactive
        await carousel.settle()
        attach(carousel, name: "Home actual carousel inactive start 375pt")
        try await Task.sleep(nanoseconds: 6_500_000_000)
        attach(carousel, name: "Home actual carousel still inactive after six seconds 375pt")
        diagnostics.scenePhase = .active
        await carousel.settle()
        attach(carousel, name: "Home actual carousel resumed interval start 375pt")
        try await Task.sleep(nanoseconds: 2_000_000_000)
        attach(carousel, name: "Home actual carousel resumed partial timed dot 375pt")
        carousel.close()

        let order = OrderFixtures.detail(statusCode: .init(value: 4), estimatedMinutes: 60)
        let timing = try HostedCanvas(size: CGSize(width: 375, height: 250)) {
            VStack(alignment: .leading, spacing: Spacing.s) {
                OrderStatusHero(order: order)
                CustomerOrderTrackerHero(order: order)
            }
            .padding(Spacing.m)
            .frame(maxHeight: .infinity, alignment: .top)
            .background(CleansiaColors.surface)
        }
        await timing.settle()
        attach(timing, name: "In progress missing timing honest outline 375pt")
        timing.close()
    }

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

@MainActor
private final class CarouselCaptureDiagnostics: ObservableObject {
    @Published var scenePhase: ScenePhase = .active
    var description = "Environment probe has not appeared."
}

private struct CarouselCaptureFixture: View {
    @ObservedObject var diagnostics: CarouselCaptureDiagnostics

    var body: some View {
        UpsellCarousel(inputs: UpsellSlide.Inputs(), onAction: { _ in })
            .padding(Spacing.m)
            .frame(maxHeight: .infinity, alignment: .top)
            .background(CleansiaColors.background)
            .background(CarouselCaptureEnvironment { diagnostics.description = $0 })
            .environment(\.scenePhase, diagnostics.scenePhase)
    }
}

private struct CarouselCaptureEnvironment: View {
    @Environment(\.scenePhase) private var scenePhase
    @Environment(\.accessibilityVoiceOverEnabled) private var voiceOverEnabled
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let record: (String) -> Void

    var body: some View {
        GeometryReader { geometry in
            Color.clear.onAppear {
                record(
                    "scene=\(scenePhase); voiceOver=\(voiceOverEnabled); reduceMotion=\(reduceMotion); "
                        + "frame=\(geometry.frame(in: .global)); screen=\(UIScreen.main.bounds)"
                )
            }
        }
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
