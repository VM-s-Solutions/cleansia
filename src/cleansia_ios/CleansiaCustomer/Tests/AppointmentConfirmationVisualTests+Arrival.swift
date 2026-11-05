import CleansiaCore
import CleansiaCustomerApi
import ImageIO
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The real hand-over, through the sheet's own submit: the order is created under the busy overlay, the
/// confirmation replaces the form, and its details answer after `detailsAfter`. Every frame is the actual view
/// hierarchy. Time 0 is the reveal: the first frame the busy card has started to leave.
extension AppointmentConfirmationVisualTests {
    static let stills: [TimeInterval] = [0, 0.12, 0.24, 0.42, 0.8, 2.6]

    /// Recorded until the figure rests and read by what finishes before what, so no runner's pace moves a check.
    func testCashArrivalLiftsTheScrimWhileTheMascotTravels() async throws {
        _ = try await waitForForegroundScene()
        let run = try await recordArrival(.cash, detailsAfter: 0.3, length: 10, name: "cash") {
            $0.restsOnTheThumbsUp(for: 0.3)
        }
        let reveal = try XCTUnwrap(run.reveal, "the busy card never left: \(run)")
        let cardGone = try XCTUnwrap(run.cardGone, "the card never left: \(run)")
        let landed = try XCTUnwrap(run.samples.first(where: \.welcoming), "the mascot never landed: \(run)")
        XCTAssertTrue(run.dimmed(in: reveal - 0.2 ... reveal, atLeast: 0.4), "the scrim holds at first: \(run)")
        XCTAssertGreaterThan(run.dimming(cardGone), 0.03, "and outlasts the card as the mascot travels: \(run)")
        XCTAssertLessThan(cardGone.time, landed.time, "the card has left before the mascot lands: \(run)")
        XCTAssertTrue(run.dimmed(in: reveal + 0.47 ... reveal + 0.8, atMost: 0.03), "gone once it has: \(run)")
        XCTAssertGreaterThanOrEqual(run.welcomeBracket ?? 0, 2.0, "the welcome plays its 2.1 s after landing: \(run)")
        XCTAssertTrue(run.restsOnTheThumbsUp(for: 0.3), "then rests on the thumbs-up: \(run)")
    }

    /// Stripe's sheet has the screen for 600 ms; the busy card is back, whole, in the frame the confirmation
    /// replaces the form, and holds 1.2 s for details that answer only after 2 s.
    func testCardArrivalComesUpWholeAndLiftsTheScrimOnTheSameBeat() async throws {
        _ = try await waitForForegroundScene()
        let run = try await recordArrival(.card, detailsAfter: 2.0, length: 5.4, name: "card")
        let swap = try XCTUnwrap(run.swap, "the confirmation never replaced the form: \(run)")
        let reveal = try XCTUnwrap(run.reveal, "the busy card never left: \(run)")
        XCTAssertTrue(
            run.dimmed(in: swap ... reveal, atLeast: 0.4),
            "no confirmation frame outside the overlay: \(run)"
        )
        XCTAssertEqual(reveal - swap, 1.2, accuracy: 0.15, "the overlay waits 1.2 s, no longer: \(run)")
        XCTAssertTrue(run.lifting(in: reveal + 0.15 ... reveal + 0.38), "the scrim lifts as the mascot lands: \(run)")
        XCTAssertTrue(run.dimmed(in: reveal + 0.47 ... reveal + 0.8, atMost: 0.03), "and is gone after: \(run)")
        XCTAssertLessThan(reveal, try XCTUnwrap(run.released), "the amounts land after the reveal: \(run)")
        assertNothingMoved(run, marks: ["title", "answer", "money"])
    }

