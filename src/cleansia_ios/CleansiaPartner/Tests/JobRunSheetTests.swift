import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The job detail's rules on the design's own sample (Thursday 12 November 2026): A1 14:00–16:00 solo by card, A2
/// 15:00–18:00 on Friday with Lenka, A3 08:00–10:00 on Monday in cash.
final class JobRunSheetTests: XCTestCase {
    private let english = Locale(identifier: "en")

    // MARK: - The run sheet

    func testConfirmedOpensSettingOffAndSaysWhenTheRestComes() throws {
        let steps = try sheet(a1(status: 2), now: at(9, 20))

        XCTAssertEqual(steps.map(\.kind), [.setOff, .getIn, .clean, .finish])
        XCTAssertEqual(steps.map(\.mark), [.current, .toCome, .toCome, .toCome])
        XCTAssertEqual(
            steps.map(\.time),
            [L10n.Orders.runFrom("13:00"), L10n.Orders.runAt("14:00"), "14:00–16:00", L10n.Orders.runBy("16:00")]
        )
        XCTAssertEqual(try sheet(a1(status: 2), now: at(14, 10))[0].time, L10n.Orders.heroDue("14:00"), "late")
        let far = try sheet(a3(status: 2), now: at(9, 20))[0].time
        XCTAssertEqual(
            far,
            L10n.Orders.runDayFrom(OrdersFormat.pattern("EEE", at(8, 0, day: 16), locale: english), "07:00")
        )
    }

    func testOnTheWayStampsSettingOffAndOpensGettingIn() throws {
        let steps = try sheet(a1(status: 3, history: [(3, at(13, 22))]), now: at(13, 40))

        XCTAssertEqual(steps.map(\.mark), [.done, .current, .toCome, .toCome])
        XCTAssertEqual(steps[0].time, "13:22")
        XCTAssertEqual(steps[0].spokenTime, L10n.Orders.stepDoneAt("13:22"))
        XCTAssertEqual(steps[1].time, L10n.Orders.runAt("14:00"))
    }

    /// The start is the earliest InProgress stamp, so a job bounced back and restarted keeps it.
    func testTheStartStampIsTheEarliestInProgressOne() throws {
        let item = a1(status: 4, history: [(3, at(13, 22)), (4, at(14, 5)), (4, at(13, 58))])
        let steps = try sheet(item, now: at(15, 12))

        XCTAssertEqual(steps.map(\.mark), [.done, .done, .current, .toCome])
        XCTAssertEqual(steps.map(\.time), ["13:22", "13:58", "14:00–16:00", L10n.Orders.runBy("16:00")])
    }

    func testASoloJobsCleaningClosesAtItsFirstAfterPhoto() throws {
        let item = a1(status: 4, history: [(3, at(13, 22)), (4, at(13, 58))], afterPhotos: true)
        let photos = [photo(._1, at(14, 1)), photo(._2, at(15, 47)), photo(._2, at(15, 46))]
        let steps = try sheet(item, photos: photos, now: at(15, 49))

        XCTAssertEqual(steps.map(\.mark), [.done, .done, .done, .current])
        XCTAssertEqual(steps[2].time, "15:46")
        XCTAssertEqual(steps[3].time, L10n.Orders.runBy("16:00"))
    }

    /// Lenka's mark never closes her Cleaning; her own does. The on-the-way and start stamps are the order's.
    func testACrewJobsCleaningClosesAtHerOwnMarkAndItsStampsAreTheJobs() throws {
        let crew = try sheet(
            a2(mine: nil, mate: at(17, 41, day: 13)),
            photos: [photo(._2, at(17, 38, day: 13))],
            now: at(17, 46, day: 13)
        )
        XCTAssertEqual(crew.map(\.mark), [.done, .done, .current, .toCome])
        XCTAssertEqual(crew[0].time, L10n.Orders.jobStamp("14:12"))
        XCTAssertEqual(crew[0].spokenTime, L10n.Orders.jobStampSpoken("14:12"))
        XCTAssertEqual(crew[1].time, L10n.Orders.jobStamp("15:02"))

        let waiting = try sheet(a2(mine: at(17, 44, day: 13), mate: nil), now: at(17, 46, day: 13))
        XCTAssertEqual(waiting.map(\.mark), [.done, .done, .done, .current])
        XCTAssertEqual(waiting[2].time, "17:44")
    }

