import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The confirmation ticket on the phones, languages, schemes and states of the build spec, each captured from
/// the real presented sheet and measured from its own pixels and view tree: no blank band over 64 pt between
/// visible things, at most 12 pt of rest above the actions, and no scrolling at default text size.
@MainActor
final class AppointmentConfirmationVisualTests: XCTestCase {
    static let phone = CGSize(width: 393, height: 852)
    static let smallPhone = CGSize(width: 375, height: 667)

    private var restoreBundle = L10n.bundle
    private var restoreTag = CoreL10n.languageTag

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    func testTheTicketOnThePhoneInBothLanguagesAndSchemes() async throws {
        _ = try await waitForForegroundScene()
        for language in ["en", "cs"] {
            for scheme in [ColorScheme.light, .dark] {
                let shot = try await capture("iphone-393x852-\(language)-\(scheme)", language: language, scheme: scheme)
                if language == "en", scheme == .light {
                    try assertReadingOrder(shot.spoken)
                }
                if scheme == .light {
                    try assertActionsMatchTheFrames(shot, language: language)
                }
            }
        }
    }

    func testTheTicketInEveryStateOfTheOrder() async throws {
        _ = try await waitForForegroundScene()
        let held = FakeOrderClient()
        held.holdDetails = true
        let loading = try await capture("iphone-393x852-en-light-loading", client: held)
        XCTAssertTrue(loading.spoken.contains(L10n.Booking.successDetailsLoading), "the money reads as loading")
        XCTAssertTrue(
            loading.spoken
                .contains { $0.hasPrefix(L10n.Booking.successFreeUntil("").trimmingCharacters(in: .whitespaces)) },
            "a slot days ahead keeps the deadline sentence while it loads: \(loading.spoken)"
        )

        for (language, scheme) in [("en", ColorScheme.light), ("cs", .dark)] {
            let failed = FakeOrderClient()
            failed.detailResults = [.failure(ApiError(httpStatus: 500))]
            let shot = try await capture(
                "iphone-393x852-\(language)-\(scheme)-unavailable", language: language, scheme: scheme, client: failed
            )
            XCTAssertTrue(shot.spoken.contains(L10n.retry), "Try again is offered")
            XCTAssertTrue(shot.spoken.contains(L10n.Booking.successFreeWhile), "no deadline is guessed")
            XCTAssertFalse(shot.spoken.contains { $0.contains(L10n.Credit.paidWithCredit) }, "no guessed credit row")
        }

        for language in ["en", "cs"] {
            try await capture("iphone-393x852-\(language)-light-cash", language: language, sample: .cash)
        }
        try await capture("iphone-393x852-en-light-nocredit", sample: .noCredit)
        for language in ["en", "cs"] {
            let late = try await capture("iphone-393x852-\(language)-light-late", language: language, sample: .late)
            XCTAssertTrue(late.spoken.contains(L10n.Booking.successFreeWhile), "a deadline behind says the rule")
        }
    }

    func testTheRemedyIsReadInEveryStateFromTheDraftsPayment() async throws {
        _ = try await waitForForegroundScene()
        for sample in [Sample.card, .cash] {
            let held = FakeOrderClient()
            held.holdDetails = true
            let failed = FakeOrderClient()
            failed.detailResults = [.failure(ApiError(httpStatus: 500))]
            let loaded = FakeOrderClient()
            loaded.detailResults = [.success(sample.order())]
            for (state, client) in [("loaded", loaded), ("loading", held), ("unavailable", failed)] {
                let canvas = try sheetCanvas(scheme: .light, client: client, booking: sample.booking())
                defer { canvas.close() }
                await canvas.settle(until: { canvas.host.presentedViewController?.view.window != nil })
                try await settlePresentedSheet(canvas)
                let spoken = try spokenOrder(XCTUnwrap(canvas.host.presentedViewController).view)
                XCTAssertTrue(
                    spoken.contains(BookingSuccessView.findingSpoken(sample.booking())),
                    "\(sample) \(state) reads the finding with its remedy: \(spoken)"
                )
            }
        }
    }