    /// Booked under a day ahead, in Czech, with the details landing after the reveal: the rule that always holds
    /// takes two lines there, and the loading frame already holds them.
    func testLateDetailsOfALateBookingMoveNothingOnTheTicket() async throws {
        _ = try await waitForForegroundScene()
        let run = try await recordArrival(.late, language: "cs", detailsAfter: 2.0, length: 3.4, name: "cs-late")
        let reveal = try XCTUnwrap(run.reveal, "the busy card never left: \(run)")
        XCTAssertLessThan(reveal, try XCTUnwrap(run.released), "the details land after the reveal: \(run)")
        assertNothingMoved(run, marks: ["title", "answer", "money"])
    }

    /// The spare height is decided under the overlay; a fetch that fails after the reveal changes the money row's
    /// own lines and moves nothing above them.
    func testAFailureAfterTheRevealMovesNothingAboveTheMoney() async throws {
        _ = try await waitForForegroundScene()
        let run = try await recordArrival(
            .cash, detailsAfter: 2.0, detail: .failure(ApiError(httpStatus: 500)), length: 3.4,
            name: "cash-late-failure"
        )
        let reveal = try XCTUnwrap(run.reveal, "the busy card never left: \(run)")
        XCTAssertLessThan(reveal, try XCTUnwrap(run.released), "the failure lands after the reveal: \(run)")
        XCTAssertTrue(
            run.after.keys.contains("money") && run.before["money"] != nil,
            "the money row is read before and after: \(run)"
        )
        assertNothingMoved(run, marks: ["answer", "money"])
    }

    /// With no flight to carry him, the figure makes its own entrance: it grows in from 92 % over 240 ms.
    func testAFigureNothingCarriesGrowsIn() async throws {
        _ = try await waitForForegroundScene()
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let order = Sample.card.order()
        let make = { (revealed: Bool) in
            AnyView(BookingSuccessView(
                Sample.card.booking(), loadOrder: { _ in order }, revealed: revealed, onViewOrder: {}, onDone: {}
            )
            .environment(\.scenePhase, PresentedBookingConfirmation.phase(of: scene)))
        }
        let canvas = try HostedCanvas(size: CGSize(width: 393, height: 787)) { make(false) }
        defer { canvas.close() }
        await canvas.settle()
        try await Task.sleep(nanoseconds: 400_000_000)
        XCTAssertTrue(animatedMascots(canvas).isEmpty, "no figure before the reveal")
        try XCTUnwrap(canvas.host as? UIHostingController<AnyView>).rootView = make(true)
        var widths: [CGFloat] = []
        let start = ProcessInfo.processInfo.systemUptime
        while ProcessInfo.processInfo.systemUptime - start < 0.6 {
            widths += animatedMascots(canvas).map { presentedWidth($0, in: canvas) }
            try await Task.sleep(nanoseconds: 8_000_000)
        }
        let whole = try XCTUnwrap(widths.last, "the figure never came")
        XCTAssertTrue(widths.contains { $0 < whole * 0.97 }, "it grows in rather than appearing whole: \(widths)")
        XCTAssertGreaterThanOrEqual(widths.min() ?? 0, whole * 0.91, "from 92 %: \(widths)")
    }

    /// Nothing travels and nothing plays: the card and the scrim fade out together in 150 ms over the
    /// thumbs-up, which is there from the first frame. Recorded past the moment a welcome would have started.
    func testReduceMotionCrossFadesCardAndScrimTogether() async throws {
        _ = try await waitForForegroundScene()
        let run = try await recordArrival(
            .cash,
            detailsAfter: 0.3,
            reduceMotion: true,
            length: 6,
            name: "reduced-motion"
        ) { $0.cardHasBeenGone(for: 0.8) }
        let reveal = try XCTUnwrap(run.reveal, "the busy card never left: \(run)")
        let cardGone = try XCTUnwrap(run.cardGone, "the card never left: \(run)")
        XCTAssertLessThanOrEqual(run.dimming(cardGone), 0.03, "the scrim fades with the card, gone as it is: \(run)")
        XCTAssertTrue(run.dimmed(in: reveal + 0.2 ... reveal + 0.6, atMost: 0.03), "both gone by 150 ms: \(run)")
        XCTAssertFalse(run.samples.contains(where: \.welcoming), "motion off never plays the welcome: \(run)")
    }

