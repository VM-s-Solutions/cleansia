import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeRecurringBookingClient: RecurringBookingClient, @unchecked Sendable {
    var mineResults: [ApiResult<[RecurringTemplate]>] = [.success([])]
    private(set) var mineCallCount = 0

    var createResult: ApiResult<RecurringTemplate> = .success(RecurringFixtures.template())
    private(set) var createInputs: [CreateRecurringInput] = []

    var setActiveResult: ApiResult<Void> = .success(())
    private(set) var setActiveCalls: [(id: String, active: Bool)] = []

    var deleteResult: ApiResult<Void> = .success(())
    private(set) var deletedIds: [String] = []

    func getMine() async -> ApiResult<[RecurringTemplate]> {
        defer { mineCallCount += 1 }
        let index = min(mineCallCount, mineResults.count - 1)
        guard index >= 0 else { return .success([]) }
        return mineResults[index]
    }

    func create(_ input: CreateRecurringInput) async -> ApiResult<RecurringTemplate> {
        createInputs.append(input)
        return createResult
    }

    func setActive(templateId: String, isActive: Bool) async -> ApiResult<Void> {
        setActiveCalls.append((templateId, isActive))
        return setActiveResult
    }

    func delete(templateId: String) async -> ApiResult<Void> {
        deletedIds.append(templateId)
        return deleteResult
    }
}

final class FakeRecurringSavedAddressClient: RecurringSavedAddressClient, @unchecked Sendable {
    var result: ApiResult<[RecurringSavedAddress]> = .success([])
    private(set) var callCount = 0

    func getMine() async -> ApiResult<[RecurringSavedAddress]> {
        callCount += 1
        return result
    }
}

enum RecurringFixtures {
    static func template(
        id: String = "tpl-1",
        isActive: Bool = true,
        frequency: Int = 1
    ) -> RecurringTemplate {
        RecurringTemplate(
            id: id,
            frequency: frequency,
            dayOfWeek: 4,
            timeOfDay: "10:00",
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "addr-1",
            addressLine: "Zenklova 6, Praha",
            selectedServiceIds: ["svc-1"],
            selectedPackageIds: [],
            paymentType: 1,
            startsOn: Date(timeIntervalSince1970: 1_780_000_000),
            endsOn: nil,
            isActive: isActive
        )
    }
}
