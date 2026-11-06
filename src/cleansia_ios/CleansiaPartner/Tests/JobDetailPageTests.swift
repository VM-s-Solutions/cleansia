import CleansiaCore
import CleansiaPartnerApi
import MapKit
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// The job detail on the design's own sample: what the head says, and which reference sections each state draws under
/// the run sheet, each fact said once.
@MainActor
final class JobDetailPageTests: XCTestCase {
    private let english = Locale(identifier: "en")
    private let myId = "emp-self"

    // MARK: - The head

    func testTheHeadCountsTheRunInFourSteps() throws {
        let today = try head(DetailJob.a1(status: 2), now: day(12, 9, 20))
        XCTAssertEqual(today.stepCounter, L10n.Orders.stepCounter(1, 4))
        guard case .steps(step: 1, fraction: nil, done: false) = today.progress else {
            return XCTFail("today: \(today.progress)")
        }

        let progress = try head(DetailJob.a1(status: 4, history: DetailJob.a1Run), now: day(12, 15, 12))
        XCTAssertEqual(progress.stepCounter, L10n.Orders.stepCounter(3, 4))
        guard case let .steps(step: 3, fraction: fraction?, done: false) = progress.progress else {
            return XCTFail("progress: \(progress.progress)")
        }
        XCTAssertEqual(fraction, 0.6, accuracy: 0.001, "the booked window gone")

        let crewWait = try head(DetailJob.a2(mine: day(13, 17, 44), mate: nil), now: day(13, 17, 46))
        XCTAssertEqual(crewWait.stepCounter, L10n.Orders.stepCounter(4, 4))

        var completedItem = DetailJob.a1(status: 5, history: DetailJob.a1Run + [(5, day(12, 15, 51))])
        completedItem.completedAt = day(12, 15, 51)
        let completed = try head(completedItem, now: day(12, 16, 20))
        guard case .steps(step: 4, fraction: _, done: true) = completed.progress else {
            return XCTFail("completed: \(completed.progress)")
        }

        let browse = try head(DetailJob.notTaken(OrdersDesignFixture.available()[2], order: "ORD-4C7D8812"))
        XCTAssertEqual(browse.progress, .none)
        XCTAssertNil(browse.stepCounter)
        XCTAssertEqual(try head(DetailJob.x1()).progress, .cancelled)
    }

    func testTheHeadSaysItsNumberAndTheStatusAsTheCoreTableReadsIt() throws {
        let progress = try head(DetailJob.a1(status: 4, history: DetailJob.a1Run), now: day(12, 15, 12))
        XCTAssertEqual(progress.number, "#ORD-7F3A9C21")
        XCTAssertEqual(progress.statusWire, OrderStatus._4.rawValue)
        XCTAssertEqual(progress.statusLabel, L10n.Orders.statusLabel(._4))
        XCTAssertEqual(progress.day, OrdersFormat.shortDay(day(12, 14, 0), locale: english))
        XCTAssertEqual(progress.window, "14:00–16:00")

        let browse = try head(DetailJob.notTaken(OrdersDesignFixture.available()[2], order: "ORD-4C7D8812"))
        XCTAssertEqual(browse.statusWire, OrderStatus._0.rawValue, "a job she has not taken reads New")
        XCTAssertEqual(browse.statusLabel, L10n.Orders.statusLabel(._0))
    }

    /// One string through the app's money formatter, in one text: never a big figure with a small fraction.
    func testTheHeadSaysThePayAsOneString() async throws {
        let facts = try head(DetailJob.dev(), now: day(2, 9, 55, month: 11))
        let pay = OrdersFormat.money(746.93, symbol: "Kč")
        XCTAssertEqual(facts.pay, pay)
        XCTAssertEqual(facts.caption, L10n.WorkContract.reward)

        let spoken = try await HostedSpeech.labels(of: JobHead(facts: facts))
        XCTAssertTrue(spoken.contains(HostedSpeech.plain(pay)), "\(spoken)")
        XCTAssertFalse(spoken.contains("746"), "\(spoken)")
    }