    /// A customer who typed notes slides to confirm with the keyboard up, and it only goes down after the
    /// confirmation has replaced the form. The spare height is spread over a viewport that ignores the keyboard.
    func testAKeyboardUpAtTheHandOverStillGivesTheMascotItsHeight() async throws {
        if #available(iOS 26, *) {
            throw XCTSkip("iOS 26 ignores synthetic keyboard notifications: SwiftUI's keyboard avoidance never moves")
        }
        _ = try await waitForForegroundScene()
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-1", confirmationCode: "130AA4")))
        create.beforeCreate = { try? await Task.sleep(nanoseconds: 1_200_000_000) }
        let vm = try await submittingVM(.cash, create: create)
        let client = FakeOrderClient()
        client.detailResults = [.success(Sample.cash.order())]
        let canvas = try sheetCanvas(scheme: .light, client: client, submitting: vm)
        defer { canvas.close() }
        await canvas.settle(until: { canvas.host.presentedViewController?.view.window != nil })
        let sheet = try XCTUnwrap(canvas.host.presentedViewController)
        try await Task.sleep(nanoseconds: 400_000_000)
        let open = lowestAccessibleEdge(sheet.view)
        postKeyboard(showing: true, in: canvas)
        try await Task.sleep(nanoseconds: 200_000_000)
        XCTAssertLessThan(lowestAccessibleEdge(sheet.view), open - 200, "the keyboard pushes the form's footer up")
        await canvas.settle(timeout: 3, until: { vm.state.selectedServiceIds.isEmpty })
        try await Task.sleep(nanoseconds: 100_000_000)
        postKeyboard(showing: false, in: canvas)
        let full = BookingSuccessView.mascotCanvas(figureHeight: 116).width
        let standing = { self.animatedMascots(canvas).contains { abs(canvas.frame(of: $0).width - full) < 1 } }
        await canvas.settle(timeout: 3, until: standing)
        let widths = animatedMascots(canvas).map { canvas.frame(of: $0).width }
        XCTAssertTrue(standing(), "the 116 pt mascot, as with no keyboard: welcoming canvas widths \(widths)")
        attach(canvas, name: "iphone-393x852-en-light-keyboard-up-at-the-hand-over", scale: 2)
    }

    /// The cleaner on the busy card stands as tall as the welcoming figure the flight lands on, from the card's
    /// entrance to the flight, on the small phone's compact confirmation, whose cash ticket leaves its hero slack,
    /// and on the phone's regular one, so the hand-over is a move. Both arts draw on a 360 px canvas: the cleaning
    /// figure is 272 px of it, the welcoming one 249.
    func testTheBusyCardsCleanerStandsAsTallAsTheWelcomingFigure() async throws {
        _ = try await waitForForegroundScene()
        for (size, sample, name) in [
            (Self.smallPhone, Sample.cash, "iphone-se-375x667-cash"),
            (Self.smallPhone, Sample.card, "iphone-se-375x667-card"),
            (Self.phone, Sample.cash, "iphone-393x852-cash")
        ] {
            let run = try await handOverMascots(size: size, sample: sample)
            let hero = try XCTUnwrap(run.hero, "\(name): no welcoming figure")
            let heroFigure = hero.height * 249 / 360
            let entered = try XCTUnwrap(run.busy.first, "\(name): no busy card").time + 0.45
            let held = run.busy.filter { $0.time >= entered }.map { ($0.time, $0.frame.height * 272 / 360) }
            let figures = held.map(\.1)
            print("CLEANSIA_HANDOFF \(name) hero \(heroFigure) held \(figures.min() ?? 0)...\(figures.max() ?? 0)")
            XCTAssertFalse(held.isEmpty, "\(name): the card held nothing past its entrance")
            XCTAssertEqual(
                (figures.min() ?? 0) / heroFigure, 1, accuracy: 0.03,
                "\(name): the busy cleaner is \(held) pt, the welcoming figure \(heroFigure) pt"
            )
            XCTAssertEqual((figures.max() ?? 0) / heroFigure, 1, accuracy: 0.03, "\(name): \(held)")
        }
    }

    /// Plus activation's busy card has no figure to hand over to, and keeps its 140 pt mascot.
    func testABusyCardWithNoFigureToMatchKeepsItsMascotBox() async throws {
        _ = try await waitForForegroundScene()
        let canvas = try HostedCanvas(size: Self.phone) {
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                BusyMascotOverlay(visible: true, message: L10n.Membership.busySubscribePlus)
            }
        }
        defer { canvas.close() }
        await canvas.settle(until: { !self.animatedMascots(canvas).isEmpty })
        try await Task.sleep(nanoseconds: 600_000_000)
        let box = try XCTUnwrap(animatedMascots(canvas).first.map { canvas.frame(of: $0) }, "no mascot on the card")
        XCTAssertEqual(box.width, 140, accuracy: 0.5, "\(box)")
        XCTAssertEqual(box.height, 140, accuracy: 0.5, "\(box)")
        attach(canvas, name: "iphone-393x852-en-light-plus-busy-card", scale: 2)
    }
}

