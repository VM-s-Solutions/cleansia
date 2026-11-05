import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

private final class SuccessLoadRecorder: @unchecked Sendable {
    var fetchedIds: [String] = []
    var warmCount = 0
    var answers: [CustomerOrderDetail?] = []
}

@MainActor
final class BookingSuccessViewModelTests: XCTestCase {
    private func makeVM(
        orderId: String = "o-1",
        order: CustomerOrderDetail? = nil,
        recorder: SuccessLoadRecorder = SuccessLoadRecorder()
    ) -> BookingSuccessViewModel {
        BookingSuccessViewModel(
            orderId: orderId,
            fetch: { id in
                recorder.fetchedIds.append(id)
                return recorder.answers.isEmpty ? order : recorder.answers.removeFirst()
            },
            warmOrders: { recorder.warmCount += 1 }
        )
    }

    private func states(of vm: BookingSuccessViewModel) -> (AnyCancellable, () -> [String]) {
        var seen: [String] = []
        let subscription = vm.$state.sink { state in
            switch state {
            case .loading: seen.append("loading")
            case .loaded: seen.append("loaded")
            case .error: seen.append("error")
            }
        }
        return (subscription, { seen })
    }

    func testAppointmentOvernightEstimateNamesTheEndDate() throws {
        let start = try XCTUnwrap(Calendar.current.date(from: DateComponents(
            year: 2026, month: 10, day: 31, hour: 23
        )))
        let window = BookingSuccessView.timeWindow(start, estimatedMinutes: 180, locale: Locale(identifier: "en_GB"))

        XCTAssertTrue(window.contains("1 Nov"), "the estimated finish must identify the following day: \(window)")
    }

    /// The welcoming frames share one 360 px canvas whose union box is x 88-255, y 51-300: a figure
    /// F tall draws that canvas 360 F / 249 wide, shifted so the box starts where the figure does.
    func testTheFigureCanvasFollowsTheWelcomingArtsUnionBox() {
        let figure = BookingSuccessView.mascotCanvas(figureHeight: 116)
        XCTAssertEqual(figure.width, 167.71, accuracy: 0.01)
        XCTAssertEqual(figure.height, figure.width)
        XCTAssertEqual(figure.minX, -41.00, accuracy: 0.01)
        XCTAssertEqual(figure.minY, -23.76, accuracy: 0.01)
    }

    func testStartsLoadingSoTheTicketShowsItsBars() {
        let vm = makeVM()

        guard case .loading = vm.state else { return XCTFail("expected .loading") }
        XCTAssertNil(vm.order)
    }

    func testLoadSuccessPopulatesTheOrder() async {
        let order = OrderFixtures.detail(id: "o-1", total: 1200)
        let vm = makeVM(order: order)

        await vm.load()

        guard case let .loaded(loaded) = vm.state else { return XCTFail("expected .loaded") }
        XCTAssertEqual(loaded, order)
        XCTAssertEqual(vm.order, order)
    }

    func testFetchFailureDegradesToErrorNotABlankScreen() async {
        let vm = makeVM(order: nil)

        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected .error") }
        XCTAssertNil(vm.order)
    }

    func testBlankOrderIdSkipsTheFetchAndDegrades() async {
        let recorder = SuccessLoadRecorder()
        let vm = makeVM(orderId: " ", recorder: recorder)

        await vm.load()

        XCTAssertTrue(recorder.fetchedIds.isEmpty)
        guard case .error = vm.state else { return XCTFail("expected .error") }
    }

    func testLoadIsSingleFlight() async {
        let recorder = SuccessLoadRecorder()
        let vm = makeVM(order: OrderFixtures.detail(id: "o-1"), recorder: recorder)

        await vm.load()
        await vm.load()

        XCTAssertEqual(recorder.fetchedIds, ["o-1"])
        XCTAssertEqual(recorder.warmCount, 1)
    }

