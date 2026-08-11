import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

final class OrderStatusLogicTests: XCTestCase {
    func testSevenStateRawValuesMatchBackendInts() {
        // OrderEnums.kt: New=0, Pending=1, Confirmed=2, OnTheWay=3,
        // InProgress=4, Completed=5, Cancelled=6.
        XCTAssertEqual(OrderStatus._0.rawValue, 0)
        XCTAssertEqual(OrderStatus._1.rawValue, 1)
        XCTAssertEqual(OrderStatus._2.rawValue, 2)
        XCTAssertEqual(OrderStatus._3.rawValue, 3)
        XCTAssertEqual(OrderStatus._4.rawValue, 4)
        XCTAssertEqual(OrderStatus._5.rawValue, 5)
        XCTAssertEqual(OrderStatus._6.rawValue, 6)
    }

    func testCodeMapsToOrderStatusByValue() {
        let code = Code(type: "OrderStatus", name: "OnTheWay", value: 3)
        XCTAssertEqual(code.toOrderStatus(), ._3)
    }

    func testCodeWithNilValueMapsToNil() {
        XCTAssertNil(Code(type: nil, name: "OnTheWay", value: nil).toOrderStatus())
    }

    func testCodeWithUnknownValueMapsToNil() {
        XCTAssertNil(Code(type: nil, name: nil, value: 99).toOrderStatus())
    }

    func testActiveStatusesAreConfirmedOnTheWayInProgress() {
        XCTAssertTrue(OrderStatusGroup.isActive(._2))
        XCTAssertTrue(OrderStatusGroup.isActive(._3))
        XCTAssertTrue(OrderStatusGroup.isActive(._4))
        XCTAssertFalse(OrderStatusGroup.isActive(._0))
        XCTAssertFalse(OrderStatusGroup.isActive(._1))
        XCTAssertFalse(OrderStatusGroup.isActive(._5))
        XCTAssertFalse(OrderStatusGroup.isActive(._6))
    }

    /// ADR-0029 D2, and the backend's own `LiveActivityEventKeys.ForStatus`: a lock-screen card belongs to
    /// the service window alone. Confirmed can be days out — a card opened there says "your cleaner is
    /// heading over" and counts down to an appointment nobody has set off for, and burns the ~8h
    /// ActivityKit budget before the clean begins.
    func testOnlyTheTwoInServiceStatusesCarryALiveActivityCard() {
        XCTAssertEqual(OrderStatusGroup.liveActivityStatus(._3), "onTheWay")
        XCTAssertEqual(OrderStatusGroup.liveActivityStatus(._4), "inProgress")

        for status in [OrderStatus._0, ._1, ._2, ._5, ._6] {
            XCTAssertNil(OrderStatusGroup.liveActivityStatus(status), "\(status) opened a card")
        }
        XCTAssertNil(OrderStatusGroup.liveActivityStatus(nil))
    }

    func testUpcomingExcludesCompletedAndCancelled() {
        XCTAssertTrue(OrderStatusGroup.isUpcoming(._0))
        XCTAssertTrue(OrderStatusGroup.isUpcoming(._3))
        XCTAssertFalse(OrderStatusGroup.isUpcoming(._5))
        XCTAssertFalse(OrderStatusGroup.isUpcoming(._6))
        XCTAssertFalse(OrderStatusGroup.isUpcoming(nil))
    }

    func testCancellableIsNewPendingConfirmed() {
        XCTAssertTrue(OrderStatusGroup.isCancellable(._0))
        XCTAssertTrue(OrderStatusGroup.isCancellable(._1))
        XCTAssertTrue(OrderStatusGroup.isCancellable(._2))
        XCTAssertFalse(OrderStatusGroup.isCancellable(._3))
        XCTAssertFalse(OrderStatusGroup.isCancellable(._5))
    }