private extension AppointmentConfirmationVisualTests {
    /// A booking ready to place by cash or by card, with its quote already landed as on the review step.
    func submittingVM(_ sample: Sample, create: FakeOrderCreateClient) async throws -> BookingViewModel {
        let card = sample != .cash
        let category = CatalogCategory(
            id: "c", slug: "home", name: "Home", description: nil, displayOrder: 0, translations: [:]
        )
        let catalog = Catalog(
            services: [CatalogService(
                id: "s-1", name: "General Cleaning", description: nil, basePrice: 500, perRoomPrice: 150,
                category: category, translations: [:]
            )],
            packages: [], currencyCode: "CZK", defaultCurrencyCode: "CZK"
        )
        let oven = CatalogExtra(
            id: "e-1", slug: "inside-oven", name: "Inside oven cleaning", description: nil, price: 200,
            displayOrder: 1, translations: [:]
        )
        let vm = BookingViewModel(
            catalogClient: FakeCatalogClient(result: .success(catalog)),
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: card ? 1150 : 1000, currencyId: "cur-czk", currencyCode: "CZK",
                requiredEmployees: card ? 2 : 1,
                estimatedMinutes: card ? 180 : 120, creditBalance: card ? 250 : 0, creditMaxShareOfOrder: 0.7
            ))),
            extraClient: FakeExtraClient(result: .success([oven])),
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            paymentIntentClient: FakePaymentIntentClient(),
            countryResolver: FakeCountryResolver(),
            consentClient: FakeConsentStatusClient(),
            tokenStore: FakeTokenStore.signedIn(),
            isCardPaymentAvailable: card,
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler(),
            savedCardClient: FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        )
        await vm.loadCatalog()
        await vm.loadExtras()
        let booking = sample.booking()
        vm.update { _ in
            var draft = BookingState()
            draft.selectedServiceIds = ["s-1"]
            draft.selectedExtraSlugs = ["inside-oven"]
            draft.rooms = card ? 2 : 1
            draft.bathrooms = 1
            draft.street = booking.street
            draft.city = booking.city
            draft.zipCode = "18000"
            draft.countryIsoCode = "cz"
            draft.selectedInstant = booking.cleaningAt
            draft.paymentMethod = card ? .card : .cash
            return draft
        }
        await vm.refreshQuoteForTest()
        return vm
    }

    func recordArrival(
        _ sample: Sample, language: String = "en", detailsAfter: TimeInterval,
        detail: ApiResult<CustomerOrderDetail>? = nil, reduceMotion: Bool = false, length: TimeInterval, name: String,
        until done: (ArrivalTimeline) -> Bool = { _ in false }
    ) async throws -> ArrivalTimeline {
        try useLanguage(language)
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-1", confirmationCode: "130AA4")))
        create.beforeCreate = { try? await Task.sleep(nanoseconds: 350_000_000) }
        let payment = FakePaymentSheetPresenter()
        payment.beforeReturning = { try? await Task.sleep(nanoseconds: 600_000_000) }
        let vm = try await submittingVM(sample, create: create)
        let details = detail ?? .success(sample.order())
        let client = FakeOrderClient()
        client.holdDetails = true
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let canvas = try HostedCanvas(size: CGSize(width: 393, height: 787)) {
            BookingSheetView(
                vm: vm, geocoding: CLGeocoderGeocodingService(), mapProvider: PreviewMapProvider(),
                paymentSheet: payment, orderClient: client, onDismiss: {}, submitsOnAppear: true
            )
            .environment(\.locale, Locale(identifier: language))
            .environment(\.scenePhase, PresentedBookingConfirmation.phase(of: scene))
            .environment(\._accessibilityReduceMotion, reduceMotion)
            .preferredColorScheme(.light)
        }
        defer { canvas.close() }
        let started = ProcessInfo.processInfo.systemUptime
        var frames: [CGImage] = []
        var timeline = ArrivalTimeline()
        var cardSpot: CGPoint?
        var released = false
        while ProcessInfo.processInfo.systemUptime - started < length, !done(timeline) {
            let now = ProcessInfo.processInfo.systemUptime - started
            if timeline.swap == nil, vm.state.selectedServiceIds.isEmpty { timeline.swap = now }
            if let swap = timeline.swap, !released, now >= swap + detailsAfter {
                timeline.before = ticketMarks(in: canvas.window)
                client.releaseDetail(details)
                timeline.released = now
                released = true
            }
            let mascots = animatedMascots(canvas).map { canvas.frame(of: $0) }
            let busy = mascots.first { isBusyCard($0, in: canvas) }
            if let busy {
                cardSpot = CGPoint(x: busy.minX - 30, y: busy.midY)
            }
            let frame = try XCTUnwrap(snapshot(canvas, scale: 1).cgImage)
            frames.append(frame)
            try timeline.samples.append(ArrivalTimeline.Sample(
                time: now,
                busy: busy != nil,
                welcoming: mascots.contains { !isBusyCard($0, in: canvas) },
                ground: rgba(frame, in: CGRect(x: 6, y: 420, width: 1, height: 1)),
                card: cardSpot.map { try rgba(frame, in: CGRect(origin: $0, size: CGSize(width: 1, height: 1))) }
            ))
            try await Task.sleep(nanoseconds: 15_000_000)
        }
        timeline.after = ticketMarks(in: canvas.window)
        try attachArrival(frames, timeline: timeline, name: name, reduceMotion: reduceMotion)
        return timeline
    }

    /// A booking placed through the presented sheet, slid a second after its review step comes up: every frame of
    /// the busy card's mascot, which is the one in the middle, and the welcoming figure it hands over to, once that
    /// has stood a while.
    func handOverMascots(
        size: CGSize,
        sample: Sample
    ) async throws -> (busy: [(time: TimeInterval, frame: CGRect)], hero: CGRect?) {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-1", confirmationCode: "130AA4")))
        create.beforeCreate = { try? await Task.sleep(nanoseconds: 350_000_000) }
        let vm = try await submittingVM(sample, create: create)
        while vm.advance() {}
        let client = FakeOrderClient()
        client.detailResults = [.success(sample.order())]
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let canvas = try HostedCanvas(size: size) {
            ReviewThenSlide(scene: scene) { slid in
                BookingSheetView(
                    vm: vm, geocoding: CLGeocoderGeocodingService(), mapProvider: PreviewMapProvider(),
                    paymentSheet: FakePaymentSheetPresenter(), orderClient: client, onDismiss: {}, submitsOnAppear: slid
                )
            }
        }
        defer { canvas.close() }
        var busy: [(time: TimeInterval, frame: CGRect)] = []
        var hero: CGRect?
        let started = ProcessInfo.processInfo.systemUptime
        while ProcessInfo.processInfo.systemUptime - started < 5 {
            let now = ProcessInfo.processInfo.systemUptime - started
            for frame in animatedMascots(canvas).map({ canvas.frame(of: $0) }) {
                if isBusyCard(frame, in: canvas) { busy.append((now, frame)) } else { hero = frame }
            }
            try await Task.sleep(nanoseconds: 15_000_000)
        }
        return (busy, hero)
    }

    /// The busy card's mascot is the one in the middle; the welcoming figure stands to the right.
    func isBusyCard(_ frame: CGRect, in canvas: HostedCanvas) -> Bool {
        abs(frame.midX - canvas.window.bounds.midX) < 1
    }

    func attachArrival(_ frames: [CGImage], timeline: ArrivalTimeline, name: String, reduceMotion: Bool) throws {
        let label = "iphone-arrival-\(name)"
        try attachGIF(frames, timestamps: timeline.samples.map(\.time), name: label)
        if let reveal = timeline.reveal {
            let marks = reduceMotion ? [0, 0.075, 0.15] : Self.stills
            for mark in marks {
                guard let index = timeline.samples.indices.min(by: {
                    abs(timeline.samples[$0].time - reveal - mark) < abs(timeline.samples[$1].time - reveal - mark)
                }) else { continue }
                let offset = Int(((timeline.samples[index].time - reveal) * 1000).rounded())
                let still = XCTAttachment(image: UIImage(cgImage: frames[index]))
                still.name = "\(label)-at\(String(format: "%04d", Int(mark * 1000)))ms (actual \(offset) ms)"
                still.lifetime = .keepAlways
                add(still)
            }
        }
        let record = XCTAttachment(string: timeline.description)
        record.name = "\(label)-timeline"
        record.lifetime = .keepAlways
        add(record)
    }

    /// The title, the answer block and the money row's first line, read from the view tree.
    func ticketMarks(in window: UIWindow) -> [String: CGRect] {
        let frames = accessibilityFrames(window)
        let money = [L10n.Booking.successTotalLabel, L10n.Booking.successDetailsLoading]
        return [
            "title": frames[L10n.Booking.successTitle],
            "answer": frames.first { $0.key.contains("Zenklova") || $0.key.contains("Vinohradská") }?.value,
            "money": frames.first { label, _ in money.contains { label.hasPrefix($0) } }?.value
        ].compactMapValues { $0 }
    }

    /// The top edge of each mark, and the answer block's whole frame, stay where they were.
    func assertNothingMoved(
        _ run: ArrivalTimeline,
        marks: [String],
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        for mark in marks {
            guard let before = run.before[mark], let after = run.after[mark] else {
                XCTFail("\(mark) is not on the ticket before and after: \(run)", file: file, line: line)
                continue
            }
            XCTAssertEqual(after.minY, before.minY, accuracy: 0.5, "\(mark) moved: \(run)", file: file, line: line)
            if mark == "answer" {
                XCTAssertEqual(
                    after.height,
                    before.height,
                    accuracy: 0.5,
                    "answer resized: \(run)",
                    file: file,
                    line: line
                )
            }
        }
    }

    /// The on-screen width, from the presentation tree, so an animation in flight is read as drawn.
    func presentedWidth(_ view: UIView, in canvas: HostedCanvas) -> CGFloat {
        let layer = view.layer.presentation() ?? view.layer
        let window = canvas.window.layer.presentation() ?? canvas.window.layer
        return layer.convert(layer.bounds, to: window).width
    }

    func lowestAccessibleEdge(_ node: NSObject) -> CGFloat {
        if node.isAccessibilityElement { return node.accessibilityFrame.maxY }
        let children = node.accessibilityElements as? [NSObject] ?? (node as? UIView)?.subviews ?? []
        return children.map { lowestAccessibleEdge($0) }.max() ?? 0
    }

    /// The keyboard's own notifications, as UIKit posts them for a 336 pt keyboard: the test host's simulator
    /// may have a hardware keyboard attached, which never raises the software one.
    func postKeyboard(showing: Bool, in canvas: HostedCanvas) {
        let screen = canvas.window.screen.bounds
        let raised = CGRect(x: 0, y: screen.maxY - 336, width: screen.width, height: 336)
        let lowered = raised.offsetBy(dx: 0, dy: 336)
        let info: [AnyHashable: Any] = [
            UIResponder.keyboardFrameBeginUserInfoKey: NSValue(cgRect: showing ? lowered : raised),
            UIResponder.keyboardFrameEndUserInfoKey: NSValue(cgRect: showing ? raised : lowered),
            UIResponder.keyboardAnimationDurationUserInfoKey: 0.0,
            UIResponder.keyboardAnimationCurveUserInfoKey: UIView.AnimationCurve.easeInOut.rawValue,
            UIResponder.keyboardIsLocalUserInfoKey: true
        ]
        let edge = showing ? UIResponder.keyboardWillShowNotification : UIResponder.keyboardWillHideNotification
        for name in [edge, UIResponder.keyboardWillChangeFrameNotification] {
            NotificationCenter.default.post(name: name, object: canvas.window.screen, userInfo: info)
        }
    }

    func attachGIF(_ frames: [CGImage], timestamps: [TimeInterval], name: String) throws {
        let data = NSMutableData()
        let destination = try XCTUnwrap(CGImageDestinationCreateWithData(
            data, "com.compuserve.gif" as CFString, frames.count, nil
        ))
        CGImageDestinationSetProperties(destination, [
            kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0]
        ] as CFDictionary)
        for (index, frame) in frames.enumerated() {
            let delay = index + 1 < timestamps.count ? timestamps[index + 1] - timestamps[index] : 0.05
            CGImageDestinationAddImage(destination, frame, [kCGImagePropertyGIFDictionary: [
                kCGImagePropertyGIFDelayTime: delay, kCGImagePropertyGIFUnclampedDelayTime: delay
            ]] as CFDictionary)
        }
        XCTAssertTrue(CGImageDestinationFinalize(destination))
        let attachment = XCTAttachment(data: data as Data, uniformTypeIdentifier: "com.compuserve.gif")
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }
}