    func testACompletedJobStampsEveryStep() throws {
        var item = a1(status: 5, history: [(3, at(13, 22)), (4, at(13, 58)), (5, at(15, 51))])
        item.completedAt = at(15, 51)
        let steps = try sheet(item, photos: [photo(._2, at(15, 46))], now: at(16, 20))

        XCTAssertEqual(steps.map(\.mark), [.done, .done, .done, .done])
        XCTAssertEqual(steps.map(\.time), ["13:22", "13:58", "15:46", "15:51"])
    }

    /// Past 24 hours the server strips every seat's mark, hers included: the crew job's Cleaning takes her own first
    /// after photo, or the check alone when she took none.
    func testAPastCrewJobsCleaningStampIsHerOwnAfterPhoto() throws {
        var item = a2(mine: nil, mate: nil)
        item.orderStatus = Code(value: 5)
        item.statusHistory?.append(OrderStatusTrackDto(status: Code(value: 5), createdOn: at(17, 52, day: 13)))
        let later = at(9, 20, day: 16)
        XCTAssertEqual(try sheet(item, photos: [photo(._2, at(17, 38, day: 13))], now: later)[2].time, "17:38")
        XCTAssertNil(try sheet(item, now: later)[2].time)
    }

    func testAJobNotHersOrCancelledHasNoRunSheet() throws {
        XCTAssertTrue(try sheet(a1(status: 2, mine: false), now: at(9, 20)).isEmpty)
        XCTAssertTrue(try sheet(a1(status: 6), now: at(9, 20)).isEmpty)
    }

    // MARK: - One status line

    /// The Active hero builds its line from the row and the job's detail; the head from the detail and its photos.
    func testTheHeadSaysWhatTheActiveHeroSaysAtTheSameMinute() throws {
        var row = OrderListItem.sample(id: "A1", status: ._4, cleaningDateTime: at(14, 0))
        row.estimatedTime = 120
        let detail = try OrderDetail(a1(status: 4, history: [(3, at(13, 22)), (4, at(13, 58))], afterPhotos: true))
        let photos = [photo(._2, at(15, 46))]
        for minute in [at(15, 12), at(15, 49), at(16, 10)] {
            let head = detail.runFacts(myEmployeeId: "emp-self", photos: photos)
                .flatMap { OrdersBoardRules.runStatus($0, now: minute, locale: english)?.items }
            let hero = OrdersBoardRules.runFacts(row, detail: detail, photos: photos, ownEmployeeId: "emp-self")
                .flatMap { OrdersBoardRules.runStatus($0, now: minute, locale: english)?.items }
            XCTAssertNotNil(head)
            XCTAssertEqual(head, hero, "\(minute)")
        }
    }

    /// The hero's photos did not load while its job says an after photo exists: the hero still names the step the
    /// detail names, because both read the job's facts the same way.
    func testTheHeroSaysWhatTheHeadSaysWhenItsPhotosDidNotLoad() throws {
        var row = OrderListItem.sample(id: "A1", status: ._4, cleaningDateTime: at(14, 0))
        row.estimatedTime = 120
        let detail = try OrderDetail(a1(status: 4, history: [(3, at(13, 22)), (4, at(13, 58))], afterPhotos: true))
        let head = detail.runFacts(myEmployeeId: "emp-self", photos: [])
            .flatMap { OrdersBoardRules.runStatus($0, now: at(15, 49), locale: english)?.items }
        let hero = OrdersBoardRules.runFacts(row, detail: detail, photos: [], ownEmployeeId: "emp-self")
            .flatMap { OrdersBoardRules.runStatus($0, now: at(15, 49), locale: english)?.items }

        XCTAssertEqual(head?.first, L10n.Orders.runStepFinish)
        XCTAssertEqual(hero, head)
    }

    func testTheHeadsFactsCarryTheFinishAndTheCancellation() throws {
        var done = a1(status: 5, history: [(5, at(15, 51))])
        done.completedAt = at(15, 51)
        XCTAssertEqual(try OrderDetail(done).runFacts(myEmployeeId: "emp-self", photos: [])?.finishedAt, at(15, 51))

        var cancelled = a1(status: 6, history: [(6, at(8, 10))])
        cancelled.systemCancellationReason = "order.cancelled.customer_lockout"
        let facts = try OrderDetail(cancelled).runFacts(myEmployeeId: "emp-self", photos: [])
        XCTAssertEqual(facts?.cancelledAt, at(8, 10))
        XCTAssertEqual(facts?.cancellationReason, "order.cancelled.customer_lockout")
    }

