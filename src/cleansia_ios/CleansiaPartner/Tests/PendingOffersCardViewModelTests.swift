import CleansiaCore
import CleansiaPartnerApi
import Foundation
import XCTest
@testable import CleansiaPartner

/// The entry point to the offers surface, and the reason there is no permanent tab: a cleaner with no
/// pending offer — which is nearly every cleaner on nearly every day — is shown nothing at all, so the
/// empty state is never a cost anyone pays daily.
@MainActor
final class PendingOffersCardViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var store: PendingOffersStore!

    override func setUp() async throws {
        client = FakePartnerOrderClient()
        store = PendingOffersStore(client: client, ordersStaleness: OrdersStaleness())
    }

    private func makeVM() -> PendingOffersCardViewModel {
        PendingOffersCardViewModel(store: store)
    }

    private func offer(id: String, respondByUtc: Date) -> PendingOfferItem {
        .sample(id: id, respondByUtc: respondByUtc)
    }

    func testNoOffersMeansNoCard() async {
        client.pendingOffersResult = .success([])
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.state, .hidden)
    }

    func testOffersSurfaceTheSoonestDeadlineAndHowManyMoreThereAre() async {
        let soon = Date(timeIntervalSince1970: 1_786_000_000)
        let late = Date(timeIntervalSince1970: 1_786_100_000)
        client.pendingOffersResult = .success([
            offer(id: "late", respondByUtc: late),
            offer(id: "soon", respondByUtc: soon)
        ])
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.state, .visible(count: 2, soonestRespondBy: soon))
    }

    func testACardThatCannotAnswerItsOwnQuestionSimplyDoesNotAppear() async {
        client.pendingOffersResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.state, .hidden)
    }

    func testAWarmCacheIsRenderedWithoutASecondCall() async {
        client.pendingOffersResult = .success([.sample(id: "a")])
        _ = await store.refresh()
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(client.pendingOffersCallCount, 1)
        XCTAssertEqual(vm.state, .visible(count: 1, soonestRespondBy: PendingOfferItem.sample(id: "a").respondByUtc))
    }
}