/// The sheet on its review step as the customer reads it, slid to confirm a second later.
private struct ReviewThenSlide: View {
    let scene: UIWindowScene
    let sheet: (Bool) -> BookingSheetView
    @State private var presented = false
    @State private var slid = false

    var body: some View {
        CleansiaColors.background
            .onAppear { presented = true }
            .sheet(isPresented: $presented) {
                sheet(slid)
                    .environment(\.locale, Locale(identifier: "en"))
                    .environment(\.scenePhase, PresentedBookingConfirmation.phase(of: scene))
                    .preferredColorScheme(.light)
                    .task {
                        try? await Task.sleep(nanoseconds: 1_000_000_000)
                        slid = true
                    }
            }
    }
}

struct ArrivalTimeline: CustomStringConvertible {
    struct Sample {
        let time: TimeInterval
        let busy: Bool
        let welcoming: Bool
        let ground: [UInt8]
        let card: [UInt8]?
    }

    var samples: [Sample] = []
    var swap: TimeInterval?
    var released: TimeInterval?
    var before: [String: CGRect] = [:]
    var after: [String: CGRect] = [:]

    private static func luminance(_ pixel: [UInt8]) -> Double {
        0.2126 * Double(pixel[0]) + 0.7152 * Double(pixel[1]) + 0.0722 * Double(pixel[2])
    }

