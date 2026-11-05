import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The Orders board's pure rules (spec §3, §16.1). Times are built in the device's zone, as the board shows them.
final class OrdersBoardRulesTests: XCTestCase {
    private let english = Locale(identifier: "en")
    private typealias Rules = OrdersBoardRules

    // MARK: - clash (the server's overlap rule)

    func testAnOverlappingOwnJobClashes() {
        let own = job("a", at(10, 0), minutes: 180)
        XCTAssertEqual(Rules.clashes(job("c", at(12, 0), minutes: 120), own: [own])?.id, "a")
    }

    func testWindowsThatOnlyTouchDoNotClash() {
        let own = job("a", at(10, 0), minutes: 120)
        XCTAssertNil(Rules.clashes(job("after", at(12, 0), minutes: 60), own: [own]), "own end = candidate start")
        XCTAssertNil(Rules.clashes(job("before", at(9, 0), minutes: 60), own: [own]), "candidate end = own start")
    }

    func testAContainedWindowClashes() {
        let own = job("a", at(9, 0), minutes: 300)
        XCTAssertEqual(Rules.clashes(job("c", at(10, 0), minutes: 60), own: [own])?.id, "a")
    }

    func testACandidateSpanningMidnightClashesWithTheNextMorning() {
        let own = job("a", at(0, 30, dayOffset: 1), minutes: 60)
        XCTAssertEqual(Rules.clashes(job("c", at(23, 0), minutes: 120), own: [own])?.id, "a")
    }

    func testAMissingStartNeverClashesAndAMissingEstimateIsAPoint() {
        let own = job("a", at(10, 0), minutes: 120)
        XCTAssertNil(Rules.clashes(job("c", nil, minutes: 120), own: [own]))
        XCTAssertNil(Rules.clashes(job("c", at(9, 0), minutes: 120), own: [job("b", nil, minutes: 60)]))
        XCTAssertEqual(Rules.clashes(job("c", at(11, 0), minutes: 0), own: [own])?.id, "a")
        let point = job("p", at(10, 30), minutes: nil)
        XCTAssertEqual(Rules.clashes(job("c", at(10, 0), minutes: 60), own: [point])?.id, "p")
    }

    func testTheEarliestOfSeveralClashingJobsIsNamed() {
        let later = job("later", at(12, 0), minutes: 60)
        let earlier = job("earlier", at(10, 0), minutes: 120)
        XCTAssertEqual(Rules.clashes(job("c", at(9, 0), minutes: 300), own: [later, earlier])?.id, "earlier")
    }

    // MARK: - live flag and expiry

    func testAJobUnderWayIsFlaggedUntilItsBookedEnd() {
        let now = at(11, 0)
        for status in [OrderStatus._3, ._4] {
            let row = job("u", at(9, 0), minutes: 240, status: status)
            XCTAssertEqual(Rules.liveFlag(row, now: now), .underWay(until: at(13, 0)), "\(status)")
        }
    }

    func testAnOpenJobPastItsStartCanBeTakenUntilItsEnd() {
        let now = at(10, 0)
        for status in [OrderStatus._0, ._2] {
            let row = job("t", at(9, 0), minutes: 180, status: status)
            XCTAssertEqual(Rules.liveFlag(row, now: now), .takeUntil(at(12, 0)), "\(status)")
        }
        XCTAssertNil(Rules.liveFlag(job("f", at(15, 0), minutes: 60), now: now), "a future job has no live flag")
    }

    func testAJobWhoseSlotHasEndedIsExpired() {
        let row = job("e", at(9, 0), minutes: 60)
        XCTAssertTrue(Rules.isExpired(row, now: at(10, 0)))
        XCTAssertTrue(Rules.isExpired(row, now: at(10, 1)))
        XCTAssertFalse(Rules.isExpired(row, now: at(9, 59)))
    }

    // MARK: - hero

