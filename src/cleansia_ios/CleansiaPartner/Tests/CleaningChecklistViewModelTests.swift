import CleansiaCore
import XCTest
@testable import CleansiaPartner

@MainActor
final class CleaningChecklistViewModelTests: XCTestCase {
    private var store: UserDefaultsCleaningChecklistStore!
    private var defaults: UserDefaults!

    override func setUp() {
        super.setUp()
        defaults = UserDefaults(suiteName: "checklist-tests-\(UUID().uuidString)")
        store = UserDefaultsCleaningChecklistStore(defaults: defaults)
    }

    func testStartsEmpty() {
        let vm = CleaningChecklistViewModel(orderId: "o1", store: store)
        XCTAssertTrue(vm.checkedIds.isEmpty)
    }

    func testSetCheckedAddsAndRemoves() {
        let vm = CleaningChecklistViewModel(orderId: "o1", store: store)
        vm.setChecked("item-a", true)
        XCTAssertEqual(vm.checkedIds, ["item-a"])
        vm.setChecked("item-b", true)
        XCTAssertEqual(vm.checkedIds, ["item-a", "item-b"])
        vm.setChecked("item-a", false)
        XCTAssertEqual(vm.checkedIds, ["item-b"])
    }

    func testPersistsAcrossAFreshViewModel() {
        let first = CleaningChecklistViewModel(orderId: "o1", store: store)
        first.setChecked("item-a", true)

        // A fresh VM over the same store (process-death surrogate) restores it.
        let second = CleaningChecklistViewModel(orderId: "o1", store: store)
        XCTAssertEqual(second.checkedIds, ["item-a"])
    }

    func testKeyedByOrderIdNoCollision() {
        let orderA = CleaningChecklistViewModel(orderId: "order-a", store: store)
        orderA.setChecked("item", true)
        let orderB = CleaningChecklistViewModel(orderId: "order-b", store: store)
        XCTAssertTrue(orderB.checkedIds.isEmpty)
        XCTAssertEqual(orderA.checkedIds, ["item"])
    }

    func testSessionClearRemovesEveryChecklistAndPreservesDeviceSettings() async {
        let settings = UserDefaultsAppSettingsStore(defaults: defaults)
        settings.setLanguage("cs")
        settings.setTheme(.dark)
        settings.setMarket(isoCode: "CZE")
        settings.markOnboardingSeen(userId: "user-a")
        defaults.set("keep", forKey: "order_checklist_without_dot")
        store.setChecked(orderId: "order-a", itemId: "a", checked: true)
        store.setChecked(orderId: "order-b", itemId: "b", checked: true)
        let registry = SessionScopedCacheRegistry()
        if let cache = (store as AnyObject) as? SessionScopedCache { registry.register(cache) }

        await registry.clearAll()

        XCTAssertTrue(store.checkedIds(orderId: "order-a").isEmpty)
        XCTAssertTrue(store.checkedIds(orderId: "order-b").isEmpty)
        XCTAssertEqual(settings.persistedLanguageTag, "cs")
        XCTAssertEqual(settings.theme, .dark)
        XCTAssertEqual(settings.marketIsoCode, "CZE")
        XCTAssertTrue(settings.hasSeenOnboarding(userId: "user-a"))
        XCTAssertEqual(defaults.string(forKey: "order_checklist_without_dot"), "keep")
    }

    func testNormalLogoutClearsTheProductionChecklistStore() async throws {
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let orderId = "checklist-logout-" + UUID().uuidString
            defer { UserDefaults.standard.removeObject(forKey: "order_checklist." + orderId) }
            container.cleaningChecklistStore.setChecked(orderId: orderId, itemId: "tick", checked: true)
            let tokens = SessionTokenStore(signedIn: true)
            let current = try XCTUnwrap(tokens.current())
            tokens.save(AuthTokens(
                accessToken: current.accessToken, accessTokenExpiresAt: current.accessTokenExpiresAt,
                refreshToken: "", refreshTokenExpiresAt: current.refreshTokenExpiresAt
            ))
            let auth = AuthApiClient(
                apiBaseURL: container.apiBaseURL, tokenStore: tokens,
                headerAdapter: HeaderAdapter(deviceIdProvider: ChecklistDevice()),
                sessionScopedCaches: container.sessionScopedCaches
            )

            await auth.logout()

            XCTAssertNil(tokens.current())
            assertEmptyChecklist(container, orderId: orderId, trigger: "normalLogout", sample: sample)
        }
    }

    func testTerminalRefreshClearsTheProductionChecklistStore() async throws {
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let orderId = "checklist-terminal-" + UUID().uuidString
            defer { UserDefaults.standard.removeObject(forKey: "order_checklist." + orderId) }
            container.cleaningChecklistStore.setChecked(orderId: orderId, itemId: "tick", checked: true)
            let tokens = SessionTokenStore(signedIn: true)
            let refresher = SessionRefresher(
                tokenStore: tokens, refreshClient: ChecklistRejectedRefresh(),
                sessionManager: container.sessionManager, sessionScopedCaches: container.sessionScopedCaches
            )

            let outcome = await refresher.refresh(triggeredBy: "access")

            XCTAssertEqual(outcome, .signedOut)
            XCTAssertNil(tokens.current())
            assertEmptyChecklist(container, orderId: orderId, trigger: "terminalRefresh", sample: sample)
        }
    }

    func testPartnerDeletionRequestKeepsChecklistAndDoesNotResubmit() async throws {
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let orderId = "checklist-deletion-request-" + UUID().uuidString
            defer { UserDefaults.standard.removeObject(forKey: "order_checklist." + orderId) }
            container.cleaningChecklistStore.setChecked(orderId: orderId, itemId: "tick", checked: true)
            let client = ChecklistDeletionRequest()
            let viewModel = DeleteAccountViewModel(client: client, snackbar: SnackbarController())

            await viewModel.submit()
            await viewModel.submit()

            XCTAssertTrue(viewModel.requested)
            XCTAssertEqual(client.callCount, 1)
            XCTAssertEqual(container.cleaningChecklistStore.checkedIds(orderId: orderId), ["tick"])
            print("CHECKLIST_SESSION trigger=pendingDeletionRequest sample=\(sample) retainedTicks=1")
        }
    }

    private func makeContainer() throws -> PartnerAppContainer {
        try PartnerAppContainer(
            snackbar: SnackbarController(), apiBaseURL: XCTUnwrap(URL(string: "http://127.0.0.1:1/"))
        )
    }

    private func assertEmptyChecklist(
        _ container: PartnerAppContainer, orderId: String, trigger: String, sample: Int
    ) {
        let restored = CleaningChecklistViewModel(orderId: orderId, store: container.cleaningChecklistStore)
        print("CHECKLIST_SESSION trigger=\(trigger) sample=\(sample) retainedTicks=\(restored.checkedIds.count)")
        XCTAssertTrue(restored.checkedIds.isEmpty)
    }
}

private struct ChecklistDevice: DeviceIdProviding {
    let deviceId = "00000000-0000-4000-8000-000000000000"
}

private struct ChecklistRejectedRefresh: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .rejected
    }
}

private final class ChecklistDeletionRequest: PartnerGdprDeletionClient {
    private(set) var callCount = 0

    func requestDeletion() async -> ApiResult<Void> {
        callCount += 1
        return .success(())
    }
}
