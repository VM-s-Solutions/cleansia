#if DEBUG
    import CleansiaCore
    import CleansiaPartnerApi
    import Foundation

    final class PreviewPartnerOrderClient: PartnerOrderClient {
        func currentEmployeeId() async -> ApiResult<String> {
            .success("preview")
        }

        func getPaged(_: OrderPageQuery) async -> ApiResult<OrderPage> {
            .success(OrderPage(rows: [], total: 0))
        }

        func getById(orderId _: String) async -> ApiResult<OrderDetail> {
            .success(.preview)
        }

        func getMyAssignmentRemovalReason(orderId _: String) async -> ApiResult<String> {
            .success("")
        }

        func myPendingOffers() async -> ApiResult<[CleansiaPartnerApi.PendingOfferItem]> {
            .success([])
        }

        func declinePreferredOffer(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func takeOrder(orderId _: String, acceptedWorkContractTextId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func acceptWorkContract(orderId _: String, acceptedWorkContractTextId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func getWorkContractPreview(orderId _: String, language _: String) async -> ApiResult<WorkContract> {
            .success(.preview)
        }

        func getWorkContract(acceptanceId _: String, language _: String) async -> ApiResult<WorkContract> {
            .success(.preview)
        }

        func notifyOnTheWay(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func startOrder(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func markCashCollected(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func reportCashNotPaid(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func markMyPartDone(orderId _: String) async -> ApiResult<Bool> {
            .success(false)
        }

        func completeOrder(orderId _: String, actualMinutes _: Int?, notes _: String?) async -> ApiResult<Void> {
            .success(())
        }

        func dropOrder(orderId _: String) async -> ApiResult<Int> {
            .success(0)
        }

        func addNote(orderId _: String, content _: String) async -> ApiResult<Void> {
            .success(())
        }

        func updateNote(orderId _: String, noteId _: String, content _: String) async -> ApiResult<Void> {
            .success(())
        }

        func deleteNote(orderId _: String, noteId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func reportIssue(orderId _: String, description _: String) async -> ApiResult<Void> {
            .success(())
        }

        func updateIssue(orderId _: String, issueId _: String, description _: String) async -> ApiResult<Void> {
            .success(())
        }

        func deleteIssue(orderId _: String, issueId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func reportLockout(orderId _: String, callAttempts _: String) async -> ApiResult<Void> {
            .success(())
        }

        func getPhotos(orderId _: String) async -> ApiResult<[OrderPhoto]> {
            .success([])
        }

        func savePhoto(
            orderId _: String,
            photoType _: PhotoType,
            base64Content _: String,
            fileName _: String,
            contentType _: String
        ) async -> ApiResult<Void> {
            .success(())
        }

        func deletePhoto(photoId _: String) async -> ApiResult<Void> {
            .success(())
        }
    }
#endif