    func testTheHeroIsPickedByStatusFirstThenByStart() {
        let confirmed = job("c", at(8, 0), minutes: 60, status: ._2)
        let onTheWay = job("w", at(12, 0), minutes: 60, status: ._3)
        let inProgress = job("p", at(15, 0), minutes: 60, status: ._4)
        XCTAssertEqual(Rules.heroPick([confirmed, onTheWay, inProgress])?.id, "p", "in progress beats earlier")
        XCTAssertEqual(Rules.heroPick([confirmed, onTheWay])?.id, "w", "on the way beats confirmed")
        let early = job("early", at(7, 0), minutes: 60, status: ._2)
        XCTAssertEqual(Rules.heroPick([confirmed, early])?.id, "early", "ties by start")
        XCTAssertNil(Rules.heroPick([job("x", at(7, 0), minutes: 60, status: ._5)]))
    }

    func testTheHeroControlFollowsTheStatusAndNeverCompletes() {
        let start = at(15, 0)
        let confirmed = job("c", start, minutes: 60, status: ._2)
        let opensAt = at(14, 0)
        XCTAssertEqual(Rules.heroControl(confirmed, now: at(13, 59)), .setOff(opensAt: opensAt, enabled: false))
        XCTAssertEqual(Rules.heroControl(confirmed, now: opensAt), .setOff(opensAt: opensAt, enabled: true))
        XCTAssertEqual(Rules.heroControl(job("w", start, minutes: 60, status: ._3), now: opensAt), .start)
        XCTAssertEqual(Rules.heroControl(job("p", start, minutes: 60, status: ._4), now: opensAt), .openJob)
        XCTAssertEqual(Rules.setOffOpensAt(confirmed), opensAt)
        XCTAssertEqual(Rules.startGraceWindowMinutes, 60)
    }

    // MARK: - cancelling and the summary gate

    func testTheCancelLineFollowsTheStatusTheStartAndTheVersion() {
        let now = at(10, 0)
        let future = at(15, 0)
        let past = at(9, 0)
        for version in ["2026-09-29", "2026-10-05", "2026-10-27"] {
            XCTAssertEqual(Rules.cancelLine(status: ._2, start: future, now: now, version: version), .freeUntil(future))
            XCTAssertEqual(Rules.cancelLine(status: ._0, start: future, now: now, version: version), .freeUntil(future))
        }
        for version in ["2026-09-20", "2027-01-01"] {
            XCTAssertNil(Rules.cancelLine(status: ._2, start: future, now: now, version: version), version)
        }
        for version in ["2026-09-20", "2026-09-29", "2026-10-05", "2026-10-27", "2027-01-01"] {
            XCTAssertEqual(Rules.cancelLine(status: ._3, start: past, now: now, version: version), .underWay)
            XCTAssertEqual(Rules.cancelLine(status: ._4, start: past, now: now, version: version), .underWay)
            XCTAssertEqual(Rules.cancelLine(status: ._2, start: past, now: now, version: version), .startPassed)
        }
    }

    func testTheCancelTextBeforeAndAfterTheTake() {
        let start = at(15, 0)
        let free = L10n.WorkContract.cancelFreeUntil(OrdersFormat.shortDayTime(start, locale: english))
        XCTAssertEqual(Rules.cancelText(.freeUntil(start), phase: .beforeTake, locale: english), free)
        XCTAssertEqual(
            Rules.cancelText(.underWay, phase: .beforeTake, locale: english),
            L10n.WorkContract.cancelUnderWay
        )
        XCTAssertEqual(
            Rules.cancelText(.startPassed, phase: .beforeTake, locale: english),
            L10n.WorkContract.cancelStartPassed
        )
        XCTAssertEqual(Rules.cancelText(.freeUntil(start), phase: .taken, locale: english), free)
        for line in [Rules.CancelLine.underWay, .startPassed] {
            let text = Rules.cancelText(line, phase: .taken, locale: english)
            XCTAssertEqual(text, L10n.WorkContract.doneCannotCancel)
            XCTAssertNotEqual(text, L10n.WorkContract.cancelUnderWay)
            XCTAssertNotEqual(text, L10n.WorkContract.cancelStartPassed)
        }
        XCTAssertNil(Rules.cancelText(nil, phase: .taken, locale: english))
        XCTAssertNil(Rules.cancelText(nil, phase: .beforeTake, locale: english))
    }

