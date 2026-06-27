import CleansiaCore
import CleansiaPartnerApi
import UIKit
import XCTest
@testable import CleansiaPartner

@MainActor
final class OrderPhotosViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        snackbar = SnackbarController()
    }

    private func makeVM(orderId: String = "order-1") -> OrderPhotosViewModel {
        OrderPhotosViewModel(orderId: orderId, client: client, snackbar: snackbar)
    }

    private func image() -> UIImage {
        let size = CGSize(width: 32, height: 32)
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = 1
        return UIGraphicsImageRenderer(size: size, format: format).image { context in
            UIColor.systemTeal.setFill()
            context.fill(CGRect(origin: .zero, size: size))
        }
    }

    // MARK: TC-IOS-PHOTOS-UPLOAD

    func testLoadTransitionsLoadingToLoaded() async {
        client.getPhotosResult = .success([OrderPhoto(id: "p1", photoType: ._1, blobUrl: nil)])
        let vm = makeVM()
        XCTAssertTrue(vm.state.isLoading)

        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["p1"])
    }

    func testLoadFailureWithNoCacheTransitionsToErrorAndSnackbars() async {
        client.getPhotosResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testUploadSuccessClearsUploadingFiresMutatedAndRefetches() async {
        client.getPhotosResult = .success([])
        let vm = makeVM()
        await vm.load()
        let fetchesBefore = client.getPhotosCallCount

        var mutatedFired = false
        let cancellable = vm.mutated.sink { mutatedFired = true }
        defer { cancellable.cancel() }

        await vm.upload(type: ._2, image: image())

        XCTAssertFalse(vm.mutation.isUploading)
        XCTAssertTrue(mutatedFired)
        XCTAssertEqual(client.getPhotosCallCount, fetchesBefore + 1) // post-success refetch
        XCTAssertEqual(client.photoCommands.map(\.name), ["savePhoto"])
        XCTAssertEqual(client.photoCommands.first?.photoType, ._2)
        XCTAssertEqual(client.photoCommands.first?.hasBase64, true)
    }

    func testUploadFailureClearsUploadingWithoutMutatedAndSnackbars() async {
        client.getPhotosResult = .success([])
        client.commandResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        var mutatedFired = false
        let cancellable = vm.mutated.sink { mutatedFired = true }
        defer { cancellable.cancel() }

        await vm.upload(type: ._1, image: image())

        XCTAssertFalse(vm.mutation.isUploading)
        XCTAssertFalse(mutatedFired)
        XCTAssertNotNil(snackbar.current)
    }

    func testDeleteSuccessClearsDeletingIdAndFiresMutated() async {
        client.getPhotosResult = .success([OrderPhoto(id: "p1", photoType: ._1, blobUrl: nil)])
        let vm = makeVM()
        await vm.load()

        var mutatedFired = false
        let cancellable = vm.mutated.sink { mutatedFired = true }
        defer { cancellable.cancel() }

        await vm.delete(photoId: "p1")

        XCTAssertNil(vm.mutation.deletingId)
        XCTAssertTrue(mutatedFired)
        XCTAssertEqual(client.photoCommands.map(\.name), ["deletePhoto"])
    }

    func testDeleteFailureClearsDeletingIdWithoutMutatedAndSnackbars() async {
        client.getPhotosResult = .success([OrderPhoto(id: "p1", photoType: ._1, blobUrl: nil)])
        client.commandResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        var mutatedFired = false
        let cancellable = vm.mutated.sink { mutatedFired = true }
        defer { cancellable.cancel() }

        await vm.delete(photoId: "p1")

        XCTAssertNil(vm.mutation.deletingId)
        XCTAssertFalse(mutatedFired)
        XCTAssertNotNil(snackbar.current)
    }

    func testReentryGuardDropsSecondUploadWhileSubmitting() async {
        client.getPhotosResult = .success([])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.load()

        let first = Task { await vm.upload(type: ._1, image: image()) }
        while client.photoCommands.isEmpty {
            await Task.yield()
        }
        XCTAssertTrue(vm.mutation.isUploading)

        await vm.upload(type: ._1, image: image()) // dropped by the guard

        client.resumeCommand()
        await first.value
        XCTAssertEqual(client.photoCommands.filter { $0.name == "savePhoto" }.count, 1)
    }

    func testReentryGuardDropsSecondDeleteWhileSubmitting() async {
        client.getPhotosResult = .success([OrderPhoto(id: "p1", photoType: ._1, blobUrl: nil)])
        client.suspendCommands = true
        let vm = makeVM()
        await vm.load()

        let first = Task { await vm.delete(photoId: "p1") }
        while client.photoCommands.isEmpty {
            await Task.yield()
        }
        await vm.delete(photoId: "p1") // dropped

        client.resumeCommand()
        await first.value
        XCTAssertEqual(client.photoCommands.filter { $0.name == "deletePhoto" }.count, 1)
    }

    func testPhotosAreCategorizedByBeforeAfterType() async {
        client.getPhotosResult = .success([
            OrderPhoto(id: "b1", photoType: ._1, blobUrl: nil),
            OrderPhoto(id: "a1", photoType: ._2, blobUrl: nil),
            OrderPhoto(id: "b2", photoType: ._1, blobUrl: nil)
        ])
        let vm = makeVM()
        await vm.load()

        let photos = try? XCTUnwrap(vm.state.loadedValue)
        XCTAssertEqual(photos?.filter { $0.photoType == ._1 }.map(\.id), ["b1", "b2"])
        XCTAssertEqual(photos?.filter { $0.photoType == ._2 }.map(\.id), ["a1"])
    }

    // MARK: TC-IOS-PHOTOS-OWNERSHIP (P1 / P2)

    func testSavePhotoCarriesOnlyConstructedOrderIdNoEmployeeId() async {
        // P1: the savePhoto surface carries orderId + the photo only — no
        // employeeId parameter exists. P2: the carried orderId is the VM's own
        // constructed id, never synthesized.
        client.getPhotosResult = .success([])
        let vm = makeVM(orderId: "order-xyz")
        await vm.load()

        await vm.upload(type: ._2, image: image())

        XCTAssertEqual(client.photoCommands.count, 1)
        XCTAssertEqual(client.photoCommands.first?.orderId, "order-xyz")
    }

    func testDeleteActsOnlyOnAPhotoIdFromItsOwnGetPhotos() async {
        // P2: the deletable id comes from this VM's own getPhotos response.
        client.getPhotosResult = .success([OrderPhoto(id: "owned-photo", photoType: ._1, blobUrl: nil)])
        let vm = makeVM(orderId: "order-1")
        await vm.load()

        let owned = try? XCTUnwrap(vm.state.loadedValue?.first?.id)
        await vm.delete(photoId: owned ?? "")

        XCTAssertEqual(client.photoCommands.first?.name, "deletePhoto")
        XCTAssertEqual(client.photoCommands.first?.photoId, "owned-photo")
    }
}
