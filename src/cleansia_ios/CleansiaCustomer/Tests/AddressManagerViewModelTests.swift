import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class AddressManagerViewModelTests: XCTestCase {
    /// Isolated defaults: the repo persists the selection, and .standard would
    /// leak into the test host's real defaults (and across tests on a crash).
    private func isolatedDefaults() throws -> UserDefaults {
        let suiteName = "address-manager-vm-tests-\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        addTeardownBlock { defaults.removePersistentDomain(forName: suiteName) }
        return defaults
    }

    private func makeVM(_ client: FakeSavedAddressClient) throws -> (AddressManagerViewModel, SavedAddressRepository) {
        let repo = try SavedAddressRepository(client: client, defaults: isolatedDefaults())
        let vm = AddressManagerViewModel(
            repository: repo,
            geocoding: StubGeocodingService(),
            mapProvider: PreviewMapProvider(),
            snackbar: SnackbarController()
        )
        return (vm, repo)
    }

    func testOnAppearMirrorsRepositoryList() async throws {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "a")]]
        let (vm, _) = try makeVM(client)

        await vm.onAppear()

        XCTAssertEqual(vm.addresses.map(\.id), ["a"])
        XCTAssertTrue(vm.loaded)
    }

    func testStartAddOpensMapPaneAndClearsPick() throws {
        let client = FakeSavedAddressClient()
        let (vm, _) = try makeVM(client)

        vm.startAdd()

        XCTAssertEqual(vm.pane, .addOnMap)
        XCTAssertNil(vm.pickedAddress)
    }

    func testMapConfirmPopulatesDraftAndAdvancesToReview() throws {
        let client = FakeSavedAddressClient()
        let (vm, _) = try makeVM(client)
        vm.startAdd()
        let picked = SavedAddressFixtures.geocoded(street: "Karlova 5", city: "Prague")

        vm.mapDidConfirm(picked)

        XCTAssertEqual(vm.pane, .reviewNew)
        XCTAssertEqual(vm.pickedAddress?.street, "Karlova 5")
        XCTAssertEqual(vm.pickedAddress?.city, "Prague")
    }

    func testFullFlowListToMapToReviewToSaveReturnsToList() async throws {
        let client = FakeSavedAddressClient()
        client.pages = [[SavedAddressFixtures.address(id: "saved")]]
        let (vm, _) = try makeVM(client)
        XCTAssertEqual(vm.pane, .list)

        vm.startAdd()
        XCTAssertEqual(vm.pane, .addOnMap)

        vm.mapDidConfirm(SavedAddressFixtures.geocoded(latitude: 50.2, longitude: 14.3))
        XCTAssertEqual(vm.pane, .reviewNew)

        await vm.saveReviewed(label: "Office", setAsDefault: true)

        XCTAssertEqual(vm.pane, .list)
        XCTAssertNil(vm.pickedAddress)
        XCTAssertEqual(client.addCallCount, 1)
        XCTAssertEqual(client.lastAddDraft?.label, "Office")
        XCTAssertEqual(client.lastAddDraft?.setAsDefault, true)
        XCTAssertEqual(client.lastAddDraft?.latitude, 50.2)
        XCTAssertEqual(vm.addresses.map(\.id), ["saved"])
    }

    func testSaveWithBlankLabelFallsBackToDefaultLabel() async throws {
        let client = FakeSavedAddressClient()
        client.pages = [[]]
        let (vm, _) = try makeVM(client)
        vm.startAdd()
        vm.mapDidConfirm(SavedAddressFixtures.geocoded())

        await vm.saveReviewed(label: "   ", setAsDefault: false)

        XCTAssertEqual(client.lastAddDraft?.label, L10n.AddressManager.fallbackLabel)
    }

    func testBackToListReturnsToListPane() throws {
        let client = FakeSavedAddressClient()
        let (vm, _) = try makeVM(client)
        vm.startAdd()

        vm.backToList()

        XCTAssertEqual(vm.pane, .list)
    }

    func testBackToMapReturnsFromReview() throws {
        let client = FakeSavedAddressClient()
        let (vm, _) = try makeVM(client)
        vm.startAdd()
        vm.mapDidConfirm(SavedAddressFixtures.geocoded())

        vm.backToMap()

        XCTAssertEqual(vm.pane, .addOnMap)
    }

    func testSetDefaultDelegatesToRepository() async throws {
        let client = FakeSavedAddressClient()
        client.pages = [
            [SavedAddressFixtures.address(id: "a", isDefault: true), SavedAddressFixtures.address(id: "b")],
            [SavedAddressFixtures.address(id: "a"), SavedAddressFixtures.address(id: "b", isDefault: true)]
        ]
        let (vm, _) = try makeVM(client)
        await vm.onAppear()

        await vm.setDefault(id: "b")

        XCTAssertEqual(client.lastSetDefaultId, "b")
        XCTAssertEqual(vm.addresses.first(where: { $0.id == "b" })?.isDefault, true)
    }

    func testDeleteDelegatesToRepository() async throws {
        let client = FakeSavedAddressClient()
        client.pages = [
            [SavedAddressFixtures.address(id: "a"), SavedAddressFixtures.address(id: "b")],
            [SavedAddressFixtures.address(id: "b")]
        ]
        let (vm, _) = try makeVM(client)
        await vm.onAppear()

        await vm.delete(id: "a")

        XCTAssertEqual(client.lastDeleteId, "a")
        XCTAssertEqual(vm.addresses.map(\.id), ["b"])
    }

    func testRenameUsesExistingCoordsAndDraftLabel() async throws {
        let client = FakeSavedAddressClient()
        client.pages = [
            [SavedAddressFixtures.address(id: "a", label: "Home", latitude: 49.5, longitude: 13.5)],
            [SavedAddressFixtures.address(id: "a", label: "Casa", latitude: 49.5, longitude: 13.5)]
        ]
        let (vm, _) = try makeVM(client)
        await vm.onAppear()

        await vm.rename(id: "a", newLabel: "Casa")

        XCTAssertEqual(client.lastUpdate?.id, "a")
        XCTAssertEqual(client.lastUpdate?.draft.label, "Casa")
        XCTAssertEqual(client.lastUpdate?.draft.latitude, 49.5)
        XCTAssertEqual(vm.addresses.first?.label, "Casa")
    }

    func testSelectPersistsTheSelectionOnTheRepository() throws {
        let client = FakeSavedAddressClient()
        let (vm, repo) = try makeVM(client)

        vm.select(SavedAddressFixtures.address(id: "a"))

        XCTAssertEqual(repo.selectedId, "a")
        XCTAssertEqual(vm.selectedId, "a")
    }
}
