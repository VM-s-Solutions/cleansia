import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class FakeMembershipClient: MembershipClient, @unchecked Sendable {
    var result: ApiResult<MembershipSnapshot>
    private(set) var callCount = 0

    init(result: ApiResult<MembershipSnapshot>) {
        self.result = result
    }

    func currentMembership() async -> ApiResult<MembershipSnapshot> {
        callCount += 1
        return result
    }
}

final class FakeServingCleanersClient: ServingCleanersClient, @unchecked Sendable {
    var result: ApiResult<[ServingCleaner]>
    private(set) var callCount = 0

    init(result: ApiResult<[ServingCleaner]> = .success([])) {
        self.result = result
    }

    func myServingCleaners() async -> ApiResult<[ServingCleaner]> {
        callCount += 1
        return result
    }
}

final class CancellationPolicyTests: XCTestCase {
    func testStandardPolicyWhenNoMembership() {
        let policy = CancellationPolicyBuilder.make(membership: nil)
        XCTAssertEqual(policy.freeHours, 24)
        XCTAssertEqual(policy.penaltyHours, 4)
        XCTAssertNil(policy.plusFreeHours)
        XCTAssertTrue(policy.showMidTier)
        XCTAssertFalse(policy.hasPlusPerk)
    }

    func testNonPlusMembershipUsesStandardWindow() {
        let policy = CancellationPolicyBuilder.make(
            membership: MembershipSnapshot(hasMembership: false, freeCancellationWindowHours: 48)
        )
        XCTAssertEqual(policy.freeHours, 24)
        XCTAssertNil(policy.plusFreeHours)
    }

    func testPlusWiderWindowBecomesPerk() {
        let policy = CancellationPolicyBuilder.make(
            membership: MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 48)
        )
        XCTAssertEqual(policy.freeHours, 48)
        XCTAssertEqual(policy.plusFreeHours, 48)
        XCTAssertTrue(policy.hasPlusPerk)
        XCTAssertTrue(policy.showMidTier)
    }

    func testPlusWindowNotWiderThanStandardIsNotAPerk() {
        let policy = CancellationPolicyBuilder.make(
            membership: MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 24)
        )
        XCTAssertEqual(policy.freeHours, 24)
        XCTAssertNil(policy.plusFreeHours)
        XCTAssertFalse(policy.hasPlusPerk)
    }

    func testZeroPlusWindowFallsBackToStandard() {
        let policy = CancellationPolicyBuilder.make(
            membership: MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 0)
        )
        XCTAssertEqual(policy.freeHours, 24)
        XCTAssertNil(policy.plusFreeHours)
    }
}

@MainActor
final class PreferredCleanerViewModelTests: XCTestCase {
    func testHiddenForNonPlusAndDoesNotFetchCleaners() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(hasMembership: false, freeCancellationWindowHours: nil)
        ))
        let cleaners = FakeServingCleanersClient(result: .success([ServingCleaner(id: "e-1", fullName: "Eva")]))
        let vm = PreferredCleanerViewModel(membershipClient: membership, cleanersClient: cleaners)

        await vm.load()

        XCTAssertFalse(vm.isVisible)
        XCTAssertFalse(vm.isPlus)
        XCTAssertEqual(cleaners.callCount, 0)
    }

    func testHiddenForPlusWithNoEligibleCleaners() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 48)
        ))
        let cleaners = FakeServingCleanersClient(result: .success([]))
        let vm = PreferredCleanerViewModel(membershipClient: membership, cleanersClient: cleaners)

        await vm.load()

        XCTAssertTrue(vm.isPlus)
        XCTAssertFalse(vm.isVisible)
    }

    func testVisibleForPlusWithCleaners() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 48)
        ))
        let cleaners = FakeServingCleanersClient(result: .success([
            ServingCleaner(id: "e-1", fullName: "Eva"),
            ServingCleaner(id: "e-2", fullName: "Petr")
        ]))
        let vm = PreferredCleanerViewModel(membershipClient: membership, cleanersClient: cleaners)

        await vm.load()

        XCTAssertTrue(vm.isVisible)
        XCTAssertEqual(vm.cleaners.count, 2)
    }

    func testCancellationPolicyReflectsPlusWindow() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 48)
        ))
        let vm = PreferredCleanerViewModel(
            membershipClient: membership,
            cleanersClient: FakeServingCleanersClient(result: .success([]))
        )

        await vm.load()

        XCTAssertEqual(vm.cancellationPolicy.freeHours, 48)
        XCTAssertTrue(vm.cancellationPolicy.hasPlusPerk)
    }

    func testLoadIsIdempotent() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 48)
        ))
        let cleaners = FakeServingCleanersClient(result: .success([ServingCleaner(id: "e-1", fullName: "Eva")]))
        let vm = PreferredCleanerViewModel(membershipClient: membership, cleanersClient: cleaners)

        await vm.load()
        await vm.load()

        XCTAssertEqual(membership.callCount, 1)
        XCTAssertEqual(cleaners.callCount, 1)
    }
}
