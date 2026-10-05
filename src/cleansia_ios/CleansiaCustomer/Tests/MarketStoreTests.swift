import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The market a customer browses in: resolved stored-if-listed → default → first, persisted, and the
/// no-market shape when the directory cannot be read.
@MainActor
final class MarketStoreTests: XCTestCase {
    // MARK: Resolution

    func testAFreshInstallResolvesTheDefaultMarketAndPersistsIt() async {
        let (store, _, preference) = MarketFixtures.store()

        await store.refresh()

        XCTAssertEqual(store.selected, MarketFixtures.czechia)
        XCTAssertEqual(store.state.countryId, "cze")
        XCTAssertEqual(preference.writes, ["CZE"])
    }

    func testAStoredMarketThatIsListedWins() async {
        let (store, _, preference) = MarketFixtures.store(stored: "SVK")

        await store.refresh()

        XCTAssertEqual(store.selected, MarketFixtures.slovakia)
        XCTAssertEqual(preference.marketIsoCode, "SVK")
    }

    func testADelistedStoredMarketFallsToTheDefaultAndIsOverwritten() async {
        let (store, _, preference) = MarketFixtures.store(stored: "POL")

        await store.refresh()

        XCTAssertEqual(store.selected, MarketFixtures.czechia)
        XCTAssertEqual(preference.marketIsoCode, "CZE")
    }

    func testWithNoRowFlaggedDefaultTheFirstListedMarketIsSelected() async {
        let germany = MarketFixtures.market(
            countryId: "deu", isoCode: "DEU", isoAlpha2: "DE", name: "Germany", currencyCode: "EUR"
        )
        let (store, _, _) = MarketFixtures.store(.success([germany, MarketFixtures.slovakia]))

        await store.refresh()

        XCTAssertEqual(store.selected, germany)
    }

    /// A stored value is attacker-controlled text: it is compared against the list and never
    /// rendered or sent.
    func testAStoredValueIsOnlyEverComparedNeverRendered() async {
        let junk = "<script>alert(1)</script>"
        let (store, _, preference) = MarketFixtures.store(stored: junk)

        await store.refresh()

        XCTAssertEqual(store.selected, MarketFixtures.czechia)
        XCTAssertEqual(preference.marketIsoCode, "CZE")
        XCTAssertFalse("\(store.state)".contains(junk))
    }

    // MARK: The no-market state

    func testAFailedReadIsTheNoMarketStateAndPersistsNothing() async {
        let (store, _, preference) = MarketFixtures.store(.failure(ApiError(httpStatus: 500)))

        await store.refresh()

        XCTAssertEqual(store.state, .unavailable)
        XCTAssertNil(store.state.countryId)
        XCTAssertFalse(store.state.offersChoice)
        XCTAssertTrue(preference.writes.isEmpty)
    }

    func testAnEmptyDirectoryIsTheNoMarketState() async {
        let (store, _, preference) = MarketFixtures.store(.success([]))

        await store.refresh()

        XCTAssertEqual(store.state, .unavailable)
        XCTAssertTrue(preference.writes.isEmpty)
    }

    func testAFailedReReadKeepsTheLastList() async {
        let (store, client, _) = MarketFixtures.store(stored: "SVK")
        await store.refresh()

        client.result = .failure(ApiError(httpStatus: 500))
        await store.refresh()

        XCTAssertEqual(store.selected, MarketFixtures.slovakia)
        XCTAssertTrue(store.state.offersChoice)
    }

    func testTheNoMarketStateIsRetriedOnTheNextEntryButAResolvedOneIsNotInsideTheWindow() async {
        let (store, client, _) = MarketFixtures.store(.failure(ApiError(httpStatus: 500)))
        await store.refresh()
        XCTAssertEqual(client.callCount, 1)

        client.result = .success(MarketFixtures.two)
        await store.refreshIfStale()
        XCTAssertEqual(client.callCount, 2)
        XCTAssertEqual(store.selected, MarketFixtures.czechia)

        await store.refreshIfStale()
        XCTAssertEqual(client.callCount, 2, "a fresh directory costs nothing on the next entry")
    }

