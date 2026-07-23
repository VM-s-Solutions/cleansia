import CleansiaCore
import CleansiaPartnerApi
import Combine
import SwiftUI

/// Per-rail mutation substate alongside the loaded photo list: `isUploading`
/// drives the Add-tile spinner, `deletingId` drives the spinner on the tile
/// being removed (the `PhotoMutationState` parity).
struct PhotoMutationState: Equatable {
    var isUploading = false
    var deletingId: String?
}

/// Backs the Photos section's before/after rails: load / upload / delete over
/// the order client. The list is owned here (its own `getPhotos`); each
/// successful mutation fires `mutated` so the parent re-fetches the order and
/// the server-recomputed `hasAfterPhotos` flows back into the Complete gate. The
/// VM never navigates.
@MainActor
final class OrderPhotosViewModel: ViewModel {
    @Published private(set) var state: UiState<[OrderPhoto]> = .loading
    @Published private(set) var mutation = PhotoMutationState()

    let mutated = PassthroughSubject<Void, Never>()

    private let orderId: String
    private let client: PartnerOrderClient
    private let snackbar: SnackbarController

    init(orderId: String, client: PartnerOrderClient, snackbar: SnackbarController) {
        self.orderId = orderId
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        if state.loadedValue == nil {
            state = .loading
        }
        // Acts only on the orderId this VM was constructed with.
        switch await client.getPhotos(orderId: orderId) {
        case let .success(photos):
            state = .loaded(photos)
        case let .failure(error):
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    func upload(type: PhotoType, image: UIImage) async {
        guard !mutation.isUploading else { return } // drop a second upload while one is in flight

        mutation.isUploading = true
        // Compression + base64 is heavy — run it off the main actor.
        let encoded = await Task.detached(priority: .userInitiated) {
            ImageCompressor.encode(image)
        }.value

        guard let encoded else {
            mutation.isUploading = false
            snackbar.showError(L10n.Orders.photoEncodeFailed)
            return
        }

        // The command carries only orderId + the photo — no client employeeId; the
        // server resolves the actor from the JWT and enforces order ownership.
        let result = await client.savePhoto(
            orderId: orderId,
            photoType: type,
            base64Content: encoded.base64,
            fileName: encoded.fileName,
            contentType: encoded.contentType
        )
        mutation.isUploading = false
        await finish(result)
    }

    func delete(photoId: String) async {
        guard mutation.deletingId == nil else { return } // drop a second delete while one is in flight
        // photoId comes from this VM's own getPhotos response, never synthesized.
        mutation.deletingId = photoId
        let result = await client.deletePhoto(photoId: photoId)
        mutation.deletingId = nil
        await finish(result)
    }

    private func finish(_ result: ApiResult<Void>) async {
        switch result {
        case .success:
            mutated.send()
            await load()
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }
}

#if DEBUG
    extension OrderPhotosViewModel {
        static var preview: OrderPhotosViewModel {
            OrderPhotosViewModel(orderId: "preview", client: PreviewPhotosClient(), snackbar: SnackbarController())
        }
    }

    private final class PreviewPhotosClient: PartnerOrderClient {
        func currentEmployeeId() async -> ApiResult<String> {
            .success("preview")
        }

        func getPaged(_: OrderPageQuery) async -> ApiResult<[OrderListItem]> {
            .success([])
        }

        func getById(orderId _: String) async -> ApiResult<OrderItem> {
            .success(OrderItem())
        }

        func takeOrder(orderId _: String) async -> ApiResult<Void> {
            .success(())
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

        func completeOrder(orderId _: String, actualMinutes _: Int?, notes _: String?) async -> ApiResult<Void> {
            .success(())
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