    /// The Report-issue gate, matching `OrderDetailScreen.kt`'s `canReportIssue`:
    /// there is nothing to dispute until a cleaner has taken the job (Confirmed),
    /// and a cancelled cleaning never happened.
    func testReportableIsConfirmedThroughCompleted() {
        XCTAssertTrue(OrderStatusGroup.isReportable(._2))
        XCTAssertTrue(OrderStatusGroup.isReportable(._3))
        XCTAssertTrue(OrderStatusGroup.isReportable(._4))
        XCTAssertTrue(OrderStatusGroup.isReportable(._5))
        XCTAssertFalse(OrderStatusGroup.isReportable(._0))
        XCTAssertFalse(OrderStatusGroup.isReportable(._1))
        XCTAssertFalse(OrderStatusGroup.isReportable(._6))
        XCTAssertFalse(OrderStatusGroup.isReportable(nil))
    }

    /// Confirmed is the one status that offers both footer actions, and Completed
    /// is reportable while no longer being cancellable — the two states that make
    /// a single-action footer wrong.
    func testConfirmedIsBothCancellableAndReportableWhileCompletedIsReportableOnly() {
        XCTAssertTrue(OrderStatusGroup.isCancellable(._2) && OrderStatusGroup.isReportable(._2))
        XCTAssertFalse(OrderStatusGroup.isCancellable(._5))
        XCTAssertTrue(OrderStatusGroup.isReportable(._5))
    }
}

/// The Completed-only footer CTAs (`canRebook` / `canMakeRecurring` in
/// `OrderDetailScreen.kt`). These are the gates a screenshot cannot check:
/// showing "Book again" on a Cancelled order, the Plus-only recurring CTA to a
/// resolved non-member, or hiding it from a member whose answer is still in
/// flight — all three render perfectly and all three are wrong.
final class OrderDetailFooterActionsTests: XCTestCase {
    func testBookAgainIsOfferedOnlyOnACompletedOrder() {
        XCTAssertTrue(OrderDetailFooterActions.showRebook(._5))
        XCTAssertFalse(OrderDetailFooterActions.showRebook(._0))
        XCTAssertFalse(OrderDetailFooterActions.showRebook(._1))
        XCTAssertFalse(OrderDetailFooterActions.showRebook(._2))
        XCTAssertFalse(OrderDetailFooterActions.showRebook(._3))
        XCTAssertFalse(OrderDetailFooterActions.showRebook(._4))
        XCTAssertFalse(OrderDetailFooterActions.showRebook(nil))
    }

    /// A cancelled cleaning never happened, so there is nothing to repeat.
    /// Android gates strictly on Completed and iOS must not drift wider.
    func testBookAgainIsNotOfferedOnACancelledOrder() {
        XCTAssertFalse(OrderDetailFooterActions.showRebook(._6))
        XCTAssertFalse(OrderDetailFooterActions.showMakeRecurring(._6, authoring: .allowed))
    }

    func testMakeRecurringNeedsBothCompletedAndTheAuthoringHalfOfPlus() {
        XCTAssertTrue(OrderDetailFooterActions.showMakeRecurring(._5, authoring: .allowed))
        XCTAssertFalse(OrderDetailFooterActions.showMakeRecurring(._5, authoring: .upsell))
        XCTAssertFalse(OrderDetailFooterActions.showMakeRecurring(._4, authoring: .allowed))
        XCTAssertFalse(OrderDetailFooterActions.showMakeRecurring(nil, authoring: .allowed))
    }

    /// A membership answer that has not landed used to read as "not a member", which
    /// took the shortcut away from paid-up members on a cold entry.
    func testAnUnresolvedMembershipStillOffersMakeRecurring() {
        XCTAssertTrue(
            OrderDetailFooterActions.showMakeRecurring(._5, authoring: .resolve(hasMembership: nil))
        )
        XCTAssertFalse(
            OrderDetailFooterActions.showMakeRecurring(._5, authoring: .resolve(hasMembership: false))
        )
    }