    func testConcurrentRefreshesShareOneRead() async {
        let (store, client, _) = MarketFixtures.store()
        client.gate = { for _ in 0 ..< 3 {
            await Task.yield()
        } }

        async let first: Void = store.refresh()
        async let second: Void = store.refresh()
        _ = await (first, second)

        XCTAssertEqual(client.callCount, 1)
        XCTAssertEqual(store.selected, MarketFixtures.czechia)
    }

    // MARK: Selection

    func testSelectingAListedMarketPersistsAndSwitches() async {
        let (store, _, preference) = MarketFixtures.store()
        await store.refresh()

        store.select(isoCode: "SVK")

        XCTAssertEqual(store.selected, MarketFixtures.slovakia)
        XCTAssertEqual(store.state.countryId, "svk")
        XCTAssertEqual(preference.writes, ["CZE", "SVK"])
    }

    func testSelectingACodeTheListDoesNotKnowChangesNothing() async {
        let (store, _, preference) = MarketFixtures.store()
        await store.refresh()

        store.select(isoCode: "POL")

        XCTAssertEqual(store.selected, MarketFixtures.czechia)
        XCTAssertEqual(preference.writes, ["CZE"])
    }

    // MARK: What the readers derive

    func testTheChipReadsTheCountryCodeAndTheCurrencyCode() {
        XCTAssertEqual(MarketFixtures.czechia.chipLabel, "CZ · CZK")
        XCTAssertEqual(MarketFixtures.slovakia.chipLabel, "SK · EUR")
    }

    func testOnlyTwoOrMoreMarketsOfferAChoice() async {
        let one = await MarketFixtures.resolved(MarketFixtures.one)
        XCTAssertFalse(one.state.offersChoice)

        let two = await MarketFixtures.resolved(MarketFixtures.two)
        XCTAssertTrue(two.state.offersChoice)
    }

    func testThePickerRowNamesTheMarketInTheCustomersLanguageWithItsCurrency() {
        XCTAssertEqual(MarketPickerLabel.row(MarketFixtures.czechia, locale: Locale(identifier: "cs")), "Česko · CZK")
        XCTAssertEqual(MarketPickerLabel.row(MarketFixtures.czechia, locale: Locale(identifier: "en")), "Czechia · CZK")
        XCTAssertEqual(
            MarketPickerLabel.row(MarketFixtures.slovakia, locale: Locale(identifier: "uk")),
            "Slovakia · EUR"
        )
    }

    func testTheInsuranceCeilingIsStatedInTheCountrysCurrencyOrNotAtAll() async {
        let store = await MarketFixtures.resolved()

        XCTAssertEqual(store.state.insurance(forCountryId: "cze"), MarketMoney(amount: 1_000_000, currencyCode: "CZK"))
        XCTAssertNil(store.state.insurance(forCountryId: "svk"), "no ceiling authored")
        XCTAssertNil(store.state.insurance(forCountryId: "pol"), "not a listed market")
        XCTAssertNil(store.state.insurance(forCountryId: nil))
        XCTAssertNil(MarketState.unavailable.insurance(forCountryId: "cze"))
    }

    /// A referral is credited in money, so the figure the copy states is the chosen market's, in its
    /// currency — and none where the market authors none or pays zero, or before a market is known.
    func testTheReferralCreditIsTheChosenMarketsInItsCurrencyOrNone() async {
        let store = await MarketFixtures.resolved()
        XCTAssertEqual(store.state.referralCredit, MarketMoney(amount: 150, currencyCode: "CZK"))

        store.select(isoCode: "SVK")
        XCTAssertNil(store.state.referralCredit, "no credit authored")

        let zero = MarketFixtures.market(
            countryId: "pol", isoCode: "POL", isoAlpha2: "PL", name: "Poland", currencyCode: "PLN", referralCredit: 0
        )
        let paysNone = await MarketFixtures.resolved([zero], selected: zero)
        XCTAssertNil(paysNone.state.referralCredit, "zero pays none")

        XCTAssertNil(MarketState.loading.referralCredit)
        XCTAssertNil(MarketState.unavailable.referralCredit)
    }

    func testTheDefaultCurrencyIsTheFlaggedRows() async {
        let store = await MarketFixtures.resolved(selected: MarketFixtures.slovakia)
        XCTAssertEqual(store.state.defaultCurrencyCode, "CZK")
        XCTAssertNil(MarketState.unavailable.defaultCurrencyCode)
    }
}
