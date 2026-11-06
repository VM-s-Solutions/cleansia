import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The working day's pure rules: the day line, the day panels, the week strip, the status line, the finding,
/// History's cash and month, the cash panel, the separator flow and the tie. Thursday 12 November 2026, 09:20,
/// in the device's zone, as the board shows it.
final class OrdersWorkingDayTests: XCTestCase {
    private typealias Rules = OrdersBoardRules
    private let english = Locale(identifier: "en")
    private let calendar = Calendar.current

    // MARK: - The day line

    func testTheDayLineRunsSevenToNineteenAndWidensToHoldEveryBlock() {
        XCTAssertTrue(range(Rules.dayRange([])) == (420, 1140), "the default 07:00–19:00")
        XCTAssertTrue(range(Rules.dayRange([(480, 720)])) == (420, 1140))
        XCTAssertTrue(range(Rules.dayRange([(1185, 1305)])) == (420, 1320), "a 19:45 start with 2 h reaches 22:00")
        XCTAssertTrue(range(Rules.dayRange([(405, 465), (465, 585)])) == (360, 1140), "a 06:45 set-off band")
        XCTAssertTrue(range(Rules.dayRange([(1185, 1425)])) == (420, 1440), "23:45 rounds to 24:00")
        let page = Rules.dayRange([(480, 600), (1185, 1305), (405, 465)])
        XCTAssertTrue(range(page) == (360, 1320), "one range over every day line of the page")
    }

    func testTheHourLabelsAreEvenAndThinOutPastFourteenHours() {
        XCTAssertEqual(Rules.dayLineHours((420, 1140)), [8, 10, 12, 14, 16, 18])
        XCTAssertEqual(Rules.dayLineHours((360, 1320)), [9, 12, 15, 18, 21])
        XCTAssertEqual(Rules.dayLineHours((420, 1260)), [8, 10, 12, 14, 16, 18, 20])
    }

    func testAJobsSpanIsItsMinutesOnItsOwnDay() {
        let span = Rules.minuteSpan(start: at(14, 0), end: at(16, 0), calendar: calendar)
        XCTAssertEqual(span.start, 840)
        XCTAssertEqual(span.end, 960)
        let late = Rules.minuteSpan(start: at(23, 0), end: at(1, 0, day: 13), calendar: calendar)
        XCTAssertEqual(late.end, 1500, "past midnight counts on")
    }

    func testRowsGroupIntoTheirDaysInOrder() {
        let rows = [
            job("a", at(8, 0), 240),
            job("b", at(14, 0), 120),
            job("c", at(8, 0, day: 13), 120),
            job("d", nil, 120),
            job("e", at(9, 0, day: 14), 120)
        ]
        let groups = Rules.dayGroups(rows, calendar: calendar)
        XCTAssertEqual(groups.map { $0.rows.compactMap(\.id) }, [["a", "b"], ["c"], ["e"]])
        XCTAssertEqual(groups.map(\.day), [12, 13, 14].map { calendar.startOfDay(for: at(9, 0, day: $0)) })
    }

    func testTheClashSpanIsWhereTheOpenJobMeetsTheOwnOne() {
        let own = job("A2", at(15, 0, day: 13), 180, status: ._2)
        let span = Rules.clashSpan(job("V4", at(14, 0, day: 13), 120), own: [own])
        XCTAssertEqual(span?.start, at(15, 0, day: 13))
        XCTAssertEqual(span?.end, at(16, 0, day: 13))
        XCTAssertNil(Rules.clashSpan(job("touch", at(13, 0, day: 13), 120), own: [own]))
    }

    // MARK: - The day header and the finding

    func testTheDayHeaderNamesHerJobsInStartOrder() {
        let one = [job("A2", at(15, 0, day: 13), 180, status: ._2)]
        XCTAssertEqual(
            Rules.dayHeaderLabel(one, short: false, locale: english),
            L10n.Orders.dayYourJob("15:00–18:00", count: 1)
        )
        XCTAssertEqual(L10n.Orders.dayYourJob("15:00–18:00", count: 1), "your job 15:00–18:00")
        let two = [job("late", at(14, 0), 120, status: ._2), job("early", at(8, 0), 120, status: ._2)]
        XCTAssertEqual(
            Rules.dayHeaderLabel(two, short: false, locale: english),
            "your jobs 08:00–10:00, 14:00–16:00"
        )
        XCTAssertEqual(Rules.dayHeaderLabel(two, short: true, locale: english), "yours 08:00–10:00, 14:00–16:00")
        XCTAssertNil(Rules.dayHeaderLabel([], short: false, locale: english))
    }

