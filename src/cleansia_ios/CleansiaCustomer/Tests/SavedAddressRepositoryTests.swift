import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class SavedAddressRepositoryTests: XCTestCase {
    func testRefreshLoadsList() async {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "a"), SavedAddressFixtures.address(id: "b")]]
        let repo = SavedAddressRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(repo.addresses.map(\.id), ["a", "b"])
        XCTAssertTrue(repo.loaded)
    }

    func testRefreshFailureStaysUnloaded() async {
        let client = FakeSavedAddressClient()
        client.getMineError = ApiError(httpStatus: 500)
        let repo = SavedAddressRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNotNil(result.apiErrorOrNil)
        XCTAssertTrue(repo.addresses.isEmpty)
        XCTAssertFalse(repo.loaded)
    }

    func testAddSendsCoordsAndReloadsList() async {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "a")]]
        let repo = SavedAddressRepository(client: client)
        let draft = SavedAddressFixtures.geocoded(latitude: 50.1, longitude: 14.2).toDraft(
            label: "Work",
            setAsDefault: false
        )

        let result = await repo.add(draft)

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(client.addCallCount, 1)
        XCTAssertEqual(client.lastAddDraft?.latitude, 50.1)
        XCTAssertEqual(client.lastAddDraft?.longitude, 14.2)
        XCTAssertEqual(repo.addresses.map(\.id), ["a"])
    }

    func testAddFailureDoesNotReload() async {
        let client = FakeSavedAddressClient()
        client.addResult = .failure(ApiError(httpStatus: 400))
        let repo = SavedAddressRepository(client: client)
        let draft = SavedAddressFixtures.geocoded().toDraft(label: "Work", setAsDefault: false)

        let result = await repo.add(draft)

        XCTAssertNotNil(result.apiErrorOrNil)
        XCTAssertEqual(client.getMineCallCount, 0)
    }

    func testUpdateReloadsList() async {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "a", label: "Renamed")]]
        let repo = SavedAddressRepository(client: client)
        let draft = SavedAddressFixtures.geocoded().toDraft(label: "Renamed", setAsDefault: false)

        let result = await repo.update(id: "a", draft: draft)

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(client.lastUpdate?.id, "a")
        XCTAssertEqual(repo.addresses.first?.label, "Renamed")
    }

    func testSetDefaultRefetchesAndDefaultMoves() async {
        let client = FakeSavedAddressClient()
        client.pages = [
            [
                SavedAddressFixtures.address(id: "a", isDefault: true),
                SavedAddressFixtures.address(id: "b", isDefault: false)
            ],
            [
                SavedAddressFixtures.address(id: "a", isDefault: false),
                SavedAddressFixtures.address(id: "b", isDefault: true)
            ]
        ]
        let repo = SavedAddressRepository(client: client)
        await repo.refresh()

        let result = await repo.setDefault(id: "b")

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(client.lastSetDefaultId, "b")
        XCTAssertEqual(repo.addresses.first(where: { $0.id == "b" })?.isDefault, true)
        XCTAssertEqual(repo.addresses.first(where: { $0.id == "a" })?.isDefault, false)
    }

    func testDeleteRefetchesListWithoutReturnedId() async {
        let client = FakeSavedAddressClient()
        client.pages = [
            [SavedAddressFixtures.address(id: "a"), SavedAddressFixtures.address(id: "b")],
            [SavedAddressFixtures.address(id: "b")]
        ]
        let repo = SavedAddressRepository(client: client)
        await repo.refresh()

        let result = await repo.delete(id: "a")

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(client.lastDeleteId, "a")
        XCTAssertEqual(repo.addresses.map(\.id), ["b"])
    }

    func testDeleteFailureDoesNotReload() async {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "a")]]
        let repo = SavedAddressRepository(client: client)
        await repo.refresh()
        client.deleteResult = .failure(ApiError(httpStatus: 404))

        let result = await repo.delete(id: "a")

        XCTAssertNotNil(result.apiErrorOrNil)
        XCTAssertEqual(repo.addresses.map(\.id), ["a"])
    }

    func testClearWipesCache() async {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "a")]]
        let repo = SavedAddressRepository(client: client)
        await repo.refresh()

        await repo.clear()

        XCTAssertTrue(repo.addresses.isEmpty)
        XCTAssertFalse(repo.loaded)
    }

    private func isolatedDefaults() throws -> UserDefaults {
        let suiteName = "saved-address-tests-\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        addTeardownBlock { defaults.removePersistentDomain(forName: suiteName) }
        return defaults
    }

    func testSetSelectedPersistsAcrossInstances() throws {
        let defaults = try isolatedDefaults()
        let repo = SavedAddressRepository(client: FakeSavedAddressClient(), defaults: defaults)

        repo.setSelected("a")

        XCTAssertEqual(repo.selectedId, "a")
        let rehydrated = SavedAddressRepository(client: FakeSavedAddressClient(), defaults: defaults)
        XCTAssertEqual(rehydrated.selectedId, "a")
    }

    func testDeletingTheSelectedAddressClearsTheSelection() async throws {
        let defaults = try isolatedDefaults()
        let client = FakeSavedAddressClient()
        client.pages = [
            [SavedAddressFixtures.address(id: "a"), SavedAddressFixtures.address(id: "b")],
            [SavedAddressFixtures.address(id: "b")]
        ]
        let repo = SavedAddressRepository(client: client, defaults: defaults)
        await repo.refresh()
        repo.setSelected("a")

        await repo.delete(id: "a")

        XCTAssertNil(repo.selectedId)
        XCTAssertNil(defaults.string(forKey: "saved_address_selected_id"))
    }

    func testDeletingAnotherAddressKeepsTheSelection() async throws {
        let defaults = try isolatedDefaults()
        let client = FakeSavedAddressClient()
        client.pages = [
            [SavedAddressFixtures.address(id: "a"), SavedAddressFixtures.address(id: "b")],
            [SavedAddressFixtures.address(id: "a")]
        ]
        let repo = SavedAddressRepository(client: client, defaults: defaults)
        await repo.refresh()
        repo.setSelected("a")

        await repo.delete(id: "b")

        XCTAssertEqual(repo.selectedId, "a")
    }

    func testClearWipesTheSelection() async throws {
        let defaults = try isolatedDefaults()
        let repo = SavedAddressRepository(client: FakeSavedAddressClient(), defaults: defaults)
        repo.setSelected("a")

        await repo.clear()

        XCTAssertNil(repo.selectedId)
        XCTAssertNil(defaults.string(forKey: "saved_address_selected_id"))
    }
}