    /// The footer's own render gate. It used to be `isCancellable || isReportable`,
    /// which happens to cover Completed today only because Completed is reportable —
    /// an accident that would silently hide both new CTAs if the dispute window
    /// ever narrowed.
    func testTheFooterRendersWheneverAnyOfItsFourActionsWould() {
        XCTAssertTrue(OrderDetailFooterActions.showFooter(._5, authoring: .upsell))
        XCTAssertTrue(OrderDetailFooterActions.showFooter(._2, authoring: .upsell))
        XCTAssertTrue(OrderDetailFooterActions.showFooter(._0, authoring: .upsell))
        XCTAssertFalse(OrderDetailFooterActions.showFooter(._6, authoring: .allowed))
        XCTAssertFalse(OrderDetailFooterActions.showFooter(nil, authoring: .allowed))
    }

    /// The pure gate above is only as good as what the view hands it. Reverting the
    /// screen to the shell-injected `Bool` reinstates the defect and leaves every
    /// assertion in this class green, and there is no view harness to see it.
    func testTheViewFeedsTheGateItsViewModelResolved() throws {
        let source = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")

        XCTAssertTrue(
            source.contains("OrderDetailFooterActions.showFooter(order.status, authoring: vm.recurringAuthoring)"),
            "the footer gate no longer reads the resolved gate"
        )
        XCTAssertTrue(
            source.contains("authoring: vm.recurringAuthoring"),
            "the make-recurring CTA no longer reads the resolved gate"
        )
        XCTAssertNil(
            source.range(of: "hasMembership"),
            "the view reads a membership flag again instead of the resolved gate"
        )
    }

    /// The shell used to answer the membership question for this screen out of its own
    /// observed copy, which is the read-without-fetch half of the defect.
    func testTheShellHandsTheScreenTheRepositoryNotAnAnswer() throws {
        let source = try readSource("CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift")
        let orderDetail = try block(in: source, after: "private func orderDetail(_ orderId: String) -> some View {")

        XCTAssertTrue(
            orderDetail.contains("membershipRepository: container.membershipRepository"),
            "the order-detail screen is built without a membership repository to fetch from"
        )
        XCTAssertNil(
            orderDetail.range(of: "hasMembership"),
            "the shell answers the membership question for the order-detail screen again"
        )
    }

    private func block(in source: String, after marker: String) throws -> String {
        let start = try XCTUnwrap(source.range(of: marker), "no `\(marker)` in source")
        XCTAssertEqual(source.range(of: marker, options: .backwards), start, "`\(marker)` is not unique")
        var depth = 1
        var index = start.upperBound
        while index < source.endIndex {
            if source[index] == "{" { depth += 1 }
            if source[index] == "}" {
                depth -= 1
                if depth == 0 { return String(source[start.upperBound ..< index]) }
            }
            index = source.index(after: index)
        }
        throw XCTSkip("unbalanced braces after `\(marker)`")
    }

    private func readSource(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}

final class LiveProgressLogicTests: XCTestCase {
    func testActiveStepMapsEachOfSevenStates() {
        XCTAssertEqual(LiveProgress.activeStep(for: ._0), .booked)
        XCTAssertEqual(LiveProgress.activeStep(for: ._1), .booked)
        XCTAssertEqual(LiveProgress.activeStep(for: ._2), .accepted)
        XCTAssertEqual(LiveProgress.activeStep(for: ._3), .onTheWay)
        XCTAssertEqual(LiveProgress.activeStep(for: ._4), .started)
        XCTAssertEqual(LiveProgress.activeStep(for: ._5), .finished)
        XCTAssertNil(LiveProgress.activeStep(for: ._6))
        XCTAssertNil(LiveProgress.activeStep(for: nil))
    }

    func testOnTheWayIsADistinctStepNotFoldedIntoInProgress() {
        XCTAssertEqual(LiveProgress.activeStep(for: ._3), .onTheWay)
        XCTAssertNotEqual(LiveProgress.activeStep(for: ._3), LiveProgress.activeStep(for: ._4))
        XCTAssertEqual(LiveProgressStep.allCases.count, 5)
    }

