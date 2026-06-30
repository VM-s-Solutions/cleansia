import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

private struct PhotoIndex: Identifiable {
    let id: Int
}

struct OrderPhotosScreen: View {
    @StateObject private var vm: OrderPhotosViewModel
    @State private var fullscreen: PhotoIndex?

    init(orderId: String, client: OrderClient, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: OrderPhotosViewModel(orderId: orderId, client: client, snackbar: snackbar))
    }

    var body: some View {
        content
            .navigationTitle(L10n.OrderPhotos.sectionTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            .fullScreenCover(item: $fullscreen) { item in
                FullscreenPager(photos: vm.state.loadedValue?.photos ?? [], startIndex: item.id) {
                    fullscreen = nil
                }
            }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            VStack(spacing: Spacing.s) {
                Image(systemName: "wifi.slash")
                    .font(.system(size: 40))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                CleansiaOutlinedButton(L10n.Orders.errorRetry, size: .medium) {
                    Task { await vm.load() }
                }
                .fixedSize()
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .loaded(response):
            grid(response.photos ?? [])
        }
    }

    private func grid(_ photos: [GetOrderPhotosOrderPhotoDto]) -> some View {
        ScrollView {
            LazyVGrid(
                columns: [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())],
                spacing: Spacing.xs
            ) {
                ForEach(Array(photos.enumerated()), id: \.offset) { index, photo in
                    Button {
                        fullscreen = PhotoIndex(id: index)
                    } label: {
                        PhotoThumb(urlString: photo.blobUrl, size: 112)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(Spacing.m)
        }
    }
}