    func testLoadWarmsTheOrdersCacheEvenWhenTheFetchIsSkipped() async {
        let recorder = SuccessLoadRecorder()
        let vm = makeVM(orderId: "", recorder: recorder)

        await vm.load()

        XCTAssertEqual(recorder.warmCount, 1)
    }

    func testTryAgainGoesBackToTheBarsAndLandsTheOrder() async {
        let recorder = SuccessLoadRecorder()
        let order = OrderFixtures.detail(id: "o-1", total: 1150)
        recorder.answers = [nil, order]
        let vm = makeVM(recorder: recorder)
        let (subscription, seen) = states(of: vm)
        defer { subscription.cancel() }

        await vm.load()
        await vm.retry()

        XCTAssertEqual(seen(), ["loading", "error", "loading", "loaded"])
        XCTAssertEqual(vm.order, order)
        XCTAssertEqual(recorder.fetchedIds, ["o-1", "o-1"], "the same GetById, read again")
        XCTAssertEqual(recorder.warmCount, 1, "a retry reads the order only")
    }

    func testTryAgainThatFailsAgainIsUnavailableAgain() async {
        let recorder = SuccessLoadRecorder()
        recorder.answers = [nil, nil]
        let vm = makeVM(recorder: recorder)
        let (subscription, seen) = states(of: vm)
        defer { subscription.cancel() }

        await vm.load()
        await vm.retry()

        XCTAssertEqual(seen(), ["loading", "error", "loading", "error"])
        XCTAssertEqual(recorder.fetchedIds, ["o-1", "o-1"])
    }

    func testTheOrdersFreeCancellationHoursAreMappedAndAMissingValueStaysMissing() throws {
        var plus = OrderItem.wireComplete()
        plus.freeCancellationHours = 4

        XCTAssertEqual(try CustomerOrderDetail(plus).freeCancellationHours, 4)
        XCTAssertNil(try CustomerOrderDetail(.wireComplete()).freeCancellationHours)
    }
}

// MARK: - The hand-over captures the draft

extension BookingSuccessViewModelTests {
    private static func catalog() -> Catalog {
        let category = CatalogCategory(
            id: "c", slug: "home", name: "Home", description: nil, displayOrder: 0, translations: [:]
        )
        func service(_ id: String, _ name: String, _ czech: String) -> CatalogService {
            CatalogService(
                id: id, name: name, description: nil, basePrice: 500, perRoomPrice: 150, category: category,
                translations: ["cs": CatalogTranslation(name: czech, description: nil)]
            )
        }
        return Catalog(
            services: [service("s-1", "General Cleaning", "Obecný úklid"), service("s-2", "Windows", "Okna")],
            packages: [CatalogPackage(
                id: "p-1", name: "Spring Package", description: nil, price: 1999,
                translations: ["cs": CatalogTranslation(name: "Jarní balíček", description: nil)], includedServices: []
            )],
            currencyCode: "CZK",
            defaultCurrencyCode: "CZK"
        )
    }

    private static func extras() -> [CatalogExtra] {
        [
            CatalogExtra(
                id: "e-1", slug: "inside-oven", name: "Inside oven cleaning", description: nil, price: 200,
                displayOrder: 1, translations: ["cs": CatalogTranslation(
                    name: "Čištění vnitřku trouby",
                    description: nil
                )]
            ),
            CatalogExtra(
                id: "e-2", slug: "fridge", name: "Inside fridge", description: nil, price: 150, displayOrder: 2,
                translations: [:]
            )
        ]
    }

    private static func quote(credit: Double = 300) throws -> BookingQuote {
        try BookingQuote(from: QuoteOrderResponse(
            totalPrice: 1660, originalSubtotal: 1660, currencyId: "cur-czk", currencyCode: "CZK",
            servicesSubtotal: 1660, packagesSubtotal: 0, extrasSubtotal: 0, expressSurchargeApplied: false,
            expressSurchargeAmount: 0, estimatedDurationMinutes: 180, requiredEmployees: 2,
            expressSurchargeWaivedByMembership: false, creditBalance: credit, creditMaxShareOfOrder: 0.5,
            dirtinessSurchargeAmount: 0, dirtinessLevel: ._1
        ))
    }

