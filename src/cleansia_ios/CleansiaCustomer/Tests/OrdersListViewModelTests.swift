import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

@MainActor
final class OrderRepositoryTests: XCTestCase {
    func testRefreshReplacesPageZeroAndSetsTotal() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 3)]
        let repo = OrderRepository(client: client, pageSize: 1)

        await repo.refresh()

        XCTAssertEqual(repo.orders.map(\.id), ["a"])
        XCTAssertEqual(repo.totalRecords, 3)
        XCTAssertTrue(repo.loaded)
        XCTAssertTrue(repo.hasMore)
        XCTAssertEqual(client.pageRequests.first?.offset, 0)
    }

    func testLoadNextPageAppendsAdditively() async {
        let client = FakeOrderClient()
        client.pages = [
            OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 2),
            OrdersPage(items: [OrderFixtures.listItem(id: "b", statusValue: 5)], total: 2)
        ]
        let repo = OrderRepository(client: client, pageSize: 1)

        await repo.refresh()
        await repo.loadNextPage()

        XCTAssertEqual(repo.orders.map(\.id), ["a", "b"])
        XCTAssertFalse(repo.hasMore)
        XCTAssertEqual(client.pageRequests.last?.offset, 1)
    }

    func testLoadNextPageNoOpWhenExhausted() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 1)]
        let repo = OrderRepository(client: client, pageSize: 1)

        await repo.refresh()
        await repo.loadNextPage()

        XCTAssertEqual(client.pageRequests.count, 1)
    }

    func testClearWipesCache() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 1)]
        let repo = OrderRepository(client: client, pageSize: 1)
        await repo.refresh()

        await repo.clear()

        XCTAssertTrue(repo.orders.isEmpty)
        XCTAssertEqual(repo.totalRecords, 0)
        XCTAssertFalse(repo.loaded)
    }
}

@MainActor
final class OrdersListViewModelTests: XCTestCase {
    private func makeVM(_ client: FakeOrderClient) -> (OrdersListViewModel, OrderRepository) {
        let repo = OrderRepository(client: client, pageSize: 1)
        let vm = OrdersListViewModel(repository: repo, snackbar: SnackbarController())
        return (vm, repo)
    }

    func testPullToRefreshLoadsAndSurfacesLoaded() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 1)]
        let (vm, _) = makeVM(client)

        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["a"])
        XCTAssertEqual(vm.refreshPhase, .idle)
    }

    func testEmptyLoadedRendersAsEmptyLoadedNotError() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [], total: 0)]
        let (vm, _) = makeVM(client)

        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.isEmpty, true)
    }

    func testFirstLoadFailureFlipsToError() async {
        let client = FakeOrderClient()
        client.pageError = ApiError(httpStatus: 500)
        let (vm, _) = makeVM(client)

        await vm.retry()

        if case .error = vm.state {} else { XCTFail("expected error state") }
    }

    func testRefreshFailureWhileLoadedStaysLoaded() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 1)]
        let (vm, _) = makeVM(client)
        await vm.pullToRefresh()

        client.pageError = ApiError(httpStatus: 500)
        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["a"])
    }

    func testLoadNextPageAppendsThroughViewModel() async {
        let client = FakeOrderClient()
        client.pages = [
            OrdersPage(items: [OrderFixtures.listItem(id: "a", statusValue: 2)], total: 2),
            OrdersPage(items: [OrderFixtures.listItem(id: "b", statusValue: 5)], total: 2)
        ]
        let (vm, _) = makeVM(client)
        await vm.pullToRefresh()
        XCTAssertTrue(vm.hasMore)

        await vm.loadNextPage()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["a", "b"])
        XCTAssertFalse(vm.hasMore)
    }

    func testFilterMatchesUpcomingCompletedCancelled() async {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [
            OrderFixtures.listItem(id: "new", statusValue: 0),
            OrderFixtures.listItem(id: "ontheway", statusValue: 3),
            OrderFixtures.listItem(id: "done", statusValue: 5),
            OrderFixtures.listItem(id: "cancelled", statusValue: 6)
        ], total: 4)]
        let (vm, _) = makeVM(client)
        await vm.pullToRefresh()

        vm.activeFilter = .upcoming
        XCTAssertEqual(Set(vm.filteredOrders.map(\.id)), ["new", "ontheway"])

        vm.activeFilter = .completed
        XCTAssertEqual(vm.filteredOrders.map(\.id), ["done"])

        vm.activeFilter = .cancelled
        XCTAssertEqual(vm.filteredOrders.map(\.id), ["cancelled"])

        XCTAssertEqual(vm.filterCount(.upcoming), 2)
    }
}