    func testTheFindingNamesTheDayAndTheJobItHitsOrCountsSeveralDays() {
        let own = [job("A2", at(15, 0, day: 13), 180, status: ._2), job("A3", at(8, 0, day: 16), 120, status: ._2)]
        let tomorrow = job("V4", at(14, 0, day: 13), 120)
        XCTAssertEqual(
            Rules.overlapFinding([tomorrow, job("V3", at(8, 0, day: 13), 120)], own: own, now: now, locale: english),
            L10n.Orders.overlapDay(1, day: L10n.Orders.dayTomorrow, window: "15:00–18:00")
        )
        XCTAssertEqual(
            L10n.Orders.overlapDay(1, day: L10n.Orders.dayTomorrow, window: "15:00–18:00"),
            "Tomorrow: 1 job overlaps your 15:00–18:00 job"
        )
        let monday = job("V9", at(9, 0, day: 16), 120)
        XCTAssertEqual(
            Rules.overlapFinding([tomorrow, monday], own: own, now: now, locale: english),
            L10n.Orders.overlapCount(2)
        )
        XCTAssertNil(Rules.overlapFinding([job("free", at(8, 0, day: 14), 120)], own: own, now: now, locale: english))
    }

    // MARK: - The week strip

    func testTheWeekStripAddsUpToTheLoadedOpenRowsOfTheNextSevenDays() {
        let open = [
            job("V1", at(8, 0), 240),
            job("V3", at(8, 0, day: 13), 120), job("V4", at(14, 0, day: 13), 120),
            job("V5", at(9, 0, day: 14), 210), job("R1", at(10, 0, day: 14), 120),
            job("V6", at(11, 0, day: 16), 180), job("V7", at(14, 0, day: 16), 120),
            job("V8", at(10, 0, day: 17), 120), job("V9", at(15, 0, day: 17), 120),
            job("later", at(9, 0, day: 19), 120)
        ]
        let own = [job("A1", at(14, 0), 120, status: ._4)]
        let cells = Rules.weekStrip(now: now, open: open, own: own, calendar: calendar)
        XCTAssertEqual(cells.count, 7)
        XCTAssertEqual(cells.map(\.open), [1, 2, 2, 0, 2, 2, 0], "a real zero stays a zero")
        XCTAssertEqual(
            cells.compactMap(\.open).reduce(0, +),
            9,
            "the loaded rows inside the seven days, never the badge"
        )
        XCTAssertEqual(cells.map(\.mine), [true, false, false, false, false, false, false])
        XCTAssertEqual(cells.first?.open, 1, "her day keeps its open count")
        XCTAssertEqual(cells.first?.day, calendar.startOfDay(for: now))
        XCTAssertEqual(cells.last?.day, calendar.startOfDay(for: at(9, 0, day: 18)))
    }

    func testTheWeekStripCountsNothingUntilTheOpenJobsHaveAnswered() {
        let own = [job("A1", at(14, 0), 120, status: ._4)]
        let waiting = Rules.weekStrip(now: now, open: nil, own: own, calendar: calendar)
        XCTAssertEqual(waiting.map(\.open), Array(repeating: nil, count: 7), "no count before the open jobs answer")
        XCTAssertEqual(waiting.map(\.mine), [true, false, false, false, false, false, false], "her days still show")
        let none = Rules.weekStrip(now: now, open: [], own: own, calendar: calendar)
        XCTAssertEqual(none.map(\.open), Array(repeating: 0, count: 7), "once they answer, a real zero is printed")
        let thursday = calendar.startOfDay(for: now)
        XCTAssertEqual(
            Rules.weekCellLabel(day: thursday, open: nil, mine: true, locale: english),
            "\(OrdersFormat.weekdayLongDay(thursday, locale: english)), \(L10n.Orders.weekLegendYourJob)"
        )
    }