    /// The customer's name is said by her row where the page draws one (Setting off and Getting in, a completed job's
    /// Customer section); otherwise the head's sub line says it.
    func testTheCustomersNameIsSaidOnce() throws {
        XCTAssertEqual(try head(DetailJob.a1(status: 2), now: day(12, 9, 20)).sub, ["Praha 2"])
        XCTAssertEqual(
            try head(DetailJob.a1(status: 3, history: [(3, day(12, 13, 22))]), now: day(12, 13, 40)).sub,
            ["Praha 2"]
        )
        XCTAssertEqual(
            try head(DetailJob.a1(status: 4, history: DetailJob.a1Run), now: day(12, 15, 12)).sub,
            ["Praha 2", "Jana Nováková"]
        )
        var completed = DetailJob.a1(status: 5, history: DetailJob.a1Run + [(5, day(12, 15, 51))])
        completed.completedAt = day(12, 15, 51)
        XCTAssertEqual(try head(completed, now: day(12, 16, 20)).sub, ["Praha 2"])
        let past = try head(DetailJob.h7(), now: day(12, 9, 20))
        XCTAssertEqual(past.street, "Praha 9")
        XCTAssertEqual(past.sub, [], "a past job's scope is its section's")
    }

    /// The weekday is the calendar line's: a job she has not taken says only how far off its day is.
    func testANotTakenJobSaysHowFarOffItsDayIsNeverTheWeekday() throws {
        let browse = try head(DetailJob.notTaken(OrdersDesignFixture.available()[2], order: "ORD-4C7D8812"))
        XCTAssertEqual(browse.status, [L10n.Orders.dayTomorrow])
    }

    // MARK: - The reference sections

    func testBeforeSetOffTheCustomersWordsTheScopeAndTheContract() throws {
        let plan = try OrderDetail(DetailJob.a1(status: 2)).sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertEqual(plan.customer, .init(who: nil, notes: true, access: false))
        XCTAssertEqual(plan.scope, .init(rows: true, dirt: nil, team: nil))
        XCTAssertNil(plan.payment, "a card job before completion: nothing the head does not say")
    }

    /// While the checklist is open it is the scope, so Property & scope keeps only dirt and crew, and goes with
    /// neither.
    func testWhileCleaningTheChecklistIsTheScopeAndTheWayInIsTheCustomers() throws {
        let progress = try OrderDetail(DetailJob.a1(status: 4, history: DetailJob.a1Run))
            .sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertEqual(progress.customer, .init(who: nil, notes: true, access: true))
        XCTAssertNil(progress.scope)
        XCTAssertNil(progress.payment)

        var ready = DetailJob.a1(status: 4, history: DetailJob.a1Run)
        ready.hasAfterPhotos = true
        XCTAssertNil(try OrderDetail(ready).sectionPlan(myEmployeeId: myId, photos: []).scope, "Finishing holds it")
    }

    /// The crew's finishing step draws the gates, not the checklist: the scope is its section's, with the team.
    func testACrewsFinishingKeepsTheScopeAndTheWayInWithoutTheNotes() throws {
        let plan = try OrderDetail(DetailJob.a2(mine: day(13, 17, 44), mate: nil))
            .sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertEqual(plan.customer, .init(who: nil, notes: false, access: true))
        XCTAssertEqual(plan.scope, .init(rows: true, dirt: nil, team: L10n.Orders.heroTeam(2)))
        XCTAssertNil(plan.payment)
    }