    // MARK: - Cash and the card

    /// The cash is the total less the credit, wherever it is said: the flag, the block, the button, both confirms.
    func testTheCashIsWhatTheCustomerStillOwes() throws {
        var item = a3(status: 4)
        item.totalPrice = 1490
        item.creditAppliedAmount = 250
        item.amountDueOnCard = 1240
        let detail = try OrderDetail(item)

        XCTAssertEqual(detail.cashDueLabel, OrdersFormat.money(1240, symbol: "Kč"))
        XCTAssertEqual(detail.cashNotPaidOwedLabel, OrdersFormat.money(1240, symbol: "Kč"))
    }

    func testOnlyACardStripeHasChargedCompletes() throws {
        for (status, charged) in [(2, true), (4, true), (6, true), (1, false), (3, false), (5, false), (nil, true)] {
            var item = a1(status: 4, afterPhotos: true)
            item.paymentStatus = status.map { Code(value: $0) }
            let detail = try OrderDetail(item)
            XCTAssertEqual(detail.payment.isCardNotCharged, !charged, "payment status \(String(describing: status))")
        }
        var cash = a3(status: 4)
        cash.paymentStatus = Code(value: 1)
        XCTAssertFalse(try OrderDetail(cash).payment.isCardNotCharged, "the cash gate is the cash's own")
    }

    /// The card not charged is said in the run sheet only where the locked payment slide is pinned: on a crew job
    /// once every part is marked, under the after photo and before the crew's line. Not while a part is open.
    @MainActor
    func testTheCrewGateSaysThePaymentIsNotConfirmedOnlyOnceEveryPartIsMarked() async throws {
        let line = HostedSpeech.plain(L10n.Orders.paymentNotConfirmed)
        func gate(mine: Date?, mate: Date?, status: Int = 1) async throws -> [String] {
            var item = a2(mine: mine, mate: mate)
            item.paymentType = Code(value: 2)
            item.paymentStatus = Code(value: status)
            return try await HostedSpeech.labels(of: CrewGate(
                order: OrderDetail(item),
                myEmployeeId: "emp-self",
                afterPhoto: at(17, 38, day: 13)
            ))
        }
        let done = try await gate(mine: at(17, 44, day: 13), mate: at(17, 41, day: 13))
        let said = try XCTUnwrap(done.firstIndex(of: line), "\(done)")
        let photo = try XCTUnwrap(done.firstIndex { $0.hasPrefix(HostedSpeech.plain(L10n.Orders.finishAfterPhoto)) })
        let crew = try XCTUnwrap(done.firstIndex(of: HostedSpeech.plain(L10n.tie(L10n.Orders.crewAllMarked))))
        XCTAssertLessThan(photo, said)
        XCTAssertLessThan(said, crew)

        let partOpen = try await gate(mine: nil, mate: at(17, 41, day: 13))
        XCTAssertFalse(partOpen.contains(line), "Cleaning, her part open: the move is I'm done")
        let waiting = try await gate(mine: at(17, 44, day: 13), mate: nil)
        XCTAssertFalse(waiting.contains(line), "the crew-mate's part open: the move is Call")
        let charged = try await gate(mine: at(17, 44, day: 13), mate: at(17, 41, day: 13), status: 2)
        XCTAssertFalse(charged.contains(line))
    }

    // MARK: - Access, lockout, crew, the details closing, withdrawing

    func testEachAccessPieceIsLeftOutWhenTheServerSentNone() throws {
        var item = a1(status: 2)
        XCTAssertEqual(try OrderDetail(item).accessFacts, [
            L10n.Orders.accessMode("at_home"), L10n.Orders.floor("3"), L10n.Orders.flat("12")
        ].compactMap { $0 })
        item.accessMode = nil
        XCTAssertEqual(try OrderDetail(item).accessFacts, [L10n.Orders.floor("3"), L10n.Orders.flat("12")])
        item.customerFloor = nil
        XCTAssertEqual(try OrderDetail(item).accessFacts, [L10n.Orders.flat("12")])
        item.customerApartment = " "
        XCTAssertEqual(try OrderDetail(item).accessFacts, [])
        item.accessMode = "teleport"
        XCTAssertEqual(try OrderDetail(item).accessFacts, [], "a mode this build does not know says nothing")
    }

