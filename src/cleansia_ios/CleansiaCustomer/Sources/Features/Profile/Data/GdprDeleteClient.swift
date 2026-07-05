import CleansiaCore
import CleansiaCustomerApi
import Foundation

enum GdprDeletionBlock: Equatable {
    case blockedByOrder
    case blockedByInvoice
    case alreadyPending

    init?(code: String?) {
        switch code {
        case "gdpr.deletion_blocked_by_order": self = .blockedByOrder
        case "gdpr.deletion_blocked_by_invoice": self = .blockedByInvoice
        case "gdpr.deletion_already_pending": self = .alreadyPending
        default: return nil
        }
    }
}

protocol GdprDeleteClient: AnyObject {
    func deleteMyAccount() async -> ApiResult<Void>
}

final class LiveGdprDeleteClient: GdprDeleteClient {
    func deleteMyAccount() async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerGdprAPI.gdprDeleteMyAccount()
        }
    }
}