    func testTheSummaryShowsOnlyOnTakeOrAcceptOfTheTwoCheckedVersions() {
        for version in ["2026-10-05", "2026-10-27"] {
            XCTAssertTrue(Rules.summaryVisible(.take(orderId: "o"), version: version))
            XCTAssertTrue(Rules.summaryVisible(.accept(orderId: "o"), version: version))
            XCTAssertFalse(Rules.summaryVisible(.read(acceptanceId: "a"), version: version))
        }
        for version in ["2026-09-29", "2026-09-20", "2026-12-01"] {
            XCTAssertFalse(Rules.summaryVisible(.take(orderId: "o"), version: version), version)
        }
    }

    // MARK: - where and what

    func testTheStreetIsEverythingBeforeTheCityAndThePostcode() {
        XCTAssertEqual(Rules.streetLine("A 1, Praha, 14000"), "A 1")
        XCTAssertEqual(Rules.streetLine("Na Příkopě 12, dvůr, Praha, 11000"), "Na Příkopě 12, dvůr")
        XCTAssertEqual(Rules.streetLine("Praha, 11000"), "Praha, 11000")
        XCTAssertNil(Rules.streetLine("  "))
        XCTAssertNil(Rules.streetLine(nil))
    }

    func testExtrasTakeTheirShortNamesInAFixedOrder() {
        let line = Rules.extrasLine([
            "laundry-ironing",
            "zz-new-extra",
            "inside-fridge",
            "pet-hair-supplement",
            "inside-oven",
            "interior-windows",
            "aa-new-extra"
        ])
        let expected = "+ " + [
            L10n.Orders.extraShortOven, L10n.Orders.extraShortFridge, L10n.Orders.extraShortInteriorWindows,
            L10n.Orders.extraShortIroning, OrderExtras.name("pet-hair-supplement"), OrderExtras.name("aa-new-extra"),
            OrderExtras.name("zz-new-extra")
        ].joined(separator: ", ")
        XCTAssertEqual(line, expected)
        XCTAssertEqual(OrderExtras.name("zz-new-extra"), "Zz new extra", "an unknown slug reads as words")
        XCTAssertNil(Rules.extrasLine([]))
    }

    func testTheRowsExtrasMapKeepsOnlyTrueInTheFixedOrder() {
        let slugs = Rules.extraSlugs(["interior-windows": true, "inside-oven": true, "inside-fridge": false])
        XCTAssertEqual(slugs, ["inside-oven", "interior-windows"])
        XCTAssertEqual(Rules.extraSlugs(nil), [])
    }

    func testTheScopeLineLeadsWithThePackageThenRoomsBathsAndExtras() {
        var row = job("s", at(9, 0), minutes: 120)
        row.selectedPackages = [PackageListItem(id: "p", name: "Deep clean")]
        row.selectedServices = [ServiceListItem(id: "s", name: "Standard clean")]
        row.rooms = 2
        row.bathrooms = 1
        row.extras = ["inside-oven": true]
        let expected = ["Deep clean", L10n.Orders.rooms(2), L10n.Orders.baths(1), "+ " + L10n.Orders.extraShortOven]
        XCTAssertEqual(Rules.scopeLine(row, locale: english), expected.joined(separator: Rules.dot))
        row.selectedPackages = nil
        XCTAssertEqual(Rules.leadName(row, locale: english), "Standard clean")
    }

    func testEachDistanceBandHasItsTextAndNoBandHasNone() {
        let expected: [HomeDistanceBand: String] = [
            ._1: L10n.Orders.distanceBand(._1),
            ._2: L10n.Orders.distanceBand(._2),
            ._3: L10n.Orders.distanceBand(._3),
            ._4: L10n.Orders.distanceBand(._4),
            ._5: L10n.Orders.distanceBand(._5)
        ]
        for band in HomeDistanceBand.allCases {
            XCTAssertEqual(Rules.bandText(band), expected[band], "\(band)")
        }
        XCTAssertEqual(Rules.bandText(._2), "3–6\u{00A0}km from home")
        XCTAssertNil(Rules.bandText(nil))
    }

    func testDurations() {
        XCTAssertEqual(Rules.durationText(45), L10n.Orders.durationMin(45))
        XCTAssertEqual(Rules.durationText(120), L10n.Orders.durationH(2))
        XCTAssertEqual(Rules.durationText(150), L10n.Orders.durationHMin(2, 30))
        XCTAssertEqual(L10n.Orders.durationHMin(2, 30), "2\u{00A0}h 30\u{00A0}min")
    }