    func testFloorFlatAndModeFromConfirmedTheAccessTextFromOnTheWay() throws {
        for status in [2, 3, 4] {
            XCTAssertTrue(try OrderDetail(a1(status: status)).showsAccessFacts, "\(status)")
        }
        for status in [0, 5, 6] {
            XCTAssertFalse(try OrderDetail(a1(status: status)).showsAccessFacts, "\(status)")
        }
        XCTAssertFalse(try OrderDetail(a1(status: 2, mine: false)).showsAccessFacts)
        XCTAssertFalse(try OrderDetail(a1(status: 2)).showsAccessCard)
        XCTAssertTrue(try OrderDetail(a1(status: 3)).showsAccessCard)
        XCTAssertTrue(try OrderDetail(a1(status: 4)).showsAccessCard)
    }

    func testTheLockoutLineShowsOnConfirmedOnlyOnceTheStartHasPassed() throws {
        let confirmed = try OrderDetail(a1(status: 2))
        XCTAssertFalse(confirmed.showsLockoutLine(now: at(13, 10)))
        XCTAssertTrue(confirmed.showsLockoutLine(now: at(14, 10)))
        XCTAssertTrue(try OrderDetail(a1(status: 3)).showsLockoutLine(now: at(13, 40)))
        XCTAssertFalse(try OrderDetail(a1(status: 4)).showsLockoutLine(now: at(15, 0)))
    }