    func testAWeekCellSpeaksItsDateHerJobAndItsCount() {
        let thursday = calendar.startOfDay(for: now)
        let date = OrdersFormat.weekdayLongDay(thursday, locale: english)
        XCTAssertEqual(
            Rules.weekCellLabel(day: thursday, open: 0, mine: true, locale: english),
            "\(date), \(L10n.Orders.weekLegendYourJob), \(L10n.Orders.weekOpenCount(0))"
        )
        XCTAssertEqual(
            Rules.weekCellLabel(day: thursday, open: 2, mine: false, locale: english),
            "\(date), \(L10n.Orders.weekOpenCount(2))"
        )
        XCTAssertEqual(L10n.Orders.weekOpenCount(0), "0 open jobs")
        XCTAssertEqual(L10n.Orders.weekOpenCount(1), "1 open job")
    }

    // MARK: - History's month

    func testTheMonthGridStartsOnMondayAndRingsToday() throws {
        let november = try XCTUnwrap(Rules.historyGridRange(.thisMonth, now: now, calendar: calendar))
        XCTAssertEqual(november.from, calendar.startOfDay(for: at(9, 0, day: 1)))
        XCTAssertEqual(november.through, calendar.startOfDay(for: at(9, 0, day: 15)), "the month stops at this week")
        let rows = [job("H9", at(13, 0, day: 11), 180, status: ._5), job("H8", at(8, 0, day: 11), 120, status: ._5)]
        let grid = Rules.monthGrid(
            from: november.from,
            through: november.through,
            rows: rows,
            now: now,
            calendar: calendar
        )
        XCTAssertEqual(grid.count, 21)
        XCTAssertEqual(try calendar.component(.weekday, from: XCTUnwrap(grid.first?.day)), 2, "a Monday")
        XCTAssertEqual(try calendar.component(.day, from: XCTUnwrap(grid.first?.day)), 26, "26 October")
        XCTAssertEqual(grid.first?.inPeriod, false)
        XCTAssertEqual(grid.first { calendar.component(.day, from: $0.day) == 11 }?.jobs, 2)
        XCTAssertEqual(grid.filter(\.isToday).count, 1)
        XCTAssertEqual(grid.first(where: \.isToday)?.day, calendar.startOfDay(for: now))
        XCTAssertEqual(grid.first { calendar.component(.day, from: $0.day) == 13 }?.isFuture, true)

        let first = at(9, 20, day: 1)
        let empty = try XCTUnwrap(Rules.historyGridRange(.thisMonth, now: first, calendar: calendar))
        XCTAssertEqual(
            Rules.monthGrid(from: empty.from, through: empty.through, rows: [], now: first, calendar: calendar).count,
            7,
            "on Sunday the 1st, one week"
        )
        let october = try XCTUnwrap(Rules.historyGridRange(.lastMonth, now: now, calendar: calendar))
        XCTAssertEqual(calendar.component(.day, from: october.through), 31)
    }

    /// One pip per completed job, as many whole pips as the cell is wide: the grid never widens or spills.
    func testTheMonthGridDrawsAPipPerJobAsFarAsTheCellHolds() {
        XCTAssertEqual(Rules.pipsThatFit(0, width: 45), 0)
        XCTAssertEqual(Rules.pipsThatFit(1, width: 45), 1)
        XCTAssertEqual(Rules.pipsThatFit(5, width: 45), 5)
        XCTAssertEqual(Rules.pipsThatFit(6, width: 45), 6)
        XCTAssertEqual(Rules.pipsThatFit(9, width: 45), 6, "six 5-pt pips 3 pt apart fill 45 pt")
        XCTAssertEqual(Rules.pipsThatFit(9, width: 44), 5)
    }