    // MARK: - team and money

    func testTheTeamFlagCountsTakenSeatsNeverNames() {
        var row = job("t", at(9, 0), minutes: 240)
        row.requiredEmployees = 2
        row.assignedEmployeesCount = 0
        row.assignedEmployees = ["seat-1", "seat-2"]
        XCTAssertEqual(Rules.teamFlag(row), "\(L10n.Orders.crewSize(2)) · \(L10n.Orders.crewNobody)")
        row.assignedEmployeesCount = 1
        XCTAssertEqual(Rules.teamFlag(row), "\(L10n.Orders.crewSize(2)) · \(L10n.Orders.crewTaken(1))")
        XCTAssertEqual(Rules.heroTeam(row), L10n.Orders.heroTeam(2))
        XCTAssertNotEqual(Rules.heroTeam(row), L10n.Orders.crewSize(2))
        row.requiredEmployees = 1
        XCTAssertNil(Rules.teamFlag(row))
        XCTAssertNil(Rules.heroTeam(row))
    }

    func testANullPayIsHiddenNotZero() {
        var row = job("p", at(9, 0), minutes: 60)
        row.estimatedCleanerPay = nil
        XCTAssertNil(Rules.pay(row))
        row.estimatedCleanerPay = 780
        XCTAssertEqual(Rules.pay(row), OrdersFormat.money(780, symbol: nil))
    }

    func testTheCardFlagsComeInTheirOrder() {
        var row = job("f", at(9, 0), minutes: 240, status: ._4)
        row.paymentType = Code(value: 1)
        row.amountDueOnCard = 1290
        row.requiredEmployees = 2
        row.assignedEmployeesCount = 1
        row.dirtinessLevel = ._2
        let clash = job("own", at(10, 0), minutes: 60)
        let flags = Rules.flags(row, clash: clash, now: at(10, 0), locale: english)
        XCTAssertEqual(flags.count, 4, "under way sits beside the window, never among the flags")
        XCTAssertEqual(flags.first, .overlap(L10n.Orders.flagOverlap("10:00–11:00")))
        XCTAssertEqual(flags[1], .cash(L10n.Orders.flagCashCollect(OrdersFormat.money(1290, symbol: nil))))
        XCTAssertEqual(flags[3], .dirt(L10n.Orders.dirtinessLevel(._2) ?? ""))
        row.dirtinessLevel = ._1
        let increased = Rules.flags(row, clash: nil, now: at(8, 0), locale: english).last
        XCTAssertEqual(increased, .dirt(L10n.Orders.dirtinessLevel(._1) ?? ""))
        row.dirtinessLevel = ._0
        row.paymentType = Code(value: 0)
        row.orderStatus = Code(value: 0)
        XCTAssertEqual(Rules.flags(row, clash: nil, now: at(8, 0), locale: english).count, 1, "team only")
    }

    func testOnlyMoneyIsAmber() {
        XCTAssertTrue(Rules.Flag.cash("Cash: collect 1 240 Kč for the company").isAmber)
        XCTAssertFalse(Rules.Flag.dirt("Increased dirtiness").isAmber, "dirt keeps its icon and words, in neutral ink")
        XCTAssertFalse(Rules.Flag.overlap("Overlaps your 10:00–11:00 job").isAmber, "the overlap stays red")
        XCTAssertFalse(Rules.Flag.team("2 cleaners needed").isAmber)
        XCTAssertFalse(Rules.Flag.takeUntil("Take until 12:00").isAmber)
    }

