import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// `GET Employee/GetMyPayoutDetails` answers HTTP 400 `payout.not_found` for a cleaner who
/// has never saved a payout destination, so the normal first-time case arrives as a failure.
/// Normalized here, once, so "nothing recorded yet" (`success(nil)`) and "the read failed"
/// stay distinguishable everywhere above: left raw, a caller either shows a first-time
/// cleaner an error screen or treats every failure as empty and offers to overwrite a
/// destination it could not read.
enum PayoutDetailsRead {
    static let notFoundKey = "payout.not_found"

    static func normalize(_ result: ApiResult<MyPayoutDetails>) -> ApiResult<MyPayoutDetails?> {
        switch result {
        case let .success(details):
            .success(details)
        case let .failure(error) where isNotFound(error):
            .success(nil)
        case let .failure(error):
            .failure(error)
        }
    }

    static func isNotFound(_ error: ApiError) -> Bool {
        guard error.httpStatus == 400 else { return false }
        return error.code == notFoundKey || error.message?.contains(notFoundKey) == true
    }
}