    func testTheHandOverCapturesEveryFieldTheTicketShows() throws {
        let instant = Date(timeIntervalSince1970: 1_793_440_000)
        var state = BookingState()
        state.selectedServiceIds = ["s-1"]
        state.selectedPackageIds = ["p-1"]
        state.selectedExtraSlugs = ["inside-oven"]
        state.rooms = 2
        state.bathrooms = 1
        state.street = "Zenklova 6"
        state.city = "Praha 8"
        state.selectedInstant = instant
        state.paymentMethod = .card

        let card = try BookingSheetView.submittedDraft(
            state, quote: Self.quote(), discount: 0, catalog: Self.catalog(), extras: Self.extras(),
            locale: Locale(identifier: "cs")
        )

        XCTAssertEqual(card.cleaningAt, instant)
        XCTAssertEqual(card.estimatedMinutes, 180)
        XCTAssertEqual(card.street, "Zenklova 6")
        XCTAssertEqual(card.city, "Praha 8")
        XCTAssertTrue(card.appliesCredit)
        XCTAssertEqual(card.requiredEmployees, 2)
        XCTAssertEqual(card.rooms, 2)
        XCTAssertEqual(card.bathrooms, 1)
        XCTAssertEqual(card.dirtiness, .increased)
        XCTAssertEqual(card.basketNames, ["Obecný úklid", "Jarní balíček"], "services, then packages, in Czech")
        XCTAssertEqual(card.extraNames, ["Čištění vnitřku trouby"], "the extra by its name, never its slug")
        XCTAssertFalse(card.paysCash)

        state.paymentMethod = .cash
        let cash = try BookingSheetView.submittedDraft(state, quote: Self.quote(), discount: 0)
        XCTAssertTrue(cash.paysCash)
        XCTAssertFalse(cash.appliesCredit, "credit is a card-only tender")
        XCTAssertEqual(cash.basketNames, [], "no catalogue, no guessed names")
    }

    func testTheHomeLineNamesTheDirtinessOnlyWhenItIsNotNormal() {
        var booking = BookingSuccess(orderId: "o-1", confirmationCode: "")
        booking.rooms = 2
        booking.bathrooms = 1
        let home = L10n.OrderDetail.roomsBathrooms(2, 1)

        XCTAssertEqual(BookingSuccessView.homeLine(booking), home)
        booking.dirtiness = .heavy
        XCTAssertEqual(
            BookingSuccessView.homeLine(booking),
            home + " · " + L10n.Booking.dirtinessLevelLabel + ": " + L10n.Booking.dirtinessName(.heavy)
        )
    }

    func testTheHouseNumberAndTheDistrictNeverSplit() {
        XCTAssertEqual(
            BookingSuccessView.addressLine(street: "Zenklova 6", city: "Praha 8"),
            "Zenklova\u{00A0}6, Praha\u{00A0}8"
        )
        XCTAssertEqual(
            BookingSuccessView.addressLine(street: "Vinohradská 1024/46", city: "Praha 2"),
            "Vinohradská\u{00A0}1024/46, Praha\u{00A0}2"
        )
        XCTAssertNil(BookingSuccessView.addressLine(street: " ", city: nil))
    }
}

// MARK: - Spare height

extension BookingSuccessViewModelTests {
    private typealias Tiers = BookingSuccessView.Tiers

