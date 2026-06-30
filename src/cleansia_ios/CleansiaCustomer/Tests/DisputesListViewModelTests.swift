import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class DisputeRepositoryTests: XCTestCase {
    func testRefreshReplacesPageZeroAndSetsTotal() async {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 3)]
        let repo = DisputeRepository(client: client, pageSize: 1)

        await repo.refresh()

        XCTAssertEqual(repo.disputes.map(\.id), ["a"])
        XCTAssertEqual(repo.totalRecords, 3)
        XCTAssertTrue(repo.loaded)
        XCTAssertTrue(repo.hasMore)
        XCTAssertEqual(client.pageRequests.first?.offset, 0)
    }

    func testLoadNextPageAppendsAdditively() async {
        let client = FakeDisputeClient()
        client.pages = [
            DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 2),
            DisputesPage(items: [DisputeFixtures.entry(id: "b")], total: 2)
        ]
        let repo = DisputeRepository(client: client, pageSize: 1)

        await repo.refresh()
        await repo.loadNextPage()

        XCTAssertEqual(repo.disputes.map(\.id), ["a", "b"])
        XCTAssertFalse(repo.hasMore)
        XCTAssertEqual(client.pageRequests.last?.offset, 1)
    }

    func testLoadNextPageNoOpWhenExhausted() async {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 1)]
        let repo = DisputeRepository(client: client, pageSize: 1)

        await repo.refresh()
        await repo.loadNextPage()

        XCTAssertEqual(client.pageRequests.count, 1)
    }

    func testClearWipesCache() async {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 1)]
        let repo = DisputeRepository(client: client, pageSize: 1)
        await repo.refresh()

        await repo.clear()

        XCTAssertTrue(repo.disputes.isEmpty)
        XCTAssertEqual(repo.totalRecords, 0)
        XCTAssertFalse(repo.loaded)
    }
}

@MainActor
final class DisputesListViewModelTests: XCTestCase {
    private func makeVM(_ client: FakeDisputeClient) -> (DisputesListViewModel, DisputeRepository) {
        let repo = DisputeRepository(client: client, pageSize: 1)
        let vm = DisputesListViewModel(repository: repo, snackbar: SnackbarController())
        return (vm, repo)
    }

    func testPullToRefreshLoadsAndSurfacesLoaded() async {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 1)]
        let (vm, _) = makeVM(client)

        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["a"])
        XCTAssertEqual(vm.refreshPhase, .idle)
    }

    func testEmptyLoadedRendersAsEmptyNotError() async {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [], total: 0)]
        let (vm, _) = makeVM(client)

        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.isEmpty, true)
    }

    func testFirstLoadFailureFlipsToError() async {
        let client = FakeDisputeClient()
        client.pageError = ApiError(httpStatus: 500)
        let (vm, _) = makeVM(client)

        await vm.retry()

        if case .error = vm.state {} else { XCTFail("expected error state") }
    }

    func testRefreshFailureWhileLoadedStaysLoaded() async {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 1)]
        let (vm, _) = makeVM(client)
        await vm.pullToRefresh()

        client.pageError = ApiError(httpStatus: 500)
        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["a"])
    }

    func testLoadNextPageAppendsThroughViewModel() async {
        let client = FakeDisputeClient()
        client.pages = [
            DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 2),
            DisputesPage(items: [DisputeFixtures.entry(id: "b")], total: 2)
        ]
        let (vm, _) = makeVM(client)
        await vm.pullToRefresh()
        XCTAssertTrue(vm.hasMore)

        await vm.loadNextPage()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["a", "b"])
        XCTAssertFalse(vm.hasMore)
    }
}