    func testTheSmallPhoneFitsWithoutScrolling() async throws {
        _ = try await waitForForegroundScene()
        let card = try await capture("iphone-se-375x667-en-light", size: Self.smallPhone)
        let cash = try await capture(
            "iphone-se-375x667-cs-dark-cash", language: "cs", scheme: .dark, size: Self.smallPhone, sample: .cash
        )
        for shot in [card, cash] {
            XCTAssertEqual(shot.sheet.height, 610, accuracy: 1, "\(shot.name): the SE's .large sheet")
        }
    }

    func testLargeTextScrollsTheTicketAndPinsOnlyViewOrder() async throws {
        _ = try await waitForForegroundScene()
        for (language, scheme, name) in [
            ("en", ColorScheme.light, "iphone-393x852-en-light-ax1"), ("cs", .dark, "iphone-393x852-cs-dark-ax1")
        ] {
            let top = try await capture(name, language: language, scheme: scheme, textSize: .accessibility1)
            XCTAssertGreaterThan(top.scrollRange, 0, "\(name): the ticket scrolls")
            XCTAssertTrue(top.edgeShown, "\(name): the pinned bar takes its edge while the ticket runs under it")
            if language == "en" {
                let end = try await capture(
                    name + "-end", language: language, scheme: scheme, textSize: .accessibility1, scrollToEnd: true
                )
                XCTAssertFalse(end.edgeShown, "at the end nothing runs under the bar")
                let home = try XCTUnwrap(end.frames[L10n.Booking.successGoHome], "Back to home ends the scroll")
                let view = try XCTUnwrap(end.frames[L10n.Orders.viewOrder])
                XCTAssertLessThan(home.maxY, view.minY, "only View order is pinned")
            }
        }
    }
}

// MARK: - Samples

extension AppointmentConfirmationVisualTests {
    enum Sample {
        case card, cash, noCredit, late

        static var saturday: Date {
            let soon = Date().addingTimeInterval(2 * 86400)
            return Calendar.current.nextDate(
                after: soon, matching: DateComponents(hour: 9, minute: 0, weekday: 7), matchingPolicy: .nextTime
            ) ?? soon
        }

        var cleaningAt: Date {
            guard self == .late else { return Self.saturday }
            let soon = Date().addingTimeInterval(15 * 3600)
            return Calendar.current.date(bySetting: .minute, value: 0, of: soon) ?? soon
        }

        var minutes: Int {
            self == .cash ? 120 : 180
        }

        func booking(language: String = "en") -> BookingSuccess {
            let czech = language == "cs"
            var booking = BookingSuccess(
                orderId: "synthetic-order", confirmationCode: "130AA4", cleaningAt: cleaningAt,
                estimatedMinutes: minutes, street: self == .late ? "Vinohradská 1024/46" : "Zenklova 6",
                city: self == .late ? "Praha 2" : "Praha 8", appliesCredit: self == .card || self == .late
            )
            booking.requiredEmployees = self == .cash ? 1 : 2
            booking.rooms = self == .cash ? 1 : 2
            booking.bathrooms = 1
            booking.basketNames = [czech ? "Obecný úklid" : "General Cleaning"]
            booking.extraNames = [czech ? "Čištění vnitřku trouby" : "Inside oven cleaning"]
            booking.paysCash = self == .cash
            return booking
        }

        func order(status: Int = 0) -> CustomerOrderDetail {
            let credit: Double = self == .card || self == .late ? 250 : 0
            let total: Double = self == .cash ? 1000 : 1150
            let reference = switch self {
            case .card: "ORD-88BC31F3"
            case .cash: "ORD-5D07A1C2"
            case .noCredit: "ORD-3B9E4F70"
            case .late: "ORD-6A12F0B9"
            }
            return OrderFixtures.detail(
                statusCode: Code(value: status), displayOrderNumber: reference, cleaningDateTime: cleaningAt,
                address: OrderAddress(
                    street: self == .late ? "Vinohradská 1024/46" : "Zenklova 6",
                    city: self == .late ? "Praha 2" : "Praha 8"
                ),
                rooms: self == .cash ? 1 : 2, bathrooms: 1, estimatedMinutes: minutes, total: total,
                creditAppliedAmount: credit, amountDueOnCard: self == .cash ? 0 : total - credit,
                paymentType: Code(type: "PaymentType", value: self == .cash ? 1 : 2),
                paymentStatus: Code(type: "PaymentStatus", value: 1), currencyCode: "CZK",
                freeCancellationHours: 24
            )
        }
    }