    func testACompletedJobNamesTheCustomerOnceAndWhereThePayoutGoes() throws {
        var item = DetailJob.a1(status: 5, history: DetailJob.a1Run + [(5, day(12, 15, 51))])
        item.completedAt = day(12, 15, 51)
        let plan = try OrderDetail(item).sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertEqual(plan.customer, .init(who: .contact, notes: false, access: false))
        XCTAssertEqual(plan.scope, .init(rows: true, dirt: nil, team: nil))
        XCTAssertEqual(plan.payment, .init(cashRule: nil, payouts: true))

        let past = try OrderDetail(DetailJob.h7()).sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertEqual(
            past.customer,
            .init(who: .removed(L10n.Orders.customerDetailsClosedCompleted), notes: false, access: false)
        )
        XCTAssertEqual(past.scope?.rows, true)
        XCTAssertEqual(past.scope?.dirt, L10n.Orders.dirtinessLevel(._1))
    }

    /// A cash job's settlement is Payment's, as shipped; the amount itself is the head's flag and the move's.
    func testACashJobsRuleIsThePaymentSections() throws {
        var item = DetailJob.a3(status: 4)
        item.hasAfterPhotos = true
        let plan = try OrderDetail(item).sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertEqual(
            plan.payment,
            .init(
                cashRule: L10n.Orders.cashRule(OrdersBoardRules.nonBreaking(OrdersFormat.money(714, symbol: "Kč"))),
                payouts: false
            )
        )
        XCTAssertEqual(plan.customer, .init(who: nil, notes: false, access: true), "the cash step holds the words")
        XCTAssertEqual(plan.scope?.rows, true)
    }

    /// Mike's DEV job: no notes, no way in, the checklist open and a card: only the contract is left.
    func testTheDevJobDrawsNoReferenceSectionButTheContract() throws {
        let plan = try OrderDetail(DetailJob.dev()).sectionPlan(myEmployeeId: myId, photos: [])
        XCTAssertNil(plan.customer)
        XCTAssertNil(plan.scope)
        XCTAssertNil(plan.payment)
    }

    // MARK: - The crew

    /// The crew-mate's row says who and where she stands; Call is the pinned move's alone.
    func testTheCrewMatesRowCarriesNoCall() async throws {
        let order = try OrderDetail(DetailJob.a2(mine: day(13, 17, 44), mate: nil))
        let gate = try await HostedSpeech.labels(of: CrewGate(
            order: order,
            myEmployeeId: myId,
            afterPhoto: day(13, 17, 38)
        ))
        XCTAssertTrue(gate.contains { $0.contains("Lenka") }, "\(gate)")
        XCTAssertFalse(gate.contains { $0 == HostedSpeech.plain(L10n.Orders.actionCall) }, "\(gate)")
    }

    // MARK: - The figure