    /// A blank symbol takes its currency code's, as Android's `symbolOf` does: "1 240 Kč", never "1 240".
    func testABlankSymbolFallsBackToItsCurrencysOwn() throws {
        let symbol = try XCTUnwrap(EarningsFormat.currencySymbol("CZK"))
        var row = job("c", at(9, 0), minutes: 60)
        row.estimatedCleanerPay = 1240
        row.paymentType = Code(value: 1)
        row.amountDueOnCard = 1390
        for blank in [nil, "", "  "] {
            row.currency = CurrencyListItem(code: "CZK", symbol: blank)
            let said = blank.debugDescription
            XCTAssertEqual(Rules.pay(row), OrdersFormat.money(1240, symbol: symbol), said)
            XCTAssertEqual(Rules.cashAmount(row), Rules.nonBreaking(OrdersFormat.money(1390, symbol: symbol)), said)
            XCTAssertEqual(OrdersFormat.totalEarnings([row]), OrdersFormat.money(1240, symbol: symbol), said)
            XCTAssertEqual(OrderDetail(row: row, mine: true)?.currencySymbol, symbol, said)
            var item = OrderItem.wireComplete()
            item.currency = CurrencyDetailDto(code: "CZK", symbol: blank)
            XCTAssertEqual(try OrderDetail(item).currencySymbol, symbol, said)
        }
        row.currency = CurrencyListItem(code: "EUR", symbol: "€")
        XCTAssertEqual(Rules.pay(row), OrdersFormat.money(1240, symbol: "€"), "the server's own symbol leads")
        row.currency = nil
        XCTAssertEqual(Rules.pay(row), OrdersFormat.money(1240, symbol: nil), "no currency, no unit")
    }

    /// The reader's in-app language picks the decimal mark, never the phone's: the switch leaves `Locale.current`
    /// on the device's language, so Czech on an English phone printed "746.93 Kč".
    func testEveryFigureTakesItsMarkFromTheAppsLanguage() {
        let restore = CoreL10n.languageTag
        defer { CoreL10n.apply(languageTag: restore) }
        var row = job("c", at(8, 30), minutes: 7)
        row.estimatedCleanerPay = 746.93
        row.currency = CurrencyListItem(code: "CZK", symbol: "Kč")
        row.paymentType = Code(value: 1)
        row.amountDueOnCard = 1419.16
        for (language, mark) in [("cs", ","), ("en", ".")] {
            CoreL10n.apply(languageTag: language)
            XCTAssertEqual(Rules.pay(row), "746\(mark)93 Kč", language)
            XCTAssertEqual(Rules.cashAmount(row), "1\u{202F}419\(mark)16\u{00A0}Kč", language)
            XCTAssertEqual(Rules.historyTotals([row]).pay, "746\(mark)93 Kč", language)
            XCTAssertEqual(
                OrdersFormat.money(833.18, symbol: "Kč"),
                "833\(mark)18 Kč",
                "the detail's and the contract's"
            )
        }
    }

    func testTheCashPanelIsTheFirstHiddenRowToTheCent() {
        let czech = Locale(identifier: "cs_CZ")
        let rows = [
            CashHeld(currencyCode: "EUR", amount: 50, floatCap: 100, cashJobsHidden: false),
            CashHeld(currencyCode: "CZK", amount: 3000.40, floatCap: 3000, cashJobsHidden: true)
        ]
        let panel = Rules.cashPanel(rows, locale: czech)
        let symbol = EarningsFormat.currencySymbol("CZK")
        XCTAssertEqual(panel?.held, Rules.nonBreaking(OrdersFormat.money(3000.40, symbol: symbol, locale: czech)))
        XCTAssertEqual(panel?.limit, Rules.nonBreaking(OrdersFormat.money(3000, symbol: symbol, locale: czech)))
        XCTAssertFalse(panel?.held.contains(" ") ?? true, "the amount never breaks from its currency")
        XCTAssertEqual(OrdersFormat.money(3000.40, symbol: "Kč", locale: czech), "3\u{202F}000,40 Kč")
    }

    func testHistoryTotalsArePerCurrencyWithRoundedBookedHours() {
        var first = job("a", at(9, 0), minutes: 150)
        first.estimatedCleanerPay = 1250
        first.currency = CurrencyListItem(code: "CZK", symbol: "Kč")
        var second = job("b", at(14, 0), minutes: 100)
        second.estimatedCleanerPay = 760
        second.currency = CurrencyListItem(code: "CZK", symbol: "Kč")
        let totals = Rules.historyTotals([first, second])
        XCTAssertEqual(totals.hours, 4, "250 minutes round to 4 h")
        XCTAssertEqual(totals.pay, OrdersFormat.totalEarnings([first, second]))
        XCTAssertEqual(Rules.historyTotals([]).hours, 0)
    }

