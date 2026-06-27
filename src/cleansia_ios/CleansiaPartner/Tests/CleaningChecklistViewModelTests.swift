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
        let a = CleaningChecklistViewModel(orderId: "order-a", store: store)
        a.setChecked("item", true)
        let b = CleaningChecklistViewModel(orderId: "order-b", store: store)
        XCTAssertTrue(b.checkedIds.isEmpty)
        XCTAssertEqual(a.checkedIds, ["item"])
    }
}