    func testTheLateJoinerReadsOneNamedMateElseTheCount() throws {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 4)
        item.requiredEmployees = 2
        item.availableSpots = 1
        item.hasAvailableSpots = true
        item.assignedEmployees = [AssignedEmployeeDto(id: "s1", employeeId: "emp-marta", fullName: "Marta")]
        XCTAssertEqual(try OrderDetail(item).onItLine, L10n.Orders.mateOnIt("Marta"))
        item.assignedEmployees = [AssignedEmployeeDto(id: "s1", employeeId: "emp-marta", fullName: " ")]
        XCTAssertEqual(try OrderDetail(item).onItLine, L10n.Orders.crewTaken(1))
        item.requiredEmployees = 3
        item.assignedEmployees = [
            AssignedEmployeeDto(id: "s1", employeeId: "emp-marta", fullName: "Marta"),
            AssignedEmployeeDto(id: "s2", employeeId: "emp-petr", fullName: "Petr")
        ]
        XCTAssertEqual(try OrderDetail(item).onItLine, L10n.Orders.crewTaken(2))
    }

    /// Once her part is marked the job waits for the first crew-mate whose part is open; she is the one Call
    /// reaches, and nobody is waited for while her own part is open or once everyone has marked.
    func testTheJobWaitsForTheCrewMateWhosePartIsOpen() throws {
        let waiting = try OrderDetail(a2(mine: at(17, 44, day: 13), mate: nil))
        XCTAssertEqual(waiting.openMate("emp-self")?.givenName, "Lenka")
        XCTAssertEqual(waiting.openMate("emp-self")?.phone, "+420 605 318 772")
        XCTAssertNil(try OrderDetail(a2(mine: nil, mate: nil)).openMate("emp-self"), "her part is open")
        XCTAssertNil(try OrderDetail(a2(mine: at(17, 44, day: 13), mate: at(17, 41, day: 13))).openMate("emp-self"))
    }

    /// A crew-mate's phone is offered for Call only on a job in progress that is hers, and only while the mate has
    /// not marked her part: nowhere else does the number leave the server's answer.
    func testCallReachesAMateOnlyOnHerJobInProgressWhileTheMatesPartIsOpen() throws {
        let lenka = { (detail: OrderDetail) in try XCTUnwrap(detail.seats.first { $0.employeeId == "emp-lenka" }) }
        let open = try OrderDetail(a2(mine: nil, mate: nil))
        XCTAssertEqual(try open.callPhone(for: lenka(open)), "+420 605 318 772")

        let marked = try OrderDetail(a2(mine: nil, mate: at(17, 41, day: 13)))
        XCTAssertNil(try marked.callPhone(for: lenka(marked)), "she has marked her part")
        for status in [2, 3, 5, 6] {
            var item = a2(mine: nil, mate: nil)
            item.orderStatus = Code(value: status)
            let detail = try OrderDetail(item)
            XCTAssertNil(try detail.callPhone(for: lenka(detail)), "status \(status)")
        }
        var notHers = a2(mine: nil, mate: nil)
        notHers.isAssignedToCurrentUser = false
        let browsed = try OrderDetail(notHers)
        XCTAssertNil(try browsed.callPhone(for: lenka(browsed)), "a job she has not taken")
        for status in [OrderStatus._0, ._2, ._3, ._5, ._6] {
            XCTAssertNotEqual(
                OrderPrimaryAction.action(
                    for: status, isMine: true, hasAfterPhotos: true, isCashPayment: false, isPaymentSettled: true,
                    crew: .waitingForCrew
                ),
                .awaitingCrew
            )
        }
        XCTAssertNotEqual(
            OrderPrimaryAction.action(
                for: ._4, isMine: false, hasAfterPhotos: true, isCashPayment: false, isPaymentSettled: true,
                crew: .waitingForCrew
            ),
            .awaitingCrew
        )
    }

    /// Cleaning's step on the tracker fills with the booked window as it goes, and stays full past its end.
    func testTheCleaningStepFillsWithTheBookedWindow() {
        let facts = OrdersBoardRules.RunFacts(status: ._4, start: at(14, 0), end: at(16, 0))
        let fill = { (now: Date) -> Double? in
            guard case let .steps(step: 3, fraction: fraction, done: false) = JobHeadFacts.progress(
                runFacts: facts,
                isMine: true,
                now: now
            ) else { return -1 }
            return fraction
        }
        XCTAssertEqual(fill(at(15, 12)) ?? -1, 0.6, accuracy: 0.001)
        XCTAssertEqual(fill(at(16, 10)), 1)
    }

    func testTheCrewMateIsNamedByTheirGivenName() {
        let lenka = OrderSeat(id: "s", employeeId: "e", fullName: "Lenka Procházková", phone: "+420 605 318 772")
        XCTAssertEqual(lenka.givenName, "Lenka")
        XCTAssertEqual(lenka.initials, "LP")
        XCTAssertNil(OrderSeat(id: "s", employeeId: "e", fullName: "  ").givenName)
    }

    func testACancelledJobsPayLineFollowsItsReason() throws {
        var item = a1(status: 6)
        XCTAssertEqual(try OrderDetail(item).cancelledPayLine.text, L10n.WorkContract.summaryLateCancel)
        item.systemCancellationReason = "order.cancelled.customer_lockout"
        XCTAssertEqual(try OrderDetail(item).cancelledPayLine.text, L10n.Orders.cancelLockoutPaid)
        XCTAssertTrue(try OrderDetail(item).cancelledPayLine.bold)
        for reason in [
            "order.cancelled.recurring_not_confirmed", "order.cancelled.payment_not_completed",
            "order.cancelled.no_cleaner_available", "order.cancelled.company_wind_down"
        ] {
            item.systemCancellationReason = reason
            XCTAssertEqual(try OrderDetail(item).cancelledPayLine.text, L10n.Orders.cancelNoFee, reason)
            XCTAssertFalse(try OrderDetail(item).cancelledPayLine.bold, reason)
        }
        item.systemCancellationReason = "order.cancelled.unknown"
        XCTAssertEqual(try OrderDetail(item).cancelledPayLine.text, L10n.WorkContract.summaryLateCancel)
    }

    func testTheCustomersDetailsCloseTomorrowThenToday() throws {
        var item = a1(status: 5)
        item.completedAt = at(15, 51)
        item.address = OrderAddress(street: "Vinohradská 46", city: "Praha", zipCode: "12000")
        let detail = try OrderDetail(item)
        XCTAssertEqual(
            detail.detailsCloseLine(now: at(16, 20), locale: english),
            L10n.Orders.detailsCloseTomorrow("15:51")
        )
        XCTAssertEqual(
            detail.detailsCloseLine(now: at(23, 59), locale: english),
            L10n.Orders.detailsCloseTomorrow("15:51")
        )
        XCTAssertEqual(
            detail.detailsCloseLine(now: at(0, 1, day: 13), locale: english),
            L10n.Orders.detailsCloseToday("15:51")
        )
        item.address = nil
        XCTAssertNil(
            try OrderDetail(item).detailsCloseLine(now: at(16, 20), locale: english),
            "past 24 h the details are gone"
        )
    }

    func testWithdrawingIsPossibleUntilTheStart() throws {
        XCTAssertEqual(
            try OrderDetail(a1(status: 2)).dropUntil(now: at(9, 20), locale: english),
            L10n.Orders.dropPossibleUntil("14:00")
        )
        let monday = OrdersFormat.pattern("EEE", at(8, 0, day: 16), locale: english)
        XCTAssertEqual(
            try OrderDetail(a3(status: 2)).dropUntil(now: at(9, 20), locale: english),
            L10n.Orders.dropPossibleUntil("\(monday) 08:00")
        )
    }

    // MARK: - Fixtures

    private func sheet(_ item: OrderItem, photos: [OrderPhoto] = [], now: Date) throws -> [RunSheetStep] {
        try OrderDetail(item).runSheet(myEmployeeId: "emp-self", photos: photos, now: now, locale: english)
    }
}

