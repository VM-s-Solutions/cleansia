import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct MembershipSnapshot: Equatable {
    let hasMembership: Bool
    let freeCancellationWindowHours: Int?
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
                freeCancellationWindowHours: response.freeCancellationWindowHours
            )
        }
    }
}