    func testUsesLiveHeroOnlyForActiveStates() {
        XCTAssertTrue(LiveProgress.usesLiveHero(._2))
        XCTAssertTrue(LiveProgress.usesLiveHero(._3))
        XCTAssertTrue(LiveProgress.usesLiveHero(._4))
        XCTAssertFalse(LiveProgress.usesLiveHero(._5))
        XCTAssertFalse(LiveProgress.usesLiveHero(._0))
    }

    func testInProgressFractionFromStartedEntry() {
        let started = Date(timeIntervalSince1970: 1000)
        let now = started.addingTimeInterval(30 * 60)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: started)]
        let fraction = LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now)
        XCTAssertEqual(fraction ?? 0, 0.5, accuracy: 0.001)
    }

    func testInProgressFractionCapsAtNinetySeven() {
        let started = Date(timeIntervalSince1970: 1000)
        let now = started.addingTimeInterval(10 * 60 * 60)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: started)]
        let fraction = LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now)
        XCTAssertEqual(fraction ?? 0, 0.97, accuracy: 0.001)
    }

    func testInProgressFractionNilWithoutAnchors() {
        XCTAssertNil(LiveProgress.inProgressFraction(history: [], estimatedMinutes: 60, now: Date()))
        XCTAssertNil(LiveProgress.inProgressFraction(
            history: [OrderFixtures.track(statusValue: 4, createdOn: Date())],
            estimatedMinutes: 0,
            now: Date()
        ))
    }
}

/// The cancellation schedule is the server's and only the server's. What stood here (T-0527) pinned a
/// client-side ladder that charged **50%** where the backend charges 25% and told the customer *"no
/// refund is available"* where it refunds half — a green suite holding a money defect in place. The two
/// facts that decide the tier, the caller's own free-cancellation window (a Plus plan's is seeded at 4
/// hours where the standard is 24) and whether a cleaner has actually taken the job, reach no client, so
/// nothing below may reintroduce a threshold or a rate: the inputs are the tier and the amounts the
/// preview endpoint returns.
final class CancellationQuoteTests: XCTestCase {
    func testEachWireTierMapsToItsOwnMeaning() {
        XCTAssertEqual(CancellationTier(wire: ._0), .freeNotAccepted)
        XCTAssertEqual(CancellationTier(wire: ._1), .freeOopsWindow)
        XCTAssertEqual(CancellationTier(wire: ._2), .freeOutsideWindow)
        XCTAssertEqual(CancellationTier(wire: ._3), .partial)
        XCTAssertEqual(CancellationTier(wire: ._4), .lastMinute)
    }

    func testTheServersAmountsAndWaiverFlagSurviveTheMapping() throws {
        let quote = try CancellationQuote(GetCancellationFeePreviewResponse(
            orderId: "o1",
            tier: ._3,
            feeRate: 0.25,
            feeAmount: 250,
            refundAmount: 750,
            totalPrice: 1000,
            currencyCode: "CZK",
            expressWaiverForfeitedOnCancel: true
        ))

        XCTAssertEqual(quote.tier, .partial)
        XCTAssertEqual(quote.feeAmount, 250)
        XCTAssertEqual(quote.refundAmount, 750)
        XCTAssertEqual(quote.currencyCode, "CZK")
        XCTAssertTrue(quote.forfeitsExpressWaiver)
    }

    /// The sheet already falls back to the order's own currency, so a null here has an equally
    /// authoritative replacement — refusing it would strand a customer on a booking they want gone.
    func testAQuoteWithNoCurrencyStillMapsBecauseTheSheetHasASecondSource() throws {
        let quote = try CancellationQuote(GetCancellationFeePreviewResponse(
            tier: ._0,
            feeAmount: 0,
            refundAmount: 1000,
            expressWaiverForfeitedOnCancel: false
        ))

        XCTAssertNil(quote.currencyCode)
    }
}

