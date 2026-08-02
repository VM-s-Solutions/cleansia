import XCTest
@testable import CleansiaCore

final class LiveActivityStepTests: XCTestCase {
    func testTheJourneyIsFourLegsInLifecycleOrder() {
        XCTAssertEqual(LiveActivityStep.allCases, [.confirmed, .onTheWay, .cleaning, .done])
        XCTAssertEqual(LiveActivityStep.count, 4)
    }

    func testLegNumbersReadOneThroughFour() {
        XCTAssertEqual(LiveActivityStep.allCases.map(\.number), [1, 2, 3, 4])
    }

    func testEachLegPointsBackAtTheOneBeforeItAndTheFirstAtNothing() {
        XCTAssertNil(LiveActivityStep.confirmed.previous)
        XCTAssertEqual(LiveActivityStep.onTheWay.previous, .confirmed)
        XCTAssertEqual(LiveActivityStep.cleaning.previous, .onTheWay)
        XCTAssertEqual(LiveActivityStep.done.previous, .cleaning)
    }

    func testEveryStatusTheBackendCanSendLandsOnItsOwnLeg() {
        XCTAssertEqual(LiveActivityCard.forStatus("onTheWay"), .journey(.onTheWay))
        XCTAssertEqual(LiveActivityCard.forStatus("inProgress"), .journey(.cleaning))
        XCTAssertEqual(LiveActivityCard.forStatus("completed"), .journey(.done))
        XCTAssertEqual(LiveActivityCard.forStatus("cancelled"), .cancelled)
    }

    /// ADR-0029 D4: an unrecognised status decodes fine and renders a generic in-service card. It must
    /// never be placed on the journey — a guessed position is worse than an admitted unknown.
    func testAnUnknownStatusRendersGenericRatherThanGuessingAPosition() {
        let unknown = ["", " ", "paused", "onTheWay ", "ontheway", "InProgress", "completed2", "v2Rescheduled"]

        for status in unknown {
            XCTAssertEqual(LiveActivityCard.forStatus(status), .unknown, "\(status.debugDescription) got placed")
        }
    }

    func testTheWireStatusesAreExactlyTheFourTheContractDefines() {
        XCTAssertEqual(
            LiveActivityStatus.allCases.map(\.rawValue),
            ["onTheWay", "inProgress", "completed", "cancelled"]
        )
    }

    /// The two terminal strings the app writes when it ends a card locally must stay inside the vocabulary
    /// the card renders, or a locally-ended activity shows the unknown presentation.
    func testTheTerminalStatusesStayInsideTheRenderedVocabulary() {
        for terminal in LiveActivityTerminalStatus.allCases {
            XCTAssertNotNil(
                LiveActivityStatus(rawValue: terminal.rawValue),
                "\(terminal.rawValue) is written on end but the card cannot render it"
            )
            XCTAssertNotEqual(LiveActivityCard.forStatus(terminal.rawValue), .unknown)
        }
    }

    func testALegBeforeThePositionIsComplete() {
        XCTAssertEqual(LiveActivityStep.confirmed.leg(at: .cleaning), .complete)
        XCTAssertEqual(LiveActivityStep.onTheWay.leg(at: .cleaning), .complete)
    }

    func testTheLegAtThePositionIsCurrent() {
        XCTAssertEqual(LiveActivityStep.cleaning.leg(at: .cleaning), .current)
    }

    func testALegAfterThePositionIsUpcoming() {
        XCTAssertEqual(LiveActivityStep.done.leg(at: .cleaning), .upcoming)
    }

    /// Exhaustive: at every position exactly one leg is current, everything before it is complete, and
    /// everything after it is upcoming. A `<=` slipped into the comparison fails here, not in QA.
    func testAtEveryPositionTheLegsPartitionCleanly() {
        for position in LiveActivityStep.allCases {
            let legs = LiveActivityStep.allCases.map { $0.leg(at: position) }

            XCTAssertEqual(legs.filter { $0 == .current }.count, 1, "at \(position)")
            XCTAssertEqual(legs.filter { $0 == .complete }.count, position.rawValue, "at \(position)")
            XCTAssertEqual(
                legs.filter { $0 == .upcoming }.count,
                LiveActivityStep.count - position.rawValue - 1,
                "at \(position)"
            )
        }
    }

    /// The card only ever opens once the cleaner is on the way (ADR-0029 D2), so `confirmed` is a passed
    /// marker and carries no line of its own; every leg a card can actually sit on explains itself.
    func testEveryLegACardCanSitOnCarriesADetailLine() {
        XCTAssertNil(LiveActivityStep.confirmed.detail)

        for step in [LiveActivityStep.onTheWay, .cleaning, .done] {
            XCTAssertFalse(step.detail?.isEmpty ?? true, "\(step) has no detail line")
        }
    }
}

