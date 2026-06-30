import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class RecurringBookingsViewModelTests: XCTestCase {
    private func makeVM(
        client: FakeRecurringBookingClient = FakeRecurringBookingClient(),
        membership: MyMembership = MembershipFixtures.active
    ) -> (RecurringBookingsViewModel, RecurringBookingRepository, MembershipRepository) {
        let repo = RecurringBookingRepository(client: client)
        let memClient = FakeMembershipManagementClient()
        memClient.mineResults = [.success(membership)]
        let memRepo = MembershipRepository(client: memClient)
        let vm = RecurringBookingsViewModel(
            repository: repo,
            membershipRepository: memRepo,
            snackbar: SnackbarController()
        )
        return (vm, repo, memRepo)
    }

    func testLoadPopulatesTemplates() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template()])]
        let (vm, _, _) = makeVM(client: client)

        await vm.load()

        XCTAssertEqual(vm.templates.count, 1)
        XCTAssertEqual(vm.templates.first?.id, "tpl-1")
    }

    func testToggleActiveFlipsAndRefetches() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [
            .success([RecurringFixtures.template(isActive: true)]),
            .success([RecurringFixtures.template(isActive: false)])
        ]
        let (vm, _, _) = makeVM(client: client)
        await vm.load()

        await vm.toggleActive(templateId: "tpl-1", currentlyActive: true)

        XCTAssertEqual(client.setActiveCalls.map(\.active), [false])
        XCTAssertEqual(vm.templates.first?.isActive, false)
    }

    func testDeleteRemovesAndRefetches() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [
            .success([RecurringFixtures.template()]),
            .success([])
        ]
        let (vm, _, _) = makeVM(client: client)
        await vm.load()

        await vm.delete(templateId: "tpl-1")

        XCTAssertEqual(client.deletedIds, ["tpl-1"])
        XCTAssertTrue(vm.templates.isEmpty)
    }

    func testPlusGateTrueForMember() async {
        let (vm, _, memRepo) = makeVM(membership: MembershipFixtures.active)
        await memRepo.refresh()
        XCTAssertTrue(vm.isPlusMember)
    }

    func testPlusGateFalseForNonMember() async {
        let (vm, _, memRepo) = makeVM(membership: MembershipFixtures.inactive)
        await memRepo.refresh()
        XCTAssertFalse(vm.isPlusMember)
    }
}