    func testACancelledOrFailedPageRests() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._6), .still(.resting))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._2, failed: true), .still(.resting))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._2), .still(.waving))
    }

    /// The figure shrinks on an SE, decided from the whole window: inside its safe area an SE still has 647.
    func testOnlyAnSEsWindowIsShort() {
        typealias Frame = JobDetailFrame<EmptyView, EmptyView, EmptyView>
        XCTAssertTrue(Frame.isShortScreen(windowHeight: 667), "SE")
        XCTAssertFalse(Frame.isShortScreen(windowHeight: 812), "mini")
        XCTAssertFalse(Frame.isShortScreen(windowHeight: 852), "the 393 phones")
    }

    // MARK: - The panel over the map

    /// The panel rests expanded at the band's foot, under the back button, or collapsed onto its head; nowhere
    /// between.
    func testThePanelRestsAtTheBandsFootOrOnItsHead() {
        typealias Frame = JobDetailFrame<EmptyView, EmptyView, EmptyView>
        XCTAssertEqual(Frame.stops, [.mapFocus, .expanded])
        XCTAssertEqual(Frame.expandedTop(figure: true, map: true), 136)
        XCTAssertEqual(Frame.expandedTop(figure: false, map: true), 106)
        XCTAssertEqual(Frame.peekHeight(head: 180), 17 + 180 + Spacing.xs, "the handle, the head and its edge")
        XCTAssertEqual(Frame.peekHeight(head: 0), 0, "no head of its own: the sheet's own share")
    }

    /// The map stands still at its full height behind the panel, drawn as the band drew it: the same metres to the
    /// point, the pin and the lock chip where the band put them.
    func testTheFullHeightMapKeepsTheBandsScaleAndPin() {
        let layout = JobBandMapLayout(size: CGSize(width: 393, height: 680), top: 54, visible: 136)
        let bandMap = 54 + 136 + Spacing.l
        XCTAssertEqual(layout.metres(700) / 680, 700 / Double(bandMap), accuracy: 1e-9)
        XCTAssertEqual(layout.pinAnchor * 680, 54 + 136 * 0.72, accuracy: 0.001)
        XCTAssertEqual(layout.chipY, 54 + 136 * 0.36, accuracy: 0.001)
        XCTAssertEqual(layout.side, 680, "a square as tall as the map, so MapKit fits the region to its height")
        XCTAssertEqual(JobBandMapLayout(size: CGSize(width: 700, height: 400), top: 0, visible: 100).side, 700)
    }

    /// Apple's logo and Legal ride the panel's edge wherever it rests, inside the phone's width and short of the
    /// figure.
    func testTheMapsMarksRideThePanelsEdge() {
        let layout = JobBandMapLayout(size: CGSize(width: 393, height: 680), top: 54, visible: 136)
        let expanded = layout.attributionInsets(sheetTop: 190, figure: 144)
        XCTAssertEqual(expanded.bottom, 680 - 190 + Spacing.xs)
        XCTAssertEqual(expanded.leading, (680 - 393) / 2 + Spacing.s)
        XCTAssertEqual(expanded.trailing, (680 - 393) / 2 + Spacing.s + 144)
        XCTAssertEqual(layout.attributionInsets(sheetTop: 530, figure: 0).bottom, 680 - 530 + Spacing.xs)
        XCTAssertEqual(layout.attributionInsets(sheetTop: 0, figure: 0).bottom, Spacing.xs, "outside a sheet")
    }

    // MARK: - The pinned move

    /// Under the move: the home indicator's inset alone, 12 on a phone without one.
    func testThePinnedMoveSitsOnTheHomeIndicatorInset() {
        XCTAssertEqual(StickyActionFooter.bottomPadding(homeIndicator: 34), 0)
        XCTAssertEqual(StickyActionFooter.bottomPadding(homeIndicator: 0), Spacing.s)
    }

    // MARK: - The contacts

    /// Navigate · Call · SMS three across in the Active hero's 329, Navigate over the pair in the run sheet's 325, one
    /// column where the pair does not fit: the widest chip, Navigate, needs 104.33 (12 + 16 + 6 + its word + 12).
    func testTheContactsShareTheirRowAsTheDesignFitsThem() {
        let needs: [CGFloat] = [104.33, 72, 76]
        XCTAssertEqual(ContactRows.rows(needs: needs, width: 329, spacing: 8), [3], "the hero")
        XCTAssertEqual(ContactRows.rows(needs: needs, width: 325, spacing: 8), [1, 2], "the run sheet")
        XCTAssertEqual(ContactRows.rows(needs: needs, width: 150, spacing: 8), [1, 1, 1])
        XCTAssertEqual(ContactRows.rows(needs: [72, 76], width: 329, spacing: 8), [2], "no street to navigate to")
        XCTAssertEqual(ContactRows.rows(needs: [104.6, 72, 76], width: 329, spacing: 8), [3], "half a point short")
        XCTAssertEqual(ContactRows.rows(needs: [104.9, 72, 76], width: 329, spacing: 8), [1, 2])
    }

    /// The chip's icon box is the design's 18 at the default size and grows and shrinks with the type, so the glyph
    /// drawn in it at an accessibility size stays inside it, clear of its word.
    func testTheChipsIconBoxFollowsTheTextSize() {
        XCTAssertEqual(TonalChip.iconBox(.large), 18, accuracy: 0.01)
        XCTAssertGreaterThan(TonalChip.iconBox(.accessibility1), 18 * 1.4)
        XCTAssertLessThan(TonalChip.iconBox(.xSmall), 18)
    }

    func testTheChipsWordIsInTheTextInkAndOnlyItsIconTakesTheTone() {
        XCTAssertEqual(TonalChip.ink, CleansiaColors.onSurface)
        XCTAssertEqual(TonalChip.label, CleansiaTextStyle.nunito(.semibold, size: 14))
        XCTAssertEqual(TonalChip.Tone.primary.icon, CleansiaColors.primary)
        XCTAssertEqual(TonalChip.Tone.error.icon, CleansiaColors.error)
    }

    // MARK: - Fixtures

    private func head(_ item: OrderItem, now: Date = OrdersDesignFixture.now) throws -> JobHeadFacts {
        try OrderDetail(item).headFacts(myEmployeeId: myId, photos: [], band: nil, now: now, locale: english)
    }

    private func day(_ day: Int, _ hour: Int, _ minute: Int, month: Int = 11) -> Date {
        OrdersDesignFixture.at(day, hour, minute, month: month)
    }
}