/// The card's whole content resolved from the quote state, hoisted out of the view so the sentence and
/// the severity are assertable: both are bare arguments inside the sheet and invisible to every check
/// available without a snapshot harness. Mirrors Android's `cancellationFeeCallout`.
final class CancellationFeeCardModelTests: XCTestCase {
    func testTheCardSaysNothingNumericWhileTheQuoteIsInFlight() {
        XCTAssertEqual(CancellationFeeCardModel(.loading), .checking)
    }

    /// A fee-preview outage degrades to the neutral prompt — never to a computed number, and never to a
    /// blocked cancellation.
    func testAFailedQuoteDegradesToTheNeutralPrompt() {
        XCTAssertEqual(CancellationFeeCardModel(.error(ApiError(httpStatus: 500))), .unavailable)
    }

    /// All three are zero-fee and none is the same sentence: `FreeNotAccepted` means no cleaner has taken
    /// the job, `FreeOopsWindow` means you just booked, `FreeOutsideWindow` means you are early.
    /// Re-deriving meaning from the rate collapses them into one.
    func testTheThreeZeroFeeTiersAreThreeDifferentSentences() {
        let callouts = [
            Self.callout(.freeNotAccepted),
            Self.callout(.freeOopsWindow),
            Self.callout(.freeOutsideWindow)
        ]

        XCTAssertEqual(callouts.map(\.titleKey), [
            "order_cancel_fee_not_accepted",
            "order_cancel_fee_oops",
            "order_cancel_fee_outside_window"
        ])
        XCTAssertEqual(callouts.map(\.severity), [.free, .free, .free])
        XCTAssertEqual(callouts.map(\.amountKey), Array(repeating: "order_cancel_fee_none", count: 3))
        XCTAssertEqual(callouts.flatMap(\.amounts), [])
    }

    func testAChargedTierCarriesTheServersFeeAndItsRefundInThatOrder() {
        let partial = Self.callout(.partial, fee: 250, refund: 750)
        let lastMinute = Self.callout(.lastMinute, fee: 500, refund: 500)

        XCTAssertEqual(partial.titleKey, "order_cancel_fee_partial")
        XCTAssertEqual(partial.amountKey, "order_cancel_fee_split")
        XCTAssertEqual(partial.amounts, [250, 750])
        XCTAssertEqual(partial.severity, .fee)
        XCTAssertEqual(lastMinute.titleKey, "order_cancel_fee_last_minute")
        XCTAssertEqual(lastMinute.amounts, [500, 500])
        XCTAssertEqual(lastMinute.severity, .lastMinute)
    }

    /// AM-13: the forfeiture is disclosed off the server's flag alone, and it matters most exactly where
    /// the fee is zero — inside the oops window and on every cash order — because that is where it is
    /// otherwise invisible.
    func testTheExpressWaiverWarningRidesTheServerFlagEvenOnAFreeTier() {
        XCTAssertTrue(Self.callout(.freeOopsWindow, forfeitsExpressWaiver: true).warnsExpressWaiverForfeited)
        XCTAssertFalse(Self.callout(.lastMinute).warnsExpressWaiverForfeited)
    }

    private static func callout(
        _ tier: CancellationTier,
        fee: Double = 0,
        refund: Double = 1000,
        forfeitsExpressWaiver: Bool = false
    ) -> CancellationFeeCallout {
        let quote = CancellationQuote(
            tier: tier,
            feeAmount: fee,
            refundAmount: refund,
            currencyCode: "CZK",
            forfeitsExpressWaiver: forfeitsExpressWaiver
        )
        guard case let .quoted(callout) = CancellationFeeCardModel(.loaded(quote)) else {
            return CancellationFeeCallout(
                titleKey: "",
                amountKey: "",
                amounts: [],
                severity: .free,
                warnsExpressWaiverForfeited: false
            )
        }
        return callout
    }
}

final class CancelOrderConfirmGateTests: XCTestCase {
    func testConfirmWaitsForTheQuoteToResolve() {
        XCTAssertFalse(CancelOrderConfirmGate.canConfirm(
            hasReason: true,
            needsNotes: false,
            notes: "",
            quoteIsLoading: true,
            isSubmitting: false
        ))
    }