    /// How much of the sheet's ground the scrim takes away, against the last frame: 0.45 under the whole scrim.
    func dimming(_ sample: Sample) -> Double {
        guard let clear = samples.last.map({ Self.luminance($0.ground) }), clear > 0 else { return 0 }
        return max(0, 1 - Self.luminance(sample.ground) / clear)
    }

    /// The first frame the busy card is no longer whole: its plain surface beside the mascot starts to show
    /// what is under it. Read from the card that holds over the confirmation, once its entrance has settled.
    var reveal: TimeInterval? {
        let settled = (swap ?? 0) + 0.2
        guard let start = samples.firstIndex(where: { $0.time >= settled && $0.busy && $0.card != nil }),
              let whole = samples[start].card
        else { return nil }
        return samples[start...].first { sample in
            guard let card = sample.card else { return false }
            return (0 ..< 3).contains { abs(Int(card[$0]) - Int(whole[$0])) > 6 }
        }?.time
    }

    func inside(_ window: ClosedRange<TimeInterval>) -> [Sample] {
        samples.filter { window.contains($0.time) }
    }

    func dimmed(in window: ClosedRange<TimeInterval>, atLeast floor: Double) -> Bool {
        let frames = inside(window)
        return !frames.isEmpty && frames.allSatisfy { dimming($0) >= floor }
    }