    /// §8 "empty: 0 Kč": a period with no rows borrows the currency the board knows from elsewhere.
    func testAnEmptyPeriodsTotalCarriesTheKnownCurrency() {
        XCTAssertEqual(Rules.historyTotals([], emptySymbol: "Kč").pay, OrdersFormat.money(0, symbol: "Kč"))
        XCTAssertEqual(Rules.historyTotals([]).pay, OrdersFormat.money(0, symbol: nil), "no currency known anywhere")
    }

    // MARK: - lists

    /// The badge and *See N open jobs*: the server's total less the loaded rows whose slot has ended.
    func testTheOpenCountLeavesOutLoadedRowsWhoseSlotHasEnded() {
        let now = at(12, 0)
        let ended = job("e", at(9, 0), minutes: 60, status: ._0)
        let running = job("r", at(11, 30), minutes: 60, status: ._0)
        let later = job("l", at(14, 0), minutes: 60, status: ._0)
        XCTAssertEqual(Rules.openCount(total: 1, rows: [ended], now: now), 0, "never 1 above No open jobs right now")
        XCTAssertNil(Rules.badge(Rules.openCount(total: 1, rows: [ended], now: now)), "hidden at 0")
        XCTAssertEqual(
            Rules.openCount(total: 137, rows: [ended, running, later], now: now),
            136,
            "the pages not loaded still count"
        )
        XCTAssertEqual(Rules.openCount(total: 0, rows: [ended], now: now), 0, "never below zero")
        XCTAssertNil(Rules.openCount(total: nil, rows: [ended], now: now), "unknown before the server answers")
    }

    func testTheBadgeHidesZeroAndCapsAt99() {
        XCTAssertNil(Rules.badge(nil))
        XCTAssertNil(Rules.badge(0))
        XCTAssertEqual(Rules.badge(6), "6")
        XCTAssertEqual(Rules.badge(99), "99")
        XCTAssertEqual(Rules.badge(100), "99+")
    }

    func testBestPayIsOfferedOnlyOverEveryRowAndSortsLocally() {
        XCTAssertTrue(Rules.bestPayOffered(loaded: 37, total: 37))
        XCTAssertFalse(Rules.bestPayOffered(loaded: 100, total: 137))
        XCTAssertFalse(Rules.bestPayOffered(loaded: 0, total: nil))
        var high = job("high", at(12, 0), minutes: 60)
        high.estimatedCleanerPay = 900
        var tieLate = job("tie-late", at(15, 0), minutes: 60)
        tieLate.estimatedCleanerPay = 500
        var tieEarly = job("tie-early", at(9, 0), minutes: 60)
        tieEarly.estimatedCleanerPay = 500
        var none = job("none", at(8, 0), minutes: 60)
        none.estimatedCleanerPay = nil
        let sorted = Rules.sortedByPay([none, tieLate, high, tieEarly]).map(\.id)
        XCTAssertEqual(sorted, ["high", "tie-early", "tie-late", "none"])
    }

    func testAPageAppendsOnceAndPagingStopsAtTheTotalOrAnEmptyPage() {
        let merged = Rules.appendPage([job("a", nil), job("b", nil)], [job("b", nil), job("c", nil)])
        XCTAssertEqual(merged.map(\.id), ["a", "b", "c"])
        XCTAssertTrue(Rules.pagesComplete(loaded: 137, total: 137, lastPageCount: 37))
        XCTAssertTrue(Rules.pagesComplete(loaded: 100, total: 137, lastPageCount: 0))
        XCTAssertFalse(Rules.pagesComplete(loaded: 100, total: 137, lastPageCount: 100))
    }

    func testReservedCardsJoinTheirRowsInDeadlineOrderAndDropExpiredOffers() {
        let now = at(10, 0)
        let offers = [
            PendingOfferItem.sample(id: "late", respondByUtc: at(16, 0)),
            PendingOfferItem.sample(id: "expired", respondByUtc: at(9, 0)),
            PendingOfferItem.sample(id: "soon", respondByUtc: at(12, 0))
        ]
        let cards = Rules.reservedCards(offers, rows: [job("late", at(18, 0))], now: now)
        XCTAssertEqual(cards.map(\.offer.id), ["soon", "late"])
        XCTAssertNil(cards.first?.row, "an off-page offer has no row yet")
        XCTAssertEqual(cards.last?.row?.id, "late")
    }

