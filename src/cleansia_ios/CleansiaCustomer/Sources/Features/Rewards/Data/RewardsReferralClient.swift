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
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerReferralAPI.referralGetMy().toDomain()
        }
    }

    func getMyReferrals(offset: Int, limit: Int) async -> ApiResult<ReferralListPage> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let paged = try await CustomerReferralAPI.referralGetMyReferrals(offset: offset, limit: limit)
            return try ReferralListPage(
                items: (paged.data ?? []).map { try $0.toDomain() },
                total: paged.total.require("total")
            )
        }
    }
}

/// **Refuse.** `code` is the whole point of the screen and the payload of its share sheet, so a
/// coerced `""` sends an invitation nobody can redeem — the one iOS instance of the site ADR-0048
/// §D4 names as having survived a spec-driven sweep, because every plain string on this wire is
/// declared nullable and the C# record is the only place that says otherwise.
extension GetMyReferralResponse {
    func toDomain() throws -> ReferralAccount {
        try ReferralAccount(
            code: code.requireNonBlank("code"),
            timesUsed: timesUsed.require("timesUsed"),
            qualifiedCount: qualifiedCount.require("qualifiedCount"),
            acceptedCount: acceptedCount.require("acceptedCount"),
            pointsPerReferral: pointsPerReferral.require("pointsPerReferral")
        )
    }
}

/// **Refuse the page.** `status` is what each row reads as — invited, accepted or qualified — and a
/// default of `1` reports a friend who earned the reward as one who merely signed up. Nothing here
/// is summed client-side, but the list has no rollup to protect and no identity worth dropping a row
/// over: a status the client cannot read is a row it cannot render honestly.
/// `pointsAwardedToReferrer` is `int?` by design — null until the referral qualifies.
private extension GetMyReferralsReferralListItem {
    func toDomain() throws -> ReferralListItem {
        try ReferralListItem(
            id: id,
            referredUserName: referredFirstName,
            status: status.require("status").rawValue,
            acceptedOn: acceptedOn,
            firstQualifyingOrderOn: firstQualifyingOrderOn,
            pointsAwardedToReferrer: pointsAwardedToReferrer
        )
    }
}