    func dimmed(in window: ClosedRange<TimeInterval>, atMost ceiling: Double) -> Bool {
        let frames = inside(window)
        return !frames.isEmpty && frames.allSatisfy { dimming($0) <= ceiling }
    }

    /// Some frame in `window` is part way between the whole scrim and none.
    func lifting(in window: ClosedRange<TimeInterval>) -> Bool {
        inside(window).contains { (0.05 ... 0.38).contains(dimming($0)) }
    }

    /// The first frame, from the reveal on, the busy card's exit has finished in.
    var cardGone: Sample? {
        reveal.flatMap { reveal in samples.first { $0.time >= reveal && !$0.busy } }
    }

    func cardHasBeenGone(for tail: TimeInterval) -> Bool {
        guard let gone = cardGone, let last = samples.last else { return false }
        return last.time - gone.time >= tail
    }

    /// The welcome has played in one run, and the figure has stood still on the thumbs-up for `rest` since.
    func restsOnTheThumbsUp(for rest: TimeInterval) -> Bool {
        guard let first = samples.firstIndex(where: \.welcoming),
              let last = samples.lastIndex(where: \.welcoming),
              samples[first ... last].allSatisfy(\.welcoming),
              last + 1 < samples.count
        else { return false }
        let resting = samples[(last + 1)...]
        return resting.allSatisfy { !$0.busy } && samples[samples.count - 1].time - samples[last + 1].time >= rest
    }

    /// From the frame before the welcome to the frame after it: never shorter than the welcome, however sparse.
    var welcomeBracket: TimeInterval? {
        guard let first = samples.firstIndex(where: \.welcoming), first > 0,
              let last = samples.lastIndex(where: \.welcoming), last + 1 < samples.count
        else { return nil }
        return samples[last + 1].time - samples[first - 1].time
    }

    var description: String {
        let origin = reveal ?? 0
        let rows = samples.map {
            String(format: "%.3f (reveal %+.0f ms)", $0.time, ($0.time - origin) * 1000)
                + " busy=\($0.busy ? 1 : 0) welcoming=\($0.welcoming ? 1 : 0)"
                + String(format: " scrim=%.3f", dimming($0))
        }
        let marks = [
            "swap \(swap.map { String(format: "%.3f", $0) } ?? "none")",
            "details released \(released.map { String(format: "%.3f", $0) } ?? "never")",
            "reveal \(reveal.map { String(format: "%.3f", $0) } ?? "none")",
            "ticket before \(before.mapValues(\.integral)) after \(after.mapValues(\.integral))"
        ]
        return (marks + rows).joined(separator: "\n")
    }
}