    func testTheSizeClassComesFromTheRoomTheSheetLeaves() {
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 512, accessibilityText: false), .compact)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 559.5, accessibilityText: false), .compact)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 560, accessibilityText: false), .regular)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 659, accessibilityText: false), .regular)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 699.5, accessibilityText: false), .regular)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 717, accessibilityText: false), .tall)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 659, accessibilityText: true), .accessibility)
        XCTAssertEqual(BookingSuccessView.sizeClass(room: 512, accessibilityText: true), .accessibility)
    }

    func testTheTiersAreTheSpecsTable() {
        XCTAssertEqual(
            BookingSuccessView.SizeClass.compact.tiers,
            Tiers(pMin: 7, pNat: 10, pMax: 14, pOver: 16, hdMin: 64, hdCap: 96)
        )
        XCTAssertEqual(Tiers.regularTiers, Tiers(pMin: 10, pNat: 13, pMax: 20, pOver: 24, hdMin: 96, hdCap: 116))
        XCTAssertEqual(
            BookingSuccessView.SizeClass.tall.tiers,
            Tiers(pMin: 10, pNat: 14, pMax: 20, pOver: 24, hdMin: 104, hdCap: 136)
        )
        XCTAssertEqual(
            BookingSuccessView.SizeClass.accessibility.tiers,
            Tiers(pMin: 14, pNat: 14, pMax: 14, pOver: 14, hdMin: 96, hdCap: 96)
        )
    }

    /// The iPhone 393 x 852 frame: 659 pt of room, the card-and-credit ticket 594 tall at its minimum.
    func testTheRegularPhoneGivesTheMascotItsCapAndTheRestToTheRows() {
        let fit = BookingSuccessView.distribute(available: 659, natural: 594, tiers: .regularTiers)

        XCTAssertEqual(fit.header, 116)
        XCTAssertEqual(fit.rowPad, 15.625, accuracy: 0.001)
        XCTAssertEqual(fit.rest, 0)
    }

    func testEachTierFillsInOrderAndNothingRestsBeforeTheRowsAreFull() {
        let tiers = Tiers.regularTiers
        let steps: [[CGFloat]] = [
            [-40, 96, 10, 0],
            [0, 96, 10, 0],
            [12, 96, 11.5, 0],
            [24, 96, 13, 0],
            [34, 106, 13, 0],
            [44, 116, 13, 0],
            [72, 116, 16.5, 0],
            [100, 116, 20, 0],
            [116, 116, 22, 0],
            [132, 116, 24, 0],
            [150, 116, 24, 18]
        ]
        for step in steps {
            let fit = BookingSuccessView.distribute(available: 500 + step[0], natural: 500, tiers: tiers)
            XCTAssertEqual(fit.header, step[1], accuracy: 0.001, "header at \(step[0])")
            XCTAssertEqual(fit.rowPad, step[2], accuracy: 0.001, "row padding at \(step[0])")
            XCTAssertEqual(fit.rest, step[3], accuracy: 0.001, "rest at \(step[0])")
        }
    }

    func testACompactPhoneNeverPadsPastItsLastTier() {
        let tiers = BookingSuccessView.SizeClass.compact.tiers
        let fit = BookingSuccessView.distribute(available: 1512, natural: 512, tiers: tiers)

        XCTAssertEqual(fit.header, tiers.hdCap)
        XCTAssertEqual(fit.rowPad, tiers.pOver)
        XCTAssertLessThanOrEqual(fit.rowPad, 16)
        XCTAssertEqual(fit.rest, 1000 - 24 - 32 - 32 - 16)
        let smallPhone = BookingSuccessView.distribute(available: 516, natural: 516, tiers: tiers)
        XCTAssertEqual(smallPhone.header, 64, "the SE fits at the compact minimum without scrolling")
        XCTAssertEqual(smallPhone.rowPad, 7)
    }

    func testLargeTextIgnoresTheTiers() {
        let tiers = BookingSuccessView.SizeClass.accessibility.tiers
        let scrolls = BookingSuccessView.distribute(available: 659, natural: 893, tiers: tiers)
        XCTAssertEqual(scrolls.header, 96)
        XCTAssertEqual(scrolls.rowPad, 14)
        XCTAssertEqual(scrolls.rest, 0)
        let roomy = BookingSuccessView.distribute(available: 2000, natural: 800, tiers: tiers)
        XCTAssertEqual(roomy.header, 96)
        XCTAssertEqual(roomy.rowPad, 14)
    }
}

