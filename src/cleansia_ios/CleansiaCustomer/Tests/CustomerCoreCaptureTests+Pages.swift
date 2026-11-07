import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The pushed customer pages that put a figure or a separator inside a line of their own: the photo gallery's phase
/// headings, the full-screen pager's title and the delete-account lists.
extension CustomerCoreCaptureTests {
    func testOrderPhotos() async throws {
        let gallery = OrderPhotos(
            photos: [
                GetOrderPhotosOrderPhotoDto(id: "before-1", photoType: ._1),
                GetOrderPhotosOrderPhotoDto(id: "before-2", photoType: ._1)
            ],
            beforeCount: 2,
            afterCount: 0
        )
        for theme in ["light", "dark"] {
            try use("en")
            let client = FakeOrderClient()
            client.photosResults = [.success(gallery)]
            await CustomerCapture.shoot(
                name("photos", "gallery", Self.phone, "en", theme),
                size: Self.phone,
                to: self,
                hold: 1
            ) {
                page(theme: theme) {
                    NavigationStack(path: .constant(["order-1"])) {
                        Color.clear
                            .toolbar(.hidden, for: .navigationBar)
                            .navigationDestination(for: String.self) { id in
                                OrderPhotosScreen(orderId: id, client: client, snackbar: SnackbarController())
                            }
                    }
                }
            }
        }
    }

    /// The photos load from a file, so the frame holds the picture rather than a spinner caught mid-turn.
    func testPhotoPager() async throws {
        let file = FileManager.default.temporaryDirectory.appendingPathComponent("customer-capture-photo.png")
        let picture = UIGraphicsImageRenderer(size: CGSize(width: 120, height: 90)).image { context in
            UIColor(red: 0.55, green: 0.7, blue: 0.85, alpha: 1).setFill()
            context.fill(CGRect(x: 0, y: 0, width: 120, height: 90))
        }
        try XCTUnwrap(picture.pngData()).write(to: file)
        let photos = (1 ... 3).map {
            GetOrderPhotosOrderPhotoDto(id: "before-\($0)", photoType: ._1, blobUrl: file.absoluteString)
        }
        for theme in ["light", "dark"] {
            try use("en")
            await CustomerCapture.shoot(
                name("photos", "pager", Self.phone, "en", theme),
                size: Self.phone,
                to: self,
                hold: 1
            ) {
                PagerHost(photos: photos)
                    .frame(width: Self.phone.width, height: Self.phone.height)
                    .preferredColorScheme(theme == "dark" ? .dark : .light)
                    .environment(\.locale, Locale(identifier: "en"))
            }
        }
    }

    func testDeleteAccount() async throws {
        for theme in ["light", "dark"] {
            for end in [false, true] {
                try use("en")
                await CustomerCapture.shoot(
                    name("delete-account", "form", Self.phone, "en", theme) + (end ? "-end" : ""),
                    size: Self.phone,
                    to: self,
                    hold: 0.3,
                    end: end
                ) {
                    page(theme: theme) {
                        NavigationStack {
                            DeleteAccountView(
                                userEmail: "jana@example.test",
                                client: FakeGdprDeleteClient(),
                                authClient: FakeAuthClient(),
                                snackbar: SnackbarController(),
                                onDeleted: {}
                            )
                        }
                    }
                }
            }
        }
    }

    /// A pushed page under the status bar's stand-in.
    private func page(theme: String, @ViewBuilder content: () -> some View) -> some View {
        VStack(spacing: 0) {
            CleansiaColors.background.frame(height: 54)
            content()
        }
        .frame(width: Self.phone.width, height: Self.phone.height)
        .background(CleansiaColors.background)
        .tint(CleansiaColors.primaryText)
        .environment(\.locale, Locale(identifier: "en"))
        .preferredColorScheme(theme == "dark" ? .dark : .light)
        .dynamicTypeSize(.large)
    }
}

/// The pager as the gallery presents it, on its second photo.
private struct PagerHost: View {
    let photos: [GetOrderPhotosOrderPhotoDto]
    @Namespace private var zoom

    var body: some View {
        FullscreenPager(
            photos: photos,
            startIndex: 1,
            zoom: zoom,
            zoomPrefix: "1-",
            collectionTitle: OrderPhotoPhase.before.label,
            onClose: {}
        )
    }
}