    struct Shot {
        let name: String
        let sheet: CGRect
        let scrollRange: CGFloat
        let blank: (height: CGFloat, at: CGFloat)
        let rest: CGFloat
        let edgeShown: Bool
        let spoken: [String]
        let frames: [String: CGRect]
    }
}

// MARK: - Capturing

extension AppointmentConfirmationVisualTests {
    @discardableResult
    func capture(
        _ name: String, language: String = "en", scheme: ColorScheme = .light, size: CGSize = phone,
        textSize: DynamicTypeSize = .large, client: FakeOrderClient? = nil, sample: Sample = .card,
        scrollToEnd: Bool = false
    ) async throws -> Shot {
        try useLanguage(language)
        let client = client ?? FakeOrderClient()
        if client.detailResults.isEmpty, !client.holdDetails { client.detailResults = [.success(sample.order())] }
        let canvas = try sheetCanvas(
            scheme: scheme, size: size, textSize: textSize, locale: Locale(identifier: language), client: client,
            booking: sample.booking(language: language)
        )
        defer { canvas.close() }
        await canvas.settle(until: { canvas.host.presentedViewController?.view.window != nil })
        try await settlePresentedSheet(canvas)
        await canvas.settle(timeout: 4, until: { self.animatedMascots(canvas).isEmpty })
        try await Task.sleep(nanoseconds: 300_000_000)
        let sheet = try XCTUnwrap(canvas.host.presentedViewController)
        let scroll = try XCTUnwrap(allViews(under: sheet.view).compactMap { $0 as? UIScrollView }.first)
        if scrollToEnd {
            let bottom = scroll.contentSize.height - scroll.bounds.height + scroll.adjustedContentInset.bottom
            scroll.setContentOffset(CGPoint(x: 0, y: max(0, bottom)), animated: false)
            try await settlePresentedSheet(canvas, scroll: scroll)
            try await Task.sleep(nanoseconds: 300_000_000)
        }
        let shot = try measure(name, canvas: canvas, sheet: sheet.view, scroll: scroll)
        attach(canvas, name: name, scale: 2)
        let record = XCTAttachment(string: shot.report)
        record.name = name + " measured"
        record.lifetime = .keepAlways
        add(record)
        if !textSize.isAccessibilitySize {
            XCTAssertLessThanOrEqual(shot.scrollRange, 0.5, "\(name): nothing scrolls at default text size")
            XCTAssertLessThanOrEqual(shot.rest, 12, "\(name): rest above the actions")
        }
        XCTAssertLessThanOrEqual(shot.blank.height, 64, "\(name): blank band at y \(shot.blank.at)")
        return shot
    }