    /// The take sheet's status line says how far off the day is, never the weekday, which is the calendar line's; a
    /// job already under way says so.
    func testTheTakeSheetSaysHowFarOffTheDayIs() {
        XCTAssertEqual(Rules.howFarOff(at(14, 0), live: false, now: now, locale: english), [L10n.Orders.dayToday])
        XCTAssertEqual(
            Rules.howFarOff(at(14, 0, day: 13), live: false, now: now, locale: english),
            [L10n.Orders.dayTomorrow]
        )
        XCTAssertEqual(Rules.howFarOff(at(9, 0, day: 14), live: false, now: now, locale: english), ["In 2\u{00A0}days"])
        XCTAssertEqual(
            Rules.howFarOff(at(8, 0), live: true, now: now, locale: english),
            [L10n.Orders.dayToday, L10n.Orders.underWayShort]
        )
    }

    func testHistoryTotalsKeepTheirHalerOnlyWhenNotWhole() {
        var first = job("a", at(8, 0, day: 2), 120, status: ._5)
        first.estimatedCleanerPay = 776.1
        first.currency = CurrencyListItem(code: "CZK", symbol: "Kč")
        var second = job("b", at(8, 0, day: 3), 240, status: ._5)
        second.estimatedCleanerPay = 762
        second.currency = first.currency
        let totals = Rules.historyTotals([first, second])
        XCTAssertEqual(totals.pay, OrdersFormat.money(1538.1, symbol: "Kč"))
        XCTAssertEqual(totals.hours, 6)
        XCTAssertEqual(OrdersFormat.money(762, symbol: "Kč", locale: english), "762 Kč")
        XCTAssertEqual(OrdersFormat.money(749.5, symbol: "Kč", locale: english), "749.50 Kč")
    }

    // MARK: - Lines that wrap

    func testTheSeparatorFlowNeverStartsOrEndsALineWithADot() {
        XCTAssertEqual(OrdersSeparatorFlow.lines(widths: [50, 60, 70], separator: 10, maxWidth: 125), [[0, 1], [2]])
        XCTAssertEqual(OrdersSeparatorFlow.lines(widths: [50, 60, 70], separator: 10, maxWidth: 300), [[0, 1, 2]])
        XCTAssertEqual(OrdersSeparatorFlow.lines(widths: [200, 30], separator: 10, maxWidth: 120), [[0], [1]])
        XCTAssertEqual(OrdersSeparatorFlow.lines(widths: [], separator: 10, maxWidth: 120), [])
    }

    func testTheLastTwoWordsAreTiedOnlyInATextOfThreeOrMore() {
        XCTAssertEqual(L10n.tie("Pull down to check again."), "Pull down to check\u{00A0}again.")
        XCTAssertEqual(L10n.tie("Check again"), "Check again")
        XCTAssertEqual(L10n.tie("Retry"), "Retry")
        XCTAssertEqual(L10n.tie("12:00 · 13:00"), "12:00 · 13:00", "never across a separator")
    }
}

/// The one status line the Active hero and the detail head share, what the cash says on each tab, and the strings.
final class OrdersRunStatusTests: XCTestCase {
    private typealias Rules = OrdersBoardRules
    private let english = Locale(identifier: "en")
    private let calendar = Calendar.current

    // MARK: - The status line

    func testSettingOffSaysWhenSheCanGo() {
        let facts = ownRun(._2)
        XCTAssertEqual(line(facts, at(9, 0, day: 11)), [L10n.Orders.runStepSetOff, L10n.Orders.dayTomorrow])
        XCTAssertEqual(
            line(ownRun(._2, start: at(8, 0, day: 16)), now),
            [L10n.Orders.runStepSetOff, L10n.Orders.inDays(4)]
        )
        XCTAssertEqual(line(facts, now), [L10n.Orders.runStepSetOff, L10n.Orders.heroIn("3\u{00A0}h 40\u{00A0}min")])
        XCTAssertEqual(line(facts, at(13, 10)), [L10n.Orders.runStepSetOff, L10n.Orders.setOffNow])
        XCTAssertEqual(line(facts, at(14, 10)), [L10n.Orders.runStepSetOff, L10n.Orders.heroDue("14:00")])
        XCTAssertEqual(Rules.runStep(facts), .setOff)
    }