    /// A fee-preview outage renders the neutral prompt and leaves the button live — a customer who wants
    /// a booking gone is never held hostage by a quote we could not fetch.
    func testAFailedQuoteStillLetsTheCancellationThrough() {
        XCTAssertTrue(CancelOrderConfirmGate.canConfirm(
            hasReason: true,
            needsNotes: false,
            notes: "",
            quoteIsLoading: false,
            isSubmitting: false
        ))
    }

    func testAReasonIsStillRequiredAndOtherStillNeedsWords() {
        XCTAssertFalse(CancelOrderConfirmGate.canConfirm(
            hasReason: false,
            needsNotes: false,
            notes: "",
            quoteIsLoading: false,
            isSubmitting: false
        ))
        XCTAssertFalse(CancelOrderConfirmGate.canConfirm(
            hasReason: true,
            needsNotes: true,
            notes: "  x ",
            quoteIsLoading: false,
            isSubmitting: false
        ))
        XCTAssertTrue(CancelOrderConfirmGate.canConfirm(
            hasReason: true,
            needsNotes: true,
            notes: "moved out",
            quoteIsLoading: false,
            isSubmitting: false
        ))
    }

    func testASubmitInFlightHoldsTheButton() {
        XCTAssertFalse(CancelOrderConfirmGate.canConfirm(
            hasReason: true,
            needsNotes: false,
            notes: "",
            quoteIsLoading: false,
            isSubmitting: true
        ))
    }
}

/// Every tier the server can answer with has to reach a real sentence in all five shipped locales, and no
/// two tiers may share one — a fallback that silently reuses another tier's copy reads as finished.
final class CancellationFeeCopyTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    private static let keys = [
        "order_cancel_fee_checking",
        "order_cancel_fee_neutral",
        "order_cancel_fee_unavailable",
        "order_cancel_fee_retry",
        "order_cancel_fee_recheck_note",
        "order_cancel_fee_not_accepted",
        "order_cancel_fee_oops",
        "order_cancel_fee_outside_window",
        "order_cancel_fee_partial",
        "order_cancel_fee_last_minute",
        "order_cancel_fee_none",
        "order_cancel_express_waiver_forfeit"
    ]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testEverySentenceIsDistinctAndTranslatedInEveryLocale() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            let texts = Self.keys.map { L10n.localized($0) }
            for (key, text) in zip(Self.keys, texts) {
                XCTAssertFalse(text.isBlank, "\(key) is empty in \(language)")
                XCTAssertNotEqual(text, key, "\(key) is unlocalized in \(language)")
            }
            XCTAssertEqual(Set(texts).count, texts.count, "two cancel-sheet strings collide in \(language)")
        }
    }

    /// The money line is the same sentence for both charged tiers, and it must place the fee first and the
    /// refund second in every locale — a swapped pair reads as a plausible number and is a lie about money.
    func testTheSplitLineStatesTheFeeThenTheRefund() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            let text = L10n.format("order_cancel_fee_split", arguments: ["250 Kč", "750 Kč"])
            let fee = try XCTUnwrap(text.range(of: "250 Kč"), "the fee is missing in \(language): \(text)")
            let refund = try XCTUnwrap(text.range(of: "750 Kč"), "the refund is missing in \(language): \(text)")
            XCTAssertTrue(fee.lowerBound < refund.lowerBound, "fee and refund are swapped in \(language)")
        }
    }

    func testTheForfeitedExpressBookingIsWarnedAboutWithoutNamingTheQuota() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            let warning = L10n.OrderCancel.expressWaiverForfeit
            XCTAssertFalse(warning.isBlank, "the express-waiver warning is empty in \(language)")
            XCTAssertNil(
                warning.rangeOfCharacter(from: .decimalDigits),
                "the warning spells a quota out in \(language) — it is per-plan configurable: \(warning)"
            )
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