    /// Read from a 2x render: a row is blank when every pixel across the ticket's inner width is the sheet's
    /// ground or the ticket's surface. The pinned bar starts where the scroll view ends.
    func measure(_ name: String, canvas: HostedCanvas, sheet: UIView, scroll: UIScrollView) throws -> Shot {
        let scale = 2
        let image = try XCTUnwrap(snapshot(canvas, scale: CGFloat(scale)).cgImage)
        let frames = accessibilityFrames(sheet)
        let sheetFrame = canvas.frame(of: sheet)
        let barTop = Int(canvas.frame(of: scroll).maxY.rounded()) * scale
        let width = Int(canvas.window.bounds.width) * scale
        let pixels = try rgba(image, in: CGRect(x: 0, y: 0, width: width, height: barTop + 2))
        let groundAt = ((barTop - 4) * width + 6 * scale) * 4
        let ground = Array(pixels[groundAt ..< groundAt + 3])
        let inTicket = try XCTUnwrap(frames.filter { label, frame in
            [L10n.Booking.successFinding, L10n.Booking.successTotalLabel].contains { label.hasPrefix($0) }
                && frame.minY > sheetFrame.minY + 18 && Int(frame.maxY) * scale < barTop
        }.values.first, "\(name): no ticket row on screen")
        let surfaceAt = ((Int(inTicket.midY) * scale) * width + 24 * scale) * 4
        let surface = Array(pixels[surfaceAt ..< surfaceAt + 3])
        func near(_ index: Int, _ colour: [UInt8]) -> Bool {
            abs(Int(pixels[index]) - Int(colour[0])) <= 8 && abs(Int(pixels[index + 1]) - Int(colour[1])) <= 8
                && abs(Int(pixels[index + 2]) - Int(colour[2])) <= 8
        }
        func blank(_ row: Int, groundOnly: Bool = false) -> Bool {
            (20 * scale ..< width - 20 * scale).allSatisfy { column in
                let index = (row * width + column) * 4
                return near(index, ground) || (!groundOnly && near(index, surface))
            }
        }
        var run = 0
        var largest = (height: CGFloat(0), at: CGFloat(0))
        for row in Int((sheetFrame.minY + 18).rounded()) * scale ..< barTop {
            run = blank(row) ? run + 1 : 0
            let height = CGFloat(run) / CGFloat(scale)
            if height > largest.height { largest = (height, CGFloat(row - run + 1) / CGFloat(scale)) }
        }
        var restTop = barTop - 1
        while restTop > 0, blank(restTop, groundOnly: true) {
            restTop -= 1
        }
        let edge = (40 * scale ..< 60 * scale).contains { !near((barTop * width + $0) * 4, ground) }
        let viewport = scroll.bounds.height - scroll.adjustedContentInset.top - scroll.adjustedContentInset.bottom
        return Shot(
            name: name, sheet: sheetFrame, scrollRange: max(0, scroll.contentSize.height - viewport),
            blank: largest, rest: CGFloat(barTop - 1 - restTop) / CGFloat(scale), edgeShown: edge,
            spoken: spokenOrder(sheet), frames: frames
        )
    }

    func assertReadingOrder(_ spoken: [String]) throws {
        let order: [(String, (String) -> Bool)] = [
            ("title", { $0 == L10n.Booking.successTitle }),
            ("reference", { $0 == "ORD-88BC31F3" }),
            ("finding", { $0.hasPrefix(L10n.Booking.successFinding) }),
            ("deadline", { $0.hasPrefix(L10n.Booking.successFreeUntil("").trimmingCharacters(in: .whitespaces)) }),
            ("answer", { $0.contains(L10n.Booking.successTitle) == false && $0.contains("Zenklova") }),
            ("basket", { $0.contains("General Cleaning") }),
            ("total", { $0.hasPrefix(L10n.Booking.successTotalLabel) }),
            ("credit", { $0.hasPrefix(L10n.Credit.paidWithCredit) }),
            ("card", { $0.hasPrefix(L10n.Credit.paidByCard) }),
            ("home", { $0 == L10n.Booking.successGoHome }),
            ("view", { $0 == L10n.Orders.viewOrder })
        ]
        var cursor = spoken.startIndex
        for (label, matches) in order {
            let found = try XCTUnwrap(
                spoken[cursor...].firstIndex(where: matches), "\(label) is not read after its predecessor: \(spoken)"
            )
            cursor = spoken.index(after: found)
        }
        let record = XCTAttachment(string: spoken.joined(separator: "\n"))
        record.name = "Ticket accessibility reading order"
        record.lifetime = .keepAlways
        add(record)
    }

    /// The frames' button widths (`flex: 1 1 auto`, View order growing 1.4 to Back to home's 1), measured on the
    /// mock at 393 pt: en 176.9 / 172.1, cs 136.5 / 212.5.
    func assertActionsMatchTheFrames(_ shot: Shot, language: String) throws {
        let frames: [String: (home: CGFloat, view: CGFloat)] = ["en": (176.9, 172.1), "cs": (136.5, 212.5)]
        let expected = try XCTUnwrap(frames[language])
        let home = try XCTUnwrap(shot.frames[L10n.Booking.successGoHome], "\(shot.name): Back to home")
        let view = try XCTUnwrap(shot.frames[L10n.Orders.viewOrder], "\(shot.name): View order")
        XCTAssertEqual(home.width, expected.home, accuracy: 6, "\(shot.name): Back to home's share")
        XCTAssertEqual(view.width, expected.view, accuracy: 6, "\(shot.name): View order's share")
        XCTAssertEqual(view.minX - home.maxX, Spacing.s, accuracy: 1, "\(shot.name): one gap between them")
        XCTAssertEqual(view.maxX - home.minX, Self.phone.width - 2 * Spacing.m, accuracy: 1, "\(shot.name): one row")
    }