private func at(_ hour: Int, _ minute: Int, day: Int = 12) -> Date {
    Calendar.current.date(from: DateComponents(year: 2026, month: 11, day: day, hour: hour, minute: minute)) ?? Date()
}

private func photo(_ type: PhotoType, _ captured: Date) -> OrderPhoto {
    OrderPhoto(id: UUID().uuidString, photoType: type, blobUrl: nil, capturedAt: captured)
}

private func a1(
    status: Int,
    mine: Bool = true,
    history: [(Int, Date)] = [],
    afterPhotos: Bool = false
) -> OrderItem {
    var item = OrderItem.wireComplete()
    item.id = "A1"
    item.orderStatus = Code(value: status)
    item.cleaningDateTime = at(14, 0)
    item.estimatedTime = 120
    item.isAssignedToCurrentUser = mine
    item.hasAfterPhotos = afterPhotos
    item.paymentType = Code(value: 2)
    item.paymentStatus = Code(value: 2)
    item.customerFloor = "3"
    item.customerApartment = "12"
    item.accessMode = "at_home"
    item.accessInstructions = "Budu doma."
    item.assignedEmployees = mine ? [AssignedEmployeeDto(id: "seat-me", employeeId: "emp-self")] : []
    item.statusHistory = history.map { OrderStatusTrackDto(status: Code(value: $0.0), createdOn: $0.1) }
    return item
}

private func a2(mine: Date?, mate: Date?) -> OrderItem {
    var item = OrderItem.wireComplete()
    item.id = "A2"
    item.orderStatus = Code(value: 4)
    item.cleaningDateTime = at(15, 0, day: 13)
    item.estimatedTime = 180
    item.isAssignedToCurrentUser = true
    item.hasAfterPhotos = true
    item.requiredEmployees = 2
    item.availableSpots = 0
    item.hasAvailableSpots = false
    item.assignedEmployees = [
        AssignedEmployeeDto(
            id: "seat-lenka",
            employeeId: "emp-lenka",
            fullName: "Lenka Procházková",
            phoneNumber: "+420 605 318 772",
            completionConfirmedAt: mate
        ),
        AssignedEmployeeDto(
            id: "seat-me",
            employeeId: "emp-self",
            fullName: "Oksana Melnyk",
            completionConfirmedAt: mine
        )
    ]
    item.statusHistory = [
        OrderStatusTrackDto(status: Code(value: 3), createdOn: at(14, 12, day: 13)),
        OrderStatusTrackDto(status: Code(value: 4), createdOn: at(15, 2, day: 13))
    ]
    return item
}

private func a3(status: Int) -> OrderItem {
    var item = OrderItem.wireComplete()
    item.id = "A3"
    item.orderStatus = Code(value: status)
    item.cleaningDateTime = at(8, 0, day: 16)
    item.estimatedTime = 120
    item.isAssignedToCurrentUser = true
    item.paymentType = Code(value: 1)
    item.paymentStatus = Code(value: 1)
    item.amountDueOnCard = 1240
    item.totalPrice = 1240
    item.creditAppliedAmount = 0
    item.currency = CurrencyDetailDto(code: "CZK", symbol: "Kč")
    item.assignedEmployees = [AssignedEmployeeDto(id: "seat-me", employeeId: "emp-self")]
    return item
}