    func testTheStaleTimeCarriesTheDayOnlyWhenItWasNotToday() {
        XCTAssertEqual(Rules.staleWhen(at(10, 14), now: at(12, 0), locale: english), "10:14")
        let yesterday = at(10, 14, dayOffset: -1)
        XCTAssertEqual(
            Rules.staleWhen(yesterday, now: at(12, 0), locale: english),
            OrdersFormat.shortDayTime(yesterday, locale: english)
        )
    }

    // MARK: - tile

    func testTheTileMonthIsTheStandaloneAbbreviation() {
        let russian = Locale(identifier: "ru")
        XCTAssertEqual(OrdersDateTile.month(date(2026, 5, 9), locale: russian), "май")
        XCTAssertEqual(OrdersDateTile.month(date(2026, 3, 9), locale: russian), "март")
        XCTAssertEqual(OrdersDateTile.month(date(2026, 10, 31), locale: Locale(identifier: "uk")), "жовт.")
        XCTAssertEqual(OrdersDateTile.month(date(2026, 10, 31), locale: english), "Oct")
        XCTAssertEqual(OrdersDateTile.day(date(2026, 10, 31), locale: english), "31")
        XCTAssertEqual(OrdersDateTile.weekday(date(2026, 10, 31), locale: english), "Sat")
    }

    // MARK: - helpers

    private func at(_ hour: Int, _ minute: Int, dayOffset: Int = 0) -> Date {
        let base = date(2026, 10, 31)
        let calendar = Calendar.current
        let day = calendar.date(byAdding: .day, value: dayOffset, to: base) ?? base
        return calendar.date(bySettingHour: hour, minute: minute, second: 0, of: day) ?? day
    }

    private func date(_ year: Int, _ month: Int, _ day: Int) -> Date {
        Calendar.current.date(from: DateComponents(year: year, month: month, day: day, hour: 12)) ?? Date()
    }

    private func job(_ id: String, _ start: Date?, minutes: Int? = 60, status: OrderStatus = ._2) -> OrderListItem {
        var row = OrderListItem.sample(id: id, status: status, cleaningDateTime: start)
        row.estimatedTime = minutes
        return row
    }
}

/// The day line's marks, as every tab and the detail draw them.
final class OrdersFigureAndMarkTests: XCTestCase {
    func testAShortMarkIsTwelvePointsCentredOnItsSpanAndKeptOnTheTrack() {
        let seven = OrdersDayLine.mark(from: 100, to: 103, track: 300)
        XCTAssertEqual(seven.width, 12, "a 7-minute job is still a mark")
        XCTAssertEqual(seven.x + seven.width / 2, 101.5, accuracy: 0.001, "centred on its span")
        let first = OrdersDayLine.mark(from: 0, to: 2, track: 300)
        XCTAssertEqual(first.x, 0, "kept inside the track's start")
        XCTAssertEqual(first.width, 12)
        let last = OrdersDayLine.mark(from: 298, to: 300, track: 300)
        XCTAssertEqual(last.x + last.width, 300, accuracy: 0.001, "kept inside the track's end")
        XCTAssertEqual(last.width, 12)
        let twelve = OrdersDayLine.mark(from: 40, to: 54, track: 300)
        XCTAssertEqual(twelve.x, 41, "a mark already 12 wide is unchanged")
        XCTAssertEqual(twelve.width, 12)
        let long = OrdersDayLine.mark(from: 40, to: 120, track: 300)
        XCTAssertEqual(long.x, 41, "a longer span keeps its box")
        XCTAssertEqual(long.width, 78)
    }
}