final class LiveActivityCardTests: XCTestCase {
    private func card(_ status: String) -> LiveActivityCard {
        LiveActivityCard.forStatus(status)
    }

    /// The headline is the point of the stepper design: it names where the order came FROM as well as
    /// where it is, so the card narrates a move rather than announcing a state.
    func testAnInServiceCardHeadlinesTheMoveItJustMade() {
        XCTAssertEqual(card("onTheWay").headline, .transition(from: .confirmed, to: .onTheWay))
        XCTAssertEqual(card("inProgress").headline, .transition(from: .onTheWay, to: .cleaning))
    }

    /// Nothing follows the last leg, so it stops narrating a move and simply says the clean is over.
    func testTheFinishedAndDeadCardsHeadlineTheirOwnOutcome() {
        XCTAssertEqual(card("completed").headline, .completed)
        XCTAssertEqual(card("cancelled").headline, .cancelled)
        XCTAssertEqual(card("something_new").headline, .generic)
    }

    func testOnlyAPlacedCardDrawsTheStepper() {
        XCTAssertEqual(card("onTheWay").position, .onTheWay)
        XCTAssertEqual(card("inProgress").position, .cleaning)
        XCTAssertEqual(card("completed").position, .done)
        XCTAssertNil(card("cancelled").position)
        XCTAssertNil(card("something_new").position)
    }

    /// A finish time is a promise. A finished clean has nothing left to finish and a cancelled one never
    /// will, so neither may show one — a lock screen promising a finish for a dead order is the worst
    /// thing this card can do.
    func testOnlyACardWithSomethingLeftToFinishShowsAFinishTime() {
        XCTAssertTrue(card("onTheWay").showsFinishTime)
        XCTAssertTrue(card("inProgress").showsFinishTime)
        XCTAssertFalse(card("completed").showsFinishTime)
        XCTAssertFalse(card("cancelled").showsFinishTime)
    }

    /// An unknown status is still an in-service card (ADR-0029 D4), so the clock keeps its promise even
    /// though the journey position cannot be drawn.
    func testAnUnknownCardKeepsItsFinishTimeButDrawsNoPosition() {
        XCTAssertTrue(card("something_new").showsFinishTime)
        XCTAssertNil(card("something_new").position)
    }

    func testEveryCardThatExplainsItselfHasCopyToExplainWith() {
        for status in ["onTheWay", "inProgress", "completed", "cancelled"] {
            XCTAssertFalse(card(status).detail?.isEmpty ?? true, "\(status) has no detail line")
        }
    }

    /// There is no copy for a status this build has never heard of, and inventing one would put words in
    /// the order's mouth.
    func testAnUnknownCardSaysNothingItCannotKnow() {
        XCTAssertNil(card("something_new").detail)
    }
}

/// The leg vocabulary is what makes the lock screen and the in-app order screen tell one story, so it is
/// pinned in all five app languages the same way the rest of the card's copy is.
final class LiveActivityStepL10nTests: XCTestCase {
    private static let regions = ["en", "cs", "sk", "uk", "ru"]

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    func testEveryLegLabelIsTranslatedInAllFiveLanguages() {
        CoreL10n.apply(languageTag: "en")
        let english = LiveActivityStep.allCases.map(\.label)

        for region in Self.regions {
            CoreL10n.apply(languageTag: region)

            for (index, step) in LiveActivityStep.allCases.enumerated() {
                XCTAssertFalse(step.label.isEmpty, "\(step) is empty in \(region)")
                XCTAssertFalse(step.label.hasPrefix("live_activity."), "\(step) fell through to its key in \(region)")
                if region != "en" {
                    XCTAssertNotEqual(step.label, english[index], "\(step) in \(region) was never translated")
                }
            }
        }
    }

    /// Four legs that read the same are not a stepper. Catches the copy-paste that points two legs at one key.
    func testTheFourLegLabelsAreDistinctInEveryLanguage() {
        for region in Self.regions {
            CoreL10n.apply(languageTag: region)

            XCTAssertEqual(Set(LiveActivityStep.allCases.map(\.label)).count, LiveActivityStep.count, region)
        }
    }

    func testTheStepCounterSubstitutesBothNumbersInEveryLanguage() {
        for region in Self.regions {
            CoreL10n.apply(languageTag: region)
            let counter = LiveActivityL10n.stepOf(3, 4)

            XCTAssertTrue(counter.contains("3"), "\(region) dropped the position: \(counter)")
            XCTAssertTrue(counter.contains("4"), "\(region) dropped the total: \(counter)")
        }
    }
}