/// Both arts draw on a 360 px canvas: the cleaning still covers rows 43-314 (272 tall); the welcoming figure stands
/// in the box the hero hangs its canvas from, rows 51-299 (249 tall), 2 px above the art's own alpha, rows 53-301.
extension BookingSuccessViewModelTests {
    private static let phoneHero = BookingSuccessView.mascotCanvas(figureHeight: 116).offsetBy(dx: 285, dy: 76)
    private static let smallPhoneHero = BookingSuccessView.mascotCanvas(figureHeight: 71.7).offsetBy(dx: 302, dy: 82)

    func testTheBusyBoxDrawsTheCleanerAsTallAsTheHerosFigure() {
        XCTAssertEqual(MascotFigureBox.cleaning.canvasSide(figureHeight: 116), 153.53, accuracy: 0.01)
        XCTAssertEqual(MascotFigureBox.cleaning.canvasSide(figureHeight: 64), 84.71, accuracy: 0.01)
        XCTAssertEqual(MascotFigureBox.cleaning.figureHeight(canvasSide: 140), 105.78, accuracy: 0.01)
        XCTAssertEqual(MascotFigureBox.welcoming.figureHeight(canvasSide: Self.phoneHero.height), 116, accuracy: 0.001)
        XCTAssertEqual(
            MascotFigureBox.welcoming.figureHeight(canvasSide: Self.smallPhoneHero.height),
            71.7,
            accuracy: 0.001
        )
    }

    func testTheWelcomingBoxIsTheHerosCanvas() {
        for height: CGFloat in [64, 71.7, 96, 116, 136] {
            let hero = BookingSuccessView.mascotCanvas(figureHeight: height)
            XCTAssertEqual(MascotFigureBox.welcoming.canvasSide(figureHeight: height), hero.width, accuracy: 0.001)
            XCTAssertEqual(MascotFigureBox.welcoming.figureHeight(canvasSide: hero.width), height, accuracy: 0.001)
        }
    }

    /// The canvas drawn in place of the other art stands its figure as tall, its top on the same line and centred
    /// on the same vertical; standing it back gives the canvas it replaced.
    func testAStandInStandsAsTallAsTheFigureItReplaces() {
        for hero in [Self.phoneHero, Self.smallPhoneHero] {
            let cleaner = MascotFigureBox.cleaning.canvas(standingAs: .welcoming, on: hero)
            XCTAssertEqual(cleaner.width, cleaner.height)
            XCTAssertEqual(
                MascotFigureBox.cleaning.figureHeight(canvasSide: cleaner.height),
                MascotFigureBox.welcoming.figureHeight(canvasSide: hero.height),
                accuracy: 0.001
            )
            XCTAssertEqual(
                cleaner.minY + cleaner.height * 43 / 360,
                hero.minY + hero.height * 51 / 360,
                accuracy: 0.001
            )
            XCTAssertEqual(cleaner.midX, hero.midX, accuracy: 0.001)
            let back = MascotFigureBox.welcoming.canvas(standingAs: .cleaning, on: cleaner)
            XCTAssertEqual(back.minX, hero.minX, accuracy: 0.001)
            XCTAssertEqual(back.minY, hero.minY, accuracy: 0.001)
            XCTAssertEqual(back.width, hero.width, accuracy: 0.001)
        }
    }