    /// The locked slide's second line, one rule for the hero and the detail: the countdown when setting off opens
    /// today, else that day by its weekday and date.
    func testTheLockedSlideCountsDownOnItsDayAndNamesAnyOtherDay() {
        XCTAssertEqual(
            Rules.setOffWhen(at(13, 0), now: now, locale: english),
            L10n.Orders.heroIn("3\u{00A0}h 40\u{00A0}min")
        )
        for day in [13, 16] {
            let opensAt = at(13, 0, day: day)
            XCTAssertEqual(
                Rules.setOffWhen(opensAt, now: now, locale: english),
                "\(OrdersFormat.wideWeekday(opensAt, locale: english))\(Rules.dot)"
                    + OrdersFormat.dayMonth(opensAt, locale: english)
            )
        }
        XCTAssertTrue(Rules.setOffWhen(at(13, 0, day: 16), now: now, locale: english).hasPrefix("Monday"))
    }

    func testGettingInSaysTheWayThereThenTheDueTimeThenTheReport() {
        XCTAssertEqual(
            line(ownRun(._3), at(13, 40)),
            [L10n.Orders.statusLabel(._3), L10n.Orders.heroIn("20\u{00A0}min")]
        )
        XCTAssertEqual(line(ownRun(._3), at(14, 17)), [L10n.Orders.runStepGetIn, L10n.Orders.heroDue("14:00")])
        var reported = ownRun(._3)
        reported.lockoutReportedAt = at(14, 19)
        XCTAssertEqual(line(reported, at(14, 25)), [L10n.Orders.waitingCompany, L10n.Orders.reportedAt("14:19")])
        XCTAssertEqual(Rules.runStep(reported), .getIn)
    }

    func testCleaningAndFinishingCountDownToTheBookedEnd() {
        let cleaning = ownRun(._4)
        XCTAssertEqual(line(cleaning, at(15, 12)), [L10n.Orders.runStepClean, L10n.Orders.left("48\u{00A0}min")])
        XCTAssertEqual(
            line(cleaning, at(14, 30)),
            [L10n.Orders.runStepClean, L10n.Orders.left("1\u{00A0}h 30\u{00A0}min")]
        )
        XCTAssertEqual(line(cleaning, at(16, 10)), [L10n.Orders.runStepClean, L10n.Orders.heroUntil("16:00")])
        var photo = cleaning
        photo.hasAfterPhoto = true
        XCTAssertEqual(Rules.runStep(photo), .finish, "a solo job's Cleaning closes at its first after photo")
        XCTAssertEqual(line(photo, at(15, 49)), [L10n.Orders.runStepFinish, L10n.Orders.left("11\u{00A0}min")])
    }

