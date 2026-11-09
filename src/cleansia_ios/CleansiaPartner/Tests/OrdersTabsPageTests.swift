import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The Orders tabs on the design's own sample: what the pay tile says and when it acts, the Active hero as the
/// detail's head, the day line's marks, the switcher's room and History's unpaid-cash line.
final class OrdersTabsPageTests: XCTestCase {
    private let english = Locale(identifier: "en")
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main

    // MARK: - The rows and the reserved tile

    func testTheReservedTileConfirmsAndSaysItsPayAsOneString() {
        let rows = OrdersDesignFixture.available()
        let reserved = PayTileFace(row: rows[4], isTaking: false)
        XCTAssertEqual(reserved.label, L10n.Offers.confirm)
        XCTAssertEqual(reserved.icon, "chevron.right")
        XCTAssertTrue(reserved.enabled)

        var cents = rows[1]
        cents.estimatedCleanerPay = 746.93
        XCTAssertEqual(
            PayTileFace(row: cents, isTaking: false).pay,
            OrdersFormat.money(746.93, symbol: "Kč"),
            "the haléře stay in the one string"
        )
        XCTAssertFalse(PayTileFace(row: rows[4], isTaking: true).enabled, "one take at a time")
    }

    /// An open row is one button: VoiceOver hears its time, area and pay, then that it opens the job, and nothing on
    /// it takes the job.
    func testAnOpenRowSpeaksAsOneButtonThatOpensTheJob() throws {
        var row = OrdersDesignFixture.available()[1]
        row.customerAddressApproximate = "Praha 8"
        let now = OrdersDesignFixture.now
        let label = OrdersAccessibility.openRowLabel(row, flags: [], now: now, locale: english)
        let window = try XCTUnwrap(OrdersBoardRules.window(of: row, locale: english))
        let pay = try XCTUnwrap(OrdersBoardRules.pay(row))
        XCTAssertTrue(label.contains(window), label)
        XCTAssertTrue(label.contains("Praha 8"), label)
        XCTAssertTrue(label.contains(pay), label)
        XCTAssertTrue(label.hasSuffix(L10n.Orders.opensTheJob + "."), label)
        XCTAssertFalse(label.contains(L10n.WorkContract.takeSheetTitle), label)
    }

    // MARK: - The Active hero

    /// The hero is the detail's head, built from her own job's row: its number, the status as the Core table reads
    /// it, the day and window, the four steps, the street with the area and the customer, the pay as one string.
    func testTheHeroSaysWhatTheDetailsHeadSays() {
        var row = OrdersDesignFixture.active()[0]
        row.displayOrderNumber = "ORD-7F3A9C21"
        let now = OrdersDesignFixture.now
        let facts = JobHeadFacts(row: row, run: OrdersBoardRules.runFacts(row), now: now, locale: english)
        XCTAssertEqual(facts.number, "#ORD-7F3A9C21")
        XCTAssertEqual(facts.statusWire, OrderStatus._2.rawValue)
        XCTAssertEqual(facts.statusLabel, L10n.Orders.statusLabel(._2))
        XCTAssertEqual(facts.day, OrdersFormat.shortDay(OrdersDesignFixture.at(12, 14, 0), locale: english))
        XCTAssertEqual(facts.window, "14:00–16:00")
        let setOffIn = L10n.Orders.heroIn(OrdersBoardRules.durationText(220))
        XCTAssertEqual(facts.status, [L10n.Orders.runStepSetOff, setOffIn])
        XCTAssertEqual(facts.stepCounter, L10n.Orders.stepCounter(1, 4))
        XCTAssertEqual(facts.progress, .steps(step: 1, fraction: nil, done: false))
        XCTAssertEqual(facts.street, "Vinohradská 46")
        XCTAssertEqual(facts.sub, ["Praha 2", "Jana Nováková"])
        XCTAssertEqual(facts.pay, OrdersFormat.money(779, symbol: "Kč"))
        XCTAssertEqual(facts.caption, L10n.WorkContract.reward)

        let crew = OrdersDesignFixture.active()[1]
        XCTAssertEqual(
            JobHeadFacts(row: crew, run: OrdersBoardRules.runFacts(crew), now: now, locale: english).caption,
            L10n.Orders.yourShare
        )

        let cleaning = OrdersDesignFixture.active(heroStatus: ._4)[0]
        let progress = JobHeadFacts(
            row: cleaning,
            run: OrdersBoardRules.runFacts(cleaning),
            now: OrdersDesignFixture.at(12, 15, 12),
            locale: english
        )
        XCTAssertEqual(progress.stepCounter, L10n.Orders.stepCounter(3, 4))
        guard case let .steps(step: 3, fraction: fraction?, done: false) = progress.progress else {
            return XCTFail("cleaning: \(progress.progress)")
        }
        XCTAssertEqual(fraction, 0.6, accuracy: 0.001, "the booked window gone fills the current step")
    }

    // MARK: - The day line