    func useLanguage(_ language: String) throws {
        let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
        let languageURL = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"))
        L10n.bundle = try XCTUnwrap(Bundle(url: languageURL))
        CoreL10n.apply(languageTag: language)
    }

    func makeBookingVM() -> BookingViewModel {
        BookingViewModel(
            catalogClient: FakeCatalogClient(), quoteClient: FakeQuoteClient(),
            consentClient: FakeConsentStatusClient(), tokenStore: FakeTokenStore(nil),
            savedCardClient: FakeSavedCardClient()
        )
    }

    func sheetCanvas(
        scheme: ColorScheme, size: CGSize = phone, textSize: DynamicTypeSize = .large,
        locale: Locale = Locale(identifier: "en"), client: FakeOrderClient? = nil, reduceMotion: Bool = false,
        booking: BookingSuccess? = Sample.card.booking(), submitting: BookingViewModel? = nil
    ) throws -> HostedCanvas {
        let client = client ?? FakeOrderClient()
        if client.detailResults.isEmpty, !client.holdDetails { client.detailResults = [.success(Sample.card.order())] }
        let vm = submitting ?? makeBookingVM()
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let canvas = try HostedCanvas(size: size) {
            PresentedBookingConfirmation(
                sheet: BookingSheetView(
                    vm: vm, geocoding: CLGeocoderGeocodingService(), mapProvider: PreviewMapProvider(),
                    paymentSheet: FakePaymentSheetPresenter(), orderClient: client, onDismiss: {},
                    initialSuccess: submitting == nil ? booking : nil, submitsOnAppear: submitting != nil
                ), scene: scene, locale: locale, textSize: textSize, scheme: scheme, reduceMotion: reduceMotion
            )
            .preferredColorScheme(scheme)
        }
        XCTAssertTrue(canvas.window.windowScene === scene, "phase must come from the actual test window's scene")
        return canvas
    }

    func waitForForegroundScene() async throws -> UIWindowScene {
        FileHandle.standardOutput.write(Data("CLEANSIA_CONFIRMATION_FOREGROUND_WAIT\n".utf8))
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let deadline = ProcessInfo.processInfo.systemUptime + 20
        while scene.activationState != .foregroundActive || UIApplication.shared.applicationState != .active,
              ProcessInfo.processInfo.systemUptime < deadline
        {
            try await Task.sleep(nanoseconds: 100_000_000)
        }
        return try XCTUnwrap(
            scene.activationState == .foregroundActive && UIApplication.shared
                .applicationState == .active ? scene : nil,
            "Real foreground wait expired: scene=\(scene.activationState.rawValue), "
                + "application=\(UIApplication.shared.applicationState.rawValue)"
        )
    }

    func settlePresentedSheet(_ canvas: HostedCanvas, scroll: UIScrollView? = nil) async throws {
        let sheet = try XCTUnwrap(canvas.host.presentedViewController)
        let deadline = ProcessInfo.processInfo.systemUptime + 5
        var previousFrame = CGRect.null
        var previousOffset: CGPoint?
        var stableSamples = 0
        while stableSamples < 6, ProcessInfo.processInfo.systemUptime < deadline {
            canvas.window.layoutIfNeeded()
            let frame = canvas.frame(of: sheet.view).integral
            let transitioning = sheet.transitionCoordinator != nil || sheet.isBeingPresented || sheet.isBeingDismissed
            if !transitioning, frame == previousFrame, scroll?.contentOffset == previousOffset {
                stableSamples += 1
            } else {
                stableSamples = 0
            }
            previousFrame = frame
            previousOffset = scroll?.contentOffset
            try await Task.sleep(nanoseconds: 60_000_000)
        }
        XCTAssertEqual(stableSamples, 6, "the modal transition and scroll position must settle before capture")
    }

    func animatedMascots(_ canvas: HostedCanvas) -> [UIImageView] {
        allViews(under: canvas.window).compactMap { $0 as? UIImageView }.filter {
            String(describing: type(of: $0)).contains("MascotAnimationView")
        }
    }