    func testACrewMatesMarkNeverMovesHerToFinishing() {
        var crew = ownRun(._4)
        crew.crew = true
        crew.hasAfterPhoto = true
        XCTAssertEqual(Rules.runStep(crew), .clean, "her part is open: an after photo does not close a crew job")
        crew.ownMark = at(17, 44)
        XCTAssertEqual(Rules.runStep(crew), .finish)

        var row = job("A2", at(15, 0, day: 13), 180, status: ._4)
        row.requiredEmployees = 2
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 4)
        item.isAssignedToCurrentUser = true
        item.cleaningDateTime = at(15, 0, day: 13)
        item.estimatedTime = 180
        item.assignedEmployees = [
            AssignedEmployeeDto(id: "seat-lenka", employeeId: "emp-lenka", completionConfirmedAt: at(17, 41, day: 13)),
            AssignedEmployeeDto(id: "seat-me", employeeId: "emp-self")
        ]
        let detail = try? OrderDetail(item)
        let mateOnly = Rules.runFacts(row, detail: detail, photos: [], ownEmployeeId: "emp-self")
        XCTAssertEqual(mateOnly.flatMap(Rules.runStep), .clean, "Lenka marked, she has not")
        item.assignedEmployees?[1] = AssignedEmployeeDto(
            id: "seat-me",
            employeeId: "emp-self",
            completionConfirmedAt: at(17, 44, day: 13)
        )
        let both = Rules.runFacts(row, detail: try? OrderDetail(item), photos: [], ownEmployeeId: "emp-self")
        XCTAssertEqual(both?.ownMark, at(17, 44, day: 13))
        XCTAssertEqual(both.flatMap(Rules.runStep), .finish)
    }

    func testASoloJobsAfterPhotoComesFromItsPhotos() {
        let row = job("A1", at(14, 0), 120, status: ._4)
        let before = OrderPhoto(id: "p1", photoType: ._1, blobUrl: nil)
        let after = OrderPhoto(id: "p2", photoType: ._2, blobUrl: nil)
        XCTAssertEqual(Rules.runFacts(row, photos: [before]).flatMap(Rules.runStep), .clean)
        XCTAssertEqual(Rules.runFacts(row, photos: [before, after]).flatMap(Rules.runStep), .finish)
        XCTAssertNil(Rules.runFacts(job("open", at(14, 0), 120)), "a job not taken has no run")
    }

    func testDoneAndCancelledReadInInk() {
        var done = ownRun(._5)
        done.finishedAt = at(15, 51)
        let status = Rules.runStatus(done, now: at(16, 20), locale: english, calendar: calendar)
        XCTAssertEqual(status?.items, [L10n.Orders.trackerHeadlineDone, L10n.Orders.finishedAt("15:51")])
        XCTAssertEqual(status?.isInk, true)
        done.finishedAt = at(10, 52, day: 10)
        XCTAssertEqual(
            line(done, now),
            [
                L10n.Orders.trackerHeadlineDone,
                L10n.Orders.finishedAt(OrdersFormat.shortDayTime(at(10, 52, day: 10), locale: english))
            ]
        )

        var cancelled = ownRun(._6, start: at(11, 0))
        cancelled.cancelledAt = at(8, 10)
        XCTAssertEqual(line(cancelled, now), [L10n.Orders.cancelledAt("08:10")], "a null reason stays neutral")
        cancelled.cancellationReason = "order.cancelled.customer_lockout"
        XCTAssertEqual(
            line(cancelled, now),
            [L10n.Orders.cancelledAt("08:10"), L10n.Orders.cancellationReason("order.cancelled.customer_lockout")]
        )
        XCTAssertEqual(L10n.Orders.cancellationReason("order.cancelled.customer_lockout"), "nobody could let you in")
        cancelled.cancellationReason = "order.cancelled.something_else"
        XCTAssertEqual(line(cancelled, now), [L10n.Orders.cancelledAt("08:10")])
        XCTAssertEqual(Rules.runStatus(cancelled, now: now, locale: english)?.isInk, true)
        XCTAssertNil(Rules.runStep(ownRun(._0)))
    }

    // MARK: - Cash

    func testCashSaysItIsTheCompanysOnEveryTab() {
        var cash = job("A3", at(8, 0, day: 16), 120, status: ._2)
        cash.paymentType = Code(value: 1)
        cash.amountDueOnCard = 1240
        XCTAssertEqual(
            Rules.cashFlag(cash),
            L10n.Orders.flagCashCollect(Rules.nonBreaking(OrdersFormat.money(1240, symbol: nil)))
        )
        XCTAssertNil(Rules.cashFlag(job("card", at(8, 0), 120)))
    }

    /// Marked collected (payment Paid) before the job is complete, the hero's flag no longer asks her to collect it.
    func testCashMarkedCollectedSaysCollected() {
        var cash = job("A3", at(8, 0, day: 16), 120, status: ._4)
        cash.paymentType = Code(value: 1)
        cash.amountDueOnCard = 1240
        cash.paymentStatus = Code(value: 2)
        let amount = Rules.nonBreaking(OrdersFormat.money(1240, symbol: nil))
        XCTAssertEqual(Rules.cashFlag(cash), L10n.Orders.historyCashCollected(amount))
        cash.paymentStatus = Code(value: 1)
        XCTAssertEqual(Rules.cashFlag(cash), L10n.Orders.flagCashCollect(amount))
    }

    func testHistorySaysWhatHappenedToTheCashByThePaymentStatus() {
        var row = job("H4", at(10, 0, day: 5), 120, status: ._5)
        row.paymentType = Code(value: 1)
        row.amountDueOnCard = 1240
        let amount = Rules.nonBreaking(OrdersFormat.money(1240, symbol: nil))
        row.paymentStatus = Code(value: 2)
        XCTAssertEqual(Rules.historyCash(row)?.items, [L10n.Orders.historyCashCollected(amount)])
        XCTAssertEqual(Rules.historyCash(row)?.collected, true)
        row.paymentStatus = Code(value: 1)
        XCTAssertEqual(
            Rules.historyCash(row)?.items,
            [L10n.Orders.historyCashNotPaid(amount), L10n.Orders.historyPaidInFull]
        )
        XCTAssertEqual(Rules.historyCash(row)?.collected, false)
        for other in [3, 4, 5, 6] {
            row.paymentStatus = Code(value: other)
            XCTAssertNil(Rules.historyCash(row), "status \(other) draws no cash line")
        }
        row.paymentStatus = Code(value: 2)
        row.paymentType = Code(value: 0)
        XCTAssertNil(Rules.historyCash(row), "a card job says nothing")
    }

    func testTheCashPanelFollowsTheBalanceAndTheCap() {
        XCTAssertNil(Rules.cashPanel([]), "no balance, no panel")
        let noCap = Rules.cashPanel([CashHeld(currencyCode: "CZK", amount: 1240, floatCap: nil, cashJobsHidden: false)])
        XCTAssertNotNil(noCap)
        XCTAssertNil(noCap?.limit, "no cap: the amount alone")
        let capped = Rules.cashPanel([CashHeld(
            currencyCode: "CZK",
            amount: 1240,
            floatCap: 3000,
            cashJobsHidden: false
        )])
        XCTAssertEqual(capped?.ratio ?? 0, 1240.0 / 3000, accuracy: 0.0001)
        XCTAssertEqual(capped?.over, false)
        XCTAssertNotNil(capped?.limit)
        let over = Rules.cashPanel([
            CashHeld(currencyCode: "EUR", amount: 20, floatCap: 100, cashJobsHidden: false),
            CashHeld(currencyCode: "CZK", amount: 3200.40, floatCap: 3000, cashJobsHidden: true)
        ], locale: english)
        XCTAssertEqual(over?.over, true, "the hidden currency leads")
        XCTAssertEqual(over?.ratio ?? 0, 3000 / 3200.40, accuracy: 0.0001, "over the limit the tick sits at the cap")
        XCTAssertFalse(over?.held.contains(" ") ?? true, "an amount never breaks from its currency")
    }

    // MARK: - Strings

    func testTheNewPluralsTakeEachLanguagesForms() throws {
        let restore = (L10n.bundle, CoreL10n.languageTag)
        defer {
            L10n.bundle = restore.0
            CoreL10n.apply(languageTag: restore.1)
        }
        let expected: [String: [Int: String]] = [
            "cs": [1: "1 volná zakázka", 2: "2 volné zakázky", 5: "5 volných zakázek"],
            "sk": [1: "1 voľná zákazka", 3: "3 voľné zákazky", 5: "5 voľných zákaziek"],
            "uk": [
                1: "1 вільне замовлення",
                2: "2 вільні замовлення",
                5: "5 вільних замовлень",
                21: "21 вільне замовлення"
            ],
            "ru": [1: "1 свободный заказ", 2: "2 свободных заказа", 5: "5 свободных заказов"]
        ]
        for (language, forms) in expected {
            try use(language)
            for (count, text) in forms {
                XCTAssertEqual(L10n.Orders.weekOpenCount(count), text, "\(language) \(count)")
            }
            XCTAssertTrue(L10n.Orders.seeOpen(2).contains("2"), language)
            XCTAssertTrue(L10n.Orders.inDays(4).contains("4"), language)
            XCTAssertTrue(L10n.Orders.overlapDay(2, day: "X", window: "Y").hasPrefix("X: 2"), language)
            XCTAssertNotEqual(
                L10n.Orders.dayYourJob("W", count: 1),
                L10n.Orders.dayYourJob("W", count: 2),
                language
            )
        }
        try use("cs")
        XCTAssertEqual(L10n.Orders.crewTaken(1), "1 místo obsazeno", "by seat, not by gender")
        XCTAssertEqual(L10n.Orders.crewTaken(3), "3 místa obsazena")
        try use("uk")
        XCTAssertEqual(L10n.Orders.crewTaken(5), "5 місць уже зайнято")
        try use("en")
        XCTAssertEqual(L10n.Orders.noActiveOrders, "No active jobs")
        XCTAssertEqual(L10n.Orders.yourShare, "Your share")
    }

    func testEveryNewBoardStringIsWrittenInAllFiveLanguages() throws {
        let keys = [
            "orders_day_your_job", "orders_day_your_jobs", "orders_day_yours_short", "orders_day_yours_short_many",
            "cash_hidden_back", "cash_limit_at", "orders_flag_under_way_short", "orders_flag_cash_collect",
            "available_checked_at", "orders_your_next_jobs", "cash_held_title",
            "cash_held_of_limit", "cash_held_hint", "active_empty_body", "active_nothing_else",
            "active_open_next_7_days", "active_week_legend_your_job", "active_soonest_open", "active_see_offer",
            "history_cash_collected", "history_cash_not_paid", "history_paid_in_full", "history_empty_month",
            "history_empty_week", "history_empty_body", "history_last_month", "history_show_period",
            "run_step_set_off", "run_step_get_in", "run_step_clean", "run_step_finish", "detail_left",
            "detail_finished_at", "detail_cancelled_at", "status_set_off_now", "detail_waiting_company",
            "detail_reported_at"
        ] + Array(L10n.Orders.cancellationReasons)
        let slots: [String: [String]] = [
            "cash_held_of_limit": ["%1$@", "%2$@"], "cash_hidden_back": ["%1$@"], "orders_flag_cash_collect": ["%1$@"],
            "history_cash_collected": ["%1$@"], "history_cash_not_paid": ["%1$@"], "detail_left": ["%1$@"]
        ]
        for language in ["en", "cs", "sk", "uk", "ru"] {
            let bundle = try localeBundle(language)
            for key in keys {
                let value = bundle.localizedString(forKey: key, value: "\u{0}", table: nil)
                XCTAssertNotEqual(value, "\u{0}", "\(key) missing in \(language)")
                XCTAssertFalse(value.isEmpty, "\(key) empty in \(language)")
                for slot in slots[key] ?? [] {
                    XCTAssertTrue(value.contains(slot), "\(language)/\(key) lost \(slot)")
                }
            }
            for gone in [
                "orders_flag_cash",
                "orders_flag_under_way",
                "orders_card_job_duration",
                "available_cash_hidden_notice"
            ] {
                XCTAssertEqual(
                    bundle.localizedString(forKey: gone, value: "\u{0}", table: nil),
                    "\u{0}",
                    "\(gone) in \(language)"
                )
            }
        }
    }
}