    /// Every mark is at least 12 wide; two whose drawn boxes touch take the upper and lower half of the track, her own
    /// job on top, else the earlier; a real clash is the red block, never two lanes.
    func testMarksWhoseDrawnBoxesTouchShareTheTrackHerOwnOnTop() {
        let range = (from: 420, to: 1140)
        let mark = { (span: (Int, Int), mine: Bool) in
            OrdersDayLine.drawn((start: span.0, end: span.1), mine: mine, range: range, track: 300)
        }
        let short = mark((735, 742), false)
        XCTAssertEqual(short.width, 12, "a 7-minute job is still a mark")
        XCTAssertEqual(short.left + short.width / 2, 132.71, accuracy: 0.01, "centred on its span")
        XCTAssertEqual(mark((480, 720), false).left, 26, accuracy: 0.01, "a long span keeps its box")

        let lanes = OrdersDayLine.lanes
        XCTAssertEqual(
            lanes([mark((480, 720), false), mark((735, 742), false), mark((750, 757), false), mark((840, 960), true)]),
            [.full, .upper, .lower, .full],
            "12:15–12:22 and 12:30–12:37 read as two marks"
        )
        XCTAssertEqual(lanes([mark((735, 742), false), mark((722, 729), true)]), [.lower, .upper], "hers on top")
        XCTAssertEqual(lanes([mark((840, 960), true), mark((900, 1020), false)]), [.full, .full], "a clash")
        XCTAssertEqual(
            lanes([mark((480, 600), false), mark((540, 660), false), mark((700, 760), false)]),
            [.upper, .lower, .full]
        )
        XCTAssertEqual(lanes([mark((480, 600), false), mark((600, 660), false)]), [.full, .full], "slots that touch")
        XCTAssertEqual(lanes([]), [])
    }

    // MARK: - The switcher

    /// The three segments share the track equally; one whose label and count need more takes it from the others.
    func testTheSwitcherSharesItsTrackAndALongLabelTakesTheRoomItNeeds() {
        XCTAssertEqual(OrdersTabSwitcher.widths(needs: [100, 90, 80], total: 360), [120, 120, 120])
        XCTAssertEqual(OrdersTabSwitcher.widths(needs: [150, 90, 80], total: 330), [150, 90, 90])
        XCTAssertEqual(OrdersTabSwitcher.widths(needs: [170, 120, 60], total: 330), [170, 120, 60])
        XCTAssertEqual(OrdersTabSwitcher.widths(needs: [], total: 330), [])
    }

    // MARK: - History's unpaid cash

    /// "Cash: the customer did not pay 1 440 Kč": the verb is held to its amount, so the fact breaks only between its
    /// meanings, in every shipped language; the words do not change.
    func testHistorysUnpaidCashHoldsItsVerbToTheAmount() throws {
        let tied = [
            "en": "Cash: the customer did\u{00A0}not\u{00A0}pay\u{00A0}%1$@",
            "cs": "Hotově: zákazník nezaplatil\u{00A0}%1$@",
            "sk": "V hotovosti: zákazník nezaplatil\u{00A0}%1$@",
            "uk": "Готівка: клієнт не\u{00A0}заплатив\u{00A0}%1$@",
            "ru": "Наличные: клиент не\u{00A0}заплатил\u{00A0}%1$@"
        ]
        for (language, expected) in tied {
            XCTAssertEqual(try table(language)["history_cash_not_paid"], expected, language)
        }
        let amount = OrdersBoardRules.nonBreaking(OrdersFormat.money(1440, symbol: "Kč", locale: english))
        XCTAssertEqual(
            L10n.Orders.historyCashNotPaid(amount).components(separatedBy: " "),
            ["Cash:", "the", "customer", "did\u{00A0}not\u{00A0}pay\u{00A0}\(amount)"]
        )
    }

    private func table(_ language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"), language)
        let url = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(NSDictionary(contentsOf: url) as? [String: String], language)
    }
}

/// The job detail's band map, the Job taken panel's strip, the head's mascot and the Note and Issue
/// buttons.
@MainActor
final class JobMapAndPanelTests: XCTestCase {
    private let precise = OrderLocation.precise(.init(
        line: "Zenklova 6, Praha, 18000",
        coordinate: Coordinate(latitude: 50.11, longitude: 14.47)
    ))

    // MARK: - The band map

    func testAJobNotTakenShowsItsAreaLockedAndNeverAnExactPlace() {
        XCTAssertEqual(
            JobBandMap.mode(location: .approximate("Praha 8"), area: "Praha 8", taken: false),
            .area("Praha 8", locked: true)
        )
        XCTAssertEqual(
            JobBandMap.mode(location: precise, area: "Praha 8", taken: false),
            .area("Praha 8", locked: true),
            "a coordinate on a job she has not taken is never used"
        )
        XCTAssertEqual(JobBandMap.mode(location: .none, area: nil, taken: false), .area(nil, locked: true))
    }

