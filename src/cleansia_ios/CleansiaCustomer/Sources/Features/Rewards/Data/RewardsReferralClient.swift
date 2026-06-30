import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The signed-in user's own referral surface (`Referral/GetMy` + `GetMyReferrals`).
/// Distinct from the booking-wizard `ReferralClient` (the anonymous `Referral/Validate`
/// passthrough) — that one validates a typed code, this one reads the user's account.
protocol RewardsReferralClient: Sendable {
    func getMy() async -> ApiResult<ReferralAccount>
    func getMyReferrals(offset: Int, limit: Int) async -> ApiResult<ReferralListPage>
}

struct LiveRewardsReferralClient: RewardsReferralClient {
    func getMy() async -> ApiResult<ReferralAccount> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerReferralAPI.referralGetMy()
        }
        return result.map { $0.toDomain() }
    }

    func getMyReferrals(offset: Int, limit: Int) async -> ApiResult<ReferralListPage> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerReferralAPI.referralGetMyReferrals(offset: offset, limit: limit)
        }
        return result.map { paged in
            ReferralListPage(items: (paged.data ?? []).map { $0.toDomain() }, total: paged.total ?? 0)
        }
    }
}

private extension GetMyReferralResponse {
    func toDomain() -> ReferralAccount {
        ReferralAccount(
            code: code ?? "",
            timesUsed: timesUsed ?? 0,
            qualifiedCount: qualifiedCount ?? 0,
            acceptedCount: acceptedCount ?? 0,
            pointsPerReferral: pointsPerReferral ?? 0
        )
    }
}

private extension GetMyReferralsReferralListItem {
    func toDomain() -> ReferralListItem {
        ReferralListItem(
            id: id,
            referredUserName: referredFirstName,
            status: status?.rawValue ?? 1,
            acceptedOn: acceptedOn,
            firstQualifyingOrderOn: firstQualifyingOrderOn,
            pointsAwardedToReferrer: pointsAwardedToReferrer
        )
    }
}
