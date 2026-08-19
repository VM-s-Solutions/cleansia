import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// The refusals a cleaner's deletion request can come back with.
///
/// Two more than the customer app's equivalent, and they are the two that only exist for someone who
/// has an `Employee`: still staffed on a job, or still owed pay. → /decisions/adr-0052
enum PartnerDeletionBlock: Equatable {
    case blockedByAssignedOrder
    case blockedByUnsettledPay
    case blockedByInvoice
    case blockedByOrder
    case alreadyPending

    init?(code: String?) {
        switch code {
        case "gdpr.deletion_blocked_by_assigned_order": self = .blockedByAssignedOrder
        case "gdpr.deletion_blocked_by_unsettled_pay": self = .blockedByUnsettledPay
        case "gdpr.deletion_blocked_by_invoice": self = .blockedByInvoice
        case "gdpr.deletion_blocked_by_order": self = .blockedByOrder
        case "gdpr.deletion_already_pending": self = .alreadyPending
        default: return nil
        }
    }
}

/// `POST /api/v1/Gdpr/delete-account` for the partner app.
///
/// The route is shared with the customer app and its name is the customer's, but for a caller who
/// has an `Employee` the server does something different: it FILES a Pending deletion request and
/// changes nothing else. Ending a working relationship needs signed paperwork and an in-person step,
/// and the records that survive it are financial rather than the subject's to delete.
///
/// Two consequences the caller must honour, neither of them visible in the response:
///
/// - **The session is not ended.** Unlike the customer flow, nothing is wiped and no sign-out
///   happens. The cleaner keeps working until an admin fulfils the request.
/// - **Success means "requested", not "deleted".**
///
/// A second call while one is pending is refused with `gdpr.deletion_already_pending`, which is the
/// idempotency this flow relies on rather than tracking state locally.
protocol PartnerGdprDeletionClient: AnyObject {
    func requestDeletion() async -> ApiResult<Void>
}

final class LivePartnerGdprDeletionClient: PartnerGdprDeletionClient {
    func requestDeletion() async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerGdprAPI.gdprDeleteMyAccount()
        }
    }
}