    func testHerJobShowsTheAddressPinnedAtEveryStep() {
        XCTAssertEqual(
            JobBandMap.mode(location: precise, area: "Praha 8", taken: true),
            .pin(Coordinate(latitude: 50.11, longitude: 14.47), street: "Zenklova 6")
        )
    }

    func testHerJobWithNoCoordinateShowsItsAreaUnlocked() {
        let noPlace = OrderLocation.precise(.init(line: "Zenklova 6, Praha, 18000", coordinate: nil))
        XCTAssertEqual(
            JobBandMap.mode(location: noPlace, area: "Praha 8", taken: true),
            .area("Praha 8", locked: false)
        )
        XCTAssertEqual(
            JobBandMap.mode(location: .approximate("Praha 8"), area: "Praha 8", taken: true),
            .area("Praha 8", locked: false),
            "a finished job whose street is gone keeps the plain area map"
        )
    }

    func testOnlyHerJobsPinnedMapMovesUnderHerFinger() {
        let pinned = JobBandMap.mode(location: precise, area: "Praha 8", taken: true)
        XCTAssertTrue(pinned.moves)
        XCTAssertFalse(
            JobBandMap.mode(location: precise, area: "Praha 8", taken: false).moves,
            "the blurred area before the take stays still"
        )
        XCTAssertFalse(JobBandMap.area("Praha 8", locked: false).moves, "her job with no coordinate: the veiled area")
    }

    func testTheRecentreButtonShowsOnlyOnceHerMapIsMovedOffTheJob() {
        let pinned = JobBandMap.mode(location: precise, area: "Praha 8", taken: true)
        XCTAssertFalse(pinned.showsRecentre(movedAway: false), "already framed on the job")
        XCTAssertTrue(pinned.showsRecentre(movedAway: true))
        XCTAssertFalse(JobBandMap.area("Praha 8", locked: true).showsRecentre(movedAway: true), "a still map")
    }

    /// A detail opened again is a new map with a new locator. The found areas outlive this test, so it looks up names
    /// no other test does.
    func testAFoundAreaIsLookedUpOnceForEveryMapAndAMissIsAskedAgain() async {
        let run = UUID().uuidString
        let area = "Praha 8 \(run)"
        let nowhere = "Nowhere \(run)"
        var asked: [String] = []
        let found = Coordinate(latitude: 50.12, longitude: 14.46)
        let geocode: (String) async -> Coordinate? = { query in
            asked.append(query)
            return query == area ? found : nil
        }
        let first = JobAreaLocator(geocode: geocode)
        await first.locate(area)
        XCTAssertEqual(first.centre, found)
        let second = JobAreaLocator(geocode: geocode)
        await second.locate(area)
        XCTAssertEqual(second.centre, found)
        XCTAssertEqual(asked, [area], "a found area is looked up once for every map")
        await second.locate(nowhere)
        XCTAssertEqual(second.centre, JobAreaLocator.marketCity)
        await second.locate(nowhere)
        XCTAssertEqual(asked, [area, nowhere, nowhere], "a miss is asked again: that lookup may have been offline")
        await second.locate(nil)
        XCTAssertEqual(second.centre, JobAreaLocator.marketCity)
        XCTAssertEqual(asked, [area, nowhere, nowhere], "no area, nothing to look up")
    }

    // MARK: - The Job taken panel

    func testTheTakenPanelDrawsItsStripOnlyForATakenJobWithACoordinate() {
        let row = OrdersDesignFixture.available()[1]
        var placed = OrdersDesignFixture.taken(row)
        placed.taken?.customerAddressLatitude = 50.03
        placed.taken?.customerAddressLongitude = 14.51
        XCTAssertEqual(
            TakenContract.strip(placed),
            .pin(Coordinate(latitude: 50.03, longitude: 14.51), street: "Opatovská 8")
        )
        placed.taken?.customerAddressLongitude = nil
        XCTAssertNil(TakenContract.strip(placed), "no coordinate, no strip")
        XCTAssertEqual(TakenContract.strip(BoardTake(snapshot: row)), .waiting, "held while the job is looked up")
        XCTAssertNil(TakenContract.strip(BoardTake(snapshot: row, takenLookupDone: true)))
        XCTAssertNil(TakenContract.strip(nil))
    }

    // MARK: - The head's mascot

    func testCleaningPlaysTheCleaningMascotAndHoldsItsStillUnderReducedMotion() {
        XCTAssertEqual(
            OrderDetailMascotArt.art(for: ._4, cleaning: true),
            .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        )
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._4, cleaning: true, reduceMotion: true), .still(.cleaning))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._4, cash: true), .still(.invoice), "finishing keeps its figure")
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._4), .still(.sprayAndCloth))
    }

    // MARK: - Note and Issue

    func testNoteIsTonalAndIssueIsInTheErrorTone() {
        let buttons = NotesAndIssuesSection.addButtons
        XCTAssertEqual(buttons.map(\.entry), [.addNote, .reportIssue])
        XCTAssertEqual(buttons.map(\.tone), [.primary, .error])
    }
}