final class OrdersScopeTieTests: XCTestCase {
    /// A scope's last item ("+ fridge") never starts a line alone: the item before it waits for a line it shares
    /// with the last one, unless the two together fit no line.
    func testATiedFlowNeverStartsALineWithItsLastItemAlone() {
        let lines = OrdersSeparatorFlow.lines
        XCTAssertEqual(lines([50, 40, 30], 10, 110, false), [[0, 1], [2]])
        XCTAssertEqual(lines([50, 40, 30], 10, 110, true), [[0], [1, 2]])
        XCTAssertEqual(lines([150, 50, 45, 60], 14, 280, true), [[0, 1], [2, 3]])
        XCTAssertEqual(lines([30, 30, 30], 5, 100, true), [[0, 1, 2]])
        XCTAssertEqual(lines([50, 60, 70], 10, 130, true), [[0, 1], [2]], "a pair wider than a line breaks as before")
        XCTAssertEqual(lines([100, 100], 10, 150, true), [[0], [1]])
        XCTAssertEqual(lines([200], 10, 100, true), [[0]])
        XCTAssertEqual(lines([], 10, 100, true), [])
    }

    /// Every flow that draws a job's scope ties its last item; the layout reads it.
    func testEveryScopeLineTiesItsLastItemToTheOneBeforeIt() throws {
        let tied = [
            ("AvailableJobCard.swift", "let parts = OrdersBoardRules.scopeParts(row, locale: locale)"),
            ("ActiveHero.swift", "OrdersBoardRules.scopeParts(row, locale: locale)"),
            ("WorkContractFacts.swift", "if let scope {"),
            ("JobHead.swift", "private var subLine"),
            ("CleaningChecklistView.swift", "OrdersDotFlow(items: labels"),
            ("JobRunSheet.swift", "private func quiet(")
        ]
        for (file, anchor) in tied {
            let call = try XCTUnwrap(Self.call("OrdersDotFlow(", in: Self.source(file), after: anchor), "\(file)")
            XCTAssertTrue(call.contains("tiesLast: true") || call.contains("tiesLast: tiesLast"), "\(file): \(call)")
        }
        let flow = try Self.source("OrdersListComponents.swift")
        XCTAssertTrue(flow.contains("OrdersSeparatorFlow(tiesLast: tiesLast)"))
        let arranged = try XCTUnwrap(Self.call("Self.lines(", in: flow, after: "private func arrange"))
        XCTAssertTrue(arranged.contains("tiesLast: tiesLast"), arranged)
    }

    /// An own job's row ties the last two words of its street and of its service, so "Kompletní úklid domácnosti"
    /// never leaves "domácnosti" alone on a line.
    func testAnOwnJobRowTiesTheLastWordsOfEachItem() throws {
        let source = try Self.source("OrdersListContent.swift")
        let row = try XCTUnwrap(Self.call("OrdersDotFlow(", in: source, after: "struct OrdersJobRow"))
        XCTAssertTrue(row.contains("Text(L10n.tie($0))"), row)
    }

    private static func source(_ file: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent("Sources/Features/Orders/\(file)"), encoding: .utf8)
    }

    private static func call(_ name: String, in source: String, after anchor: String) -> String? {
        guard let from = source.range(of: anchor)?.lowerBound,
              let start = source.range(of: name, range: from ..< source.endIndex)?.lowerBound
        else { return nil }
        var depth = 0
        var index = start
        while index < source.endIndex {
            if source[index] == "(" { depth += 1 }
            if source[index] == ")" {
                depth -= 1
                if depth == 0 { return String(source[start ... index]) }
            }
            index = source.index(after: index)
        }
        return nil
    }
}

extension OrdersBoardRulesTests {
    /// A crew flag breaks between its two facts, never inside one: "2 cleaners needed" over "1 already on it", each
    /// fact's last two words held. A flag of one fact stays one run.
    func testAFlagBreaksOnlyBetweenItsFacts() throws {
        let crew = try XCTUnwrap(Rules.teamWords(required: 2, taken: 1))
        XCTAssertEqual(
            Rules.flagParts(crew),
            [L10n.tie(L10n.Orders.crewSize(2)), L10n.tie(L10n.Orders.crewTaken(1))]
        )
        let cash = L10n.Orders.cashFlag(Rules.nonBreaking("1 240 Kč"), collected: false)
        XCTAssertEqual(Rules.flagParts(cash), [L10n.tie(cash)])
        let overlap = L10n.Orders.flagOverlap("14:00–16:00")
        XCTAssertEqual(Rules.flagParts(overlap), [L10n.tie(Rules.unbreakable(overlap))])
    }
}