    /// Each stand-in travels from its own canvas at the busy card to its own canvas at the hero; part way, both
    /// stand equally tall on the same line. From a busy card sized to the hero, that height never changes.
    func testTheFlightKeepsBothStandInsAtOneHeightAllTheWay() {
        let hero = Self.phoneHero
        let heroFigure = MascotFigureBox.welcoming.figureHeight(canvasSide: hero.height)
        let side = MascotFigureBox.cleaning.canvasSide(figureHeight: heroFigure)
        let busy = CGRect(x: 196.5 - side / 2, y: 330, width: side, height: side)
        let cleaner = (from: busy, to: MascotFigureBox.cleaning.canvas(standingAs: .welcoming, on: hero))
        let welcomer = (from: MascotFigureBox.welcoming.canvas(standingAs: .cleaning, on: busy), to: hero)
        for step in 0 ... 10 {
            let progress = CGFloat(step) / 10
            let cleaning = Self.lerp(cleaner.from, cleaner.to, progress)
            let welcoming = Self.lerp(welcomer.from, welcomer.to, progress)
            let cleaningFigure = MascotFigureBox.cleaning.figureHeight(canvasSide: cleaning.height)
            XCTAssertEqual(cleaningFigure, heroFigure, accuracy: 0.001, "at \(progress)")
            XCTAssertEqual(
                MascotFigureBox.welcoming.figureHeight(canvasSide: welcoming.height),
                heroFigure,
                accuracy: 0.001,
                "at \(progress)"
            )
            XCTAssertEqual(
                cleaning.minY + cleaning.height * 43 / 360,
                welcoming.minY + welcoming.height * 51 / 360,
                accuracy: 0.001,
                "at \(progress)"
            )
        }
    }

    /// Before any confirmation is laid out, the busy card reads the hero's height from the tiers: the room is the
    /// sheet less the 18 pt grab zone and the 76 pt of actions. A compact hero is read at its minimum, which the
    /// ticket's slack can raise; the others at their cap.
    func testBeforeTheHeroIsLaidOutItsHeightComesFromTheTiers() {
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 610, accessibilityText: false), 64)
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 653.5, accessibilityText: false), 64)
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 654, accessibilityText: false), 116)
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 753, accessibilityText: false), 116)
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 794, accessibilityText: false), 136)
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 753, accessibilityText: true), 96)
        XCTAssertEqual(BookingSuccessView.expectedFigureHeight(sheetHeight: 610, accessibilityText: true), 96)
    }

    /// The busy card reads the hero it flies to: the booked confirmation's once that is laid out under the card,
    /// until then the one laid out unseen from the draft, whose ticket gives it the same slack, and only before
    /// either the tiers.
    func testTheBusyCardReadsTheBookedHeroThenTheDraftsThenTheTiers() {
        let booked = BookingSuccessView.mascotCanvas(figureHeight: 71.7)
        let draft = BookingSuccessView.mascotCanvas(figureHeight: 70)
        let height = { (hero: CGRect?, draft: CGRect?, sheet: CGFloat) in
            BookingSuccessView.busyFigureHeight(hero: hero, draft: draft, sheetHeight: sheet, accessibilityText: false)
        }
        XCTAssertEqual(height(booked, draft, 610), 71.7, accuracy: 0.001)
        XCTAssertEqual(height(nil, draft, 610), 70, accuracy: 0.001, "the draft's hero, not the compact minimum")
        XCTAssertEqual(height(nil, nil, 610), 64)
        XCTAssertEqual(height(nil, nil, 753), 116)
        XCTAssertEqual(
            BookingSuccessView.busyFigureHeight(hero: nil, draft: nil, sheetHeight: 610, accessibilityText: true),
            96
        )
    }

    private static func lerp(_ from: CGRect, _ to: CGRect, _ progress: CGFloat) -> CGRect {
        CGRect(
            x: from.minX + (to.minX - from.minX) * progress,
            y: from.minY + (to.minY - from.minY) * progress,
            width: from.width + (to.width - from.width) * progress,
            height: from.height + (to.height - from.height) * progress
        )
    }
}

private extension BookingSuccessView.Tiers {
    static var regularTiers: Self {
        BookingSuccessView.SizeClass.regular.tiers
    }
}
