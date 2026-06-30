import CleansiaCore
import CleansiaCustomerApi
import Foundation

@MainActor
final class OrderPhotosViewModel: ViewModel {
    @Published private(set) var state: UiState<GetOrderPhotosResponse> = .loading

    private let orderId: String
    private let client: OrderClient
    private let snackbar: SnackbarController

    init(orderId: String, client: OrderClient, snackbar: SnackbarController) {
        self.orderId = orderId
        self.client = client
        self.snackbar = snackbar
    }

    /// Fetched fresh every open — SAS URLs in `blobUrl` carry a ~1h TTL, so
    /// caching would risk serving stale signed URLs (`OrderPhotosViewModel.kt`).
    func load() async {
        guard !orderId.isBlank else {
            state = .error(ApiError(code: "missing_order_id"))
            return
        }
        state = .loading
        switch await client.getPhotos(orderId: orderId) {
        case let .success(response):
            state = .loaded(response)
        case let .failure(error):
            snackbar.showApiError(error)
            state = .error(error)
        }
    }
}