/// The detail's words, in every shipped language: each new key in all five built tables with the slots English
/// has, none left in English, and every key that fell out gone.
final class DetailStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private static let added = [
        "cancel_lockout_paid", "cancel_no_fee", "run_job_stamp", "run_job_stamp_a11y", "late_already_there",
        "crew_all_marked", "detail_waiting_for", "detail_your_job_that_day", "detail_mate_on_it",
        "detail_same_pay_late", "run_a11y", "run_step_done_at", "run_from", "run_day_from", "run_at", "run_by",
        "access_mode_at_home", "access_mode_keys_handover", "access_mode_door_code", "access_mode_reception",
        "detail_floor", "detail_flat", "access_shown_on_set_off", "lockout_paid_in_full", "checklist_phone_only",
        "photos_add", "photos_after_needed", "issue_not_resolved", "crew_you", "crew_not_yet", "crew_done_at",
        "finish_needs_photo", "finish_needs_crew", "finish_before_complete", "finish_after_photo",
        "finish_cash_collected", "crew_both_parts", "cash_rule", "cash_mark_amount", "crew_mark_done_sub",
        "take_after_photo", "drop_possible_until", "detail_shown_after_take", "detail_hidden_address",
        "detail_hidden_contact", "detail_hidden_notes", "details_close_tomorrow", "details_close_today",
        "day_line_free", "detail_later_today", "detail_also_open_that_day", "detail_open_today",
        "detail_error_rest", "detail_error_rest_body", "detail_error_job", "detail_gone", "detail_off_schedule",
        "order_detail_error_message", "loading", "order_removal_lead", "dash_browse_jobs", "tracker_step_counter",
        "scope_section_title", "customer", "property", "contract_accepted_label", "contract_version_label"
    ]
    private static let removed = [
        "tracker_countdown_starts_in", "tracker_subtitle_on_the_way_arriving",
        "tracker_completed_in", "order_estimate_remaining", "order_estimate_overrun", "notify_on_the_way",
        "status_timeline_section_title", "map_approximate_area", "dirtiness_level_normal",
        "payment_method_value", "payment_method_card", "order_crew_waiting", "checklist_all_done_hint",
        "work_contract_accepted_line", "swipe_to_start", "tracker_headline_in_progress", "order_removal_message",
        "cash_collect_label", "contract_accepted_on", "contract_version_lc", "detail_job_number"
    ]

    func testEveryDetailStringIsWrittenInAllFiveLanguagesWithItsSlots() throws {
        let english = try table("en")
        for language in ["en", "cs", "sk", "uk", "ru"] {
            let strings = try table(language)
            for key in Self.added {
                let value = try XCTUnwrap(strings[key], "\(key) missing from \(language).lproj")
                XCTAssertFalse(value.trimmingCharacters(in: .whitespaces).isEmpty, "\(language) \(key)")
                XCTAssertEqual(slots(value), slots(english[key] ?? ""), "\(language) \(key)")
                if language != "en" {
                    XCTAssertNotEqual(value, english[key], "\(language).lproj left \(key) in English")
                }
            }
            for key in Self.removed {
                XCTAssertNil(strings[key], "\(key) still ships in \(language)")
            }
        }
    }

    /// The withdraw act has one wording: English renames the action and its confirmation; the other
    /// languages already said withdraw and keep their words.
    func testWithdrawingIsCalledWithdrawingInEnglish() throws {
        let english = try table("en")
        XCTAssertEqual(english["order_drop_action"], "Withdraw from this job")
        XCTAssertEqual(english["order_drop_confirm_title"], "Withdraw from this job?")
        XCTAssertEqual(english["order_drop_confirm_action"], "Yes, withdraw")
        XCTAssertEqual(try table("cs")["order_drop_confirm_title"], "Odstoupit od této zakázky?")
        XCTAssertEqual(english["take_order"], "Take this job")
    }

    private func slots(_ value: String) -> [String] {
        let pattern = try? NSRegularExpression(pattern: "%(\\d+\\$)?[@a-zA-Z]")
        return pattern?.matches(in: value, range: NSRange(value.startIndex..., in: value))
            .compactMap { Range($0.range, in: value).map { String(value[$0]) } }.sorted() ?? []
    }

    private func table(_ language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"), language)
        let url = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(NSDictionary(contentsOf: url) as? [String: String], language)
    }
}