// MARK: - Helpers

private var now: Date {
    at(9, 20)
}

private func at(_ hour: Int, _ minute: Int, day: Int = 12) -> Date {
    Calendar.current.date(from: DateComponents(year: 2026, month: 11, day: day, hour: hour, minute: minute)) ?? Date()
}

private func job(_ id: String, _ start: Date?, _ minutes: Int, status: OrderStatus = ._0) -> OrderListItem {
    var row = OrderListItem.sample(id: id, status: status, cleaningDateTime: start)
    row.estimatedTime = minutes
    return row
}

private func ownRun(_ status: OrderStatus, start: Date? = nil) -> OrdersBoardRules.RunFacts {
    let begin = start ?? at(14, 0)
    return OrdersBoardRules.RunFacts(status: status, start: begin, end: begin.addingTimeInterval(7200))
}

private func line(_ facts: OrdersBoardRules.RunFacts, _ now: Date) -> [String] {
    OrdersBoardRules.runStatus(facts, now: now, locale: Locale(identifier: "en"), calendar: .current)?.items ?? []
}

private func range(_ value: (from: Int, to: Int)) -> (Int, Int) {
    (value.from, value.to)
}

private func use(_ language: String) throws {
    L10n.bundle = try localeBundle(language)
    CoreL10n.apply(languageTag: language)
}

private func localeBundle(_ language: String) throws -> Bundle {
    let path = [Bundle.main, Bundle(for: OrdersWorkingDayTests.self)].lazy.compactMap {
        $0.path(forResource: language, ofType: "lproj")
    }.first
    return try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
}