    func allViews(under view: UIView) -> [UIView] {
        [view] + view.subviews.flatMap { self.allViews(under: $0) }
    }

    func snapshot(_ canvas: HostedCanvas, scale: CGFloat? = nil) -> UIImage {
        let format = UIGraphicsImageRendererFormat()
        format.scale = scale ?? UIScreen.main.scale
        return UIGraphicsImageRenderer(bounds: canvas.window.bounds, format: format).image { _ in
            canvas.window.drawHierarchy(in: canvas.window.bounds, afterScreenUpdates: true)
        }
    }

    func attach(_ canvas: HostedCanvas, name: String, scale: CGFloat? = nil) {
        let attachment = XCTAttachment(image: snapshot(canvas, scale: scale))
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }

    func spokenOrder(_ node: NSObject) -> [String] {
        if node.isAccessibilityElement { return [node.accessibilityLabel ?? ""] }
        let children = node.accessibilityElements as? [NSObject] ?? (node as? UIView)?.subviews ?? []
        return children.flatMap { spokenOrder($0) }
    }

    func accessibilityFrames(_ node: NSObject) -> [String: CGRect] {
        if node.isAccessibilityElement { return [node.accessibilityLabel ?? "": node.accessibilityFrame] }
        let children = node.accessibilityElements as? [NSObject] ?? (node as? UIView)?.subviews ?? []
        return children.reduce(into: [:]) { frames, child in
            frames.merge(accessibilityFrames(child)) { first, _ in first }
        }
    }

    /// The image's sRGB bytes inside `rect` (in the image's own pixels), row by row from the top.
    func rgba(_ image: CGImage, in rect: CGRect) throws -> [UInt8] {
        let width = Int(rect.width.rounded())
        let height = Int(rect.height.rounded())
        let space = try XCTUnwrap(CGColorSpace(name: CGColorSpace.sRGB))
        var bytes = [UInt8](repeating: 0, count: width * height * 4)
        let drawn = bytes.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(
                data: buffer.baseAddress, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width * 4,
                space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            ) else { return false }
            context.draw(image, in: CGRect(
                x: -rect.minX.rounded(), y: rect.maxY.rounded() - CGFloat(image.height),
                width: CGFloat(image.width), height: CGFloat(image.height)
            ))
            return true
        }
        XCTAssertTrue(drawn, "could not read \(rect)")
        return bytes
    }
}

extension AppointmentConfirmationVisualTests.Shot {
    var report: String {
        [
            "\(name)",
            "sheet \(sheet.integral) height \(sheet.height)",
            "scroll range \(scrollRange)",
            "largest blank band \(blank.height) pt at y \(blank.at)",
            "rest above the actions \(rest)",
            "pinned bar edge shown \(edgeShown)",
            "frames:"
        ].joined(separator: "\n") + "\n" + frames
            .sorted { $0.value.minY < $1.value.minY }
            .map { "  \($0.value.integral) \($0.key)" }
            .joined(separator: "\n")
    }
}

struct PresentedBookingConfirmation: View {
    let sheet: BookingSheetView
    let scene: UIWindowScene
    let locale: Locale
    let textSize: DynamicTypeSize
    let scheme: ColorScheme
    var reduceMotion = false
    @State private var presented = false

    /// A UIKit-created hosting window does not supply the SwiftUI App scene's environment to its modal.
    /// Bridge only the real window scene's current state, rather than forcing the animation on.
    static func phase(of scene: UIWindowScene) -> ScenePhase {
        switch scene.activationState {
        case .foregroundActive: .active
        case .foregroundInactive: .inactive
        case .background, .unattached: .background
        @unknown default: .background
        }
    }

    var body: some View {
        CleansiaColors.background
            .onAppear { presented = true }
            .sheet(isPresented: $presented) {
                sheet
                    .environment(\.locale, locale)
                    .environment(\.dynamicTypeSize, textSize)
                    .environment(\.scenePhase, Self.phase(of: scene))
                    .environment(\._accessibilityReduceMotion, reduceMotion)
                    .preferredColorScheme(scheme)
            }
    }
}
