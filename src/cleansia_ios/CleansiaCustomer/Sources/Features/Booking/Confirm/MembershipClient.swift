import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct MembershipSnapshot: Equatable {
    let hasMembership: Bool
    let freeCancellationWindowHours: Int?
    let expressUpgradesPerMonth: Int?
    let expressUpgradesRemaining: Int?
    let trialEndsAtUtc: Date?

    init(
        hasMembership: Bool,
        freeCancellationWindowHours: Int?,
        expressUpgradesPerMonth: Int? = nil,
        expressUpgradesRemaining: Int? = nil,
        trialEndsAtUtc: Date? = nil
    ) {
        self.hasMembership = hasMembership
        self.freeCancellationWindowHours = freeCancellationWindowHours
        self.expressUpgradesPerMonth = expressUpgradesPerMonth
        self.expressUpgradesRemaining = expressUpgradesRemaining
        self.trialEndsAtUtc = trialEndsAtUtc
    }
}

protocol MembershipClient {
    func currentMembership() async -> ApiResult<MembershipSnapshot>
}

struct LiveMembershipClient: MembershipClient {
    func currentMembership() async -> ApiResult<MembershipSnapshot> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipGetMine()
        }
        return result.map { response in
            MembershipSnapshot(
                hasMembership: response.hasMembership ?? false,
                freeCancellationWindowHours: response.freeCancellationWindowHours,
                expressUpgradesPerMonth: response.expressUpgradesPerMonth,
                expressUpgradesRemaining: response.expressUpgradesRemaining,
                trialEndsAtUtc: response.trialEndsAtUtc
            )
        }
    }
}