extension PartnerOrderDetailVisualTests {
    /// The secondary buttons: the run sheet's contacts on setting off, and Note and Issue while she cleans.
    func testMockSecondaryButtons() async throws {
        let notes = MockCapture.Scroll.toolbar(L10n.Orders.photosSectionTitle)
        for theme in ["light", "dark"] {
            try await mockShot(.setoff, "en", theme, suffix: "-contacts")
            try await mockShot(.progress, "en", theme, scroll: notes, suffix: "-photos-notes")
        }
    }

    /// The detail's panel collapsed onto its head over the still map, before the take and on her own job.
    func testMockPanelCollapsed() async throws {
        for theme in ["light", "dark"] {
            for state in [DetailState.browse, .setoff, .progress] {
                try await mockShot(state, "en", theme, suffix: "-map-collapsed", settle: 6)
            }
        }
        for state in [DetailState.browse, .setoff, .progress] {
            try await mockShot(state, "uk", "light", suffix: "-map-collapsed", settle: 6)
        }
    }

    /// The panel scrolled a little under its pinned head, expanded and collapsed, before the take and on her own job.
    func testMockScrolledUnderHead() async throws {
        for theme in ["light", "dark"] {
            for state in [DetailState.browse, .progress] {
                for suffix in ["-scrolled", "-scrolled-collapsed"] {
                    try await mockShot(state, "en", theme, scroll: .offset(24), suffix: suffix)
                }
            }
        }
    }

    /// Her job's map moved off the address: the pin and its street ride the map, the recentre button stands beside
    /// the back button, the panel and the figure stay where they were.
    func testMockMapPanned() async throws {
        for theme in ["light", "dark"] {
            for suffix in ["-map-panned", "-map-panned-collapsed"] {
                let pan = Task { @MainActor in
                    try await Task.sleep(nanoseconds: 3_000_000_000)
                    Self.panTheMap()
                }
                try await mockShot(.progress, "en", theme, suffix: suffix, settle: 6)
                pan.cancel()
            }
        }
    }

    /// Her finger's pan on the shot's map while it settles: the drawing 90 to the left and 30 down.
    private static func panTheMap() {
        func maps(_ view: UIView) -> [MKMapView] {
            ((view as? MKMapView).map { [$0] } ?? []) + view.subviews.flatMap(maps)
        }
        let window = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        guard let map = window.flatMap({ maps($0).first }) else { return XCTFail("no map to pan") }
        let centre = map.convert(map.centerCoordinate, toPointTo: map)
        map.setCenter(map.convert(CGPoint(x: centre.x + 90, y: centre.y - 30), toCoordinateFrom: map), animated: false)
    }
}

extension PartnerOrdersVisualTests {
    func testMockActiveHeroContacts() async throws {
        for theme in ["light", "dark"] {
            try await shoot("active", "next", "en", theme, board: Board(tab: .active))
        }
    }
}
