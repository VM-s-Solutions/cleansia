import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

private struct PhotoIndex: Identifiable {
    let id: Int
    let photos: [GetOrderPhotosOrderPhotoDto]
    let phase: OrderPhotoPhase
}

struct OrderPhotosScreen: View {
    @StateObject private var vm: OrderPhotosViewModel
    @State private var fullscreen: PhotoIndex?
    @State private var consumedInitialSelection = false
    private let initialSelection: OrderPhotoSelection?
    @Namespace private var zoom

    init(
        orderId: String,
        client: OrderClient,
        snackbar: SnackbarController,
        initialSelection: OrderPhotoSelection? = nil
    ) {
        _vm = StateObject(wrappedValue: OrderPhotosViewModel(orderId: orderId, client: client, snackbar: snackbar))
        self.initialSelection = initialSelection
    }

    var body: some View {
        content
            .navigationTitle(L10n.OrderPhotos.sectionTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            .onReceive(vm.$state) { state in
                guard !consumedInitialSelection, let initialSelection, case let .loaded(gallery) = state else { return }
                consumedInitialSelection = true
                if let index = gallery.previewIndex(for: initialSelection) {
                    fullscreen = PhotoIndex(
                        id: index,
                        photos: gallery.photos(for: initialSelection.phase),
                        phase: initialSelection.phase
                    )
                }
            }
            .fullScreenCover(item: $fullscreen) { item in
                FullscreenPager(
                    photos: item.photos,
                    startIndex: item.id,
                    zoom: zoom,
                    zoomPrefix: "\(item.phase.rawValue)-",
                    collectionTitle: item.phase.label
                ) {
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
        case let .loaded(gallery):
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: Spacing.m) {
                        ForEach(gallery.phases) { phase in
                            let photos = gallery.photos(for: phase)
                            VStack(alignment: .leading, spacing: Spacing.xs) {
                                Text("\(phase.label) · \(photos.count)")
                                    .font(CleansiaTypography.titleMedium)
                                    .foregroundColor(CleansiaColors.onSurface)
                                if photos.isEmpty {
                                    Text(L10n.OrderPhotos.empty)
                                        .font(CleansiaTypography.bodyMedium)
                                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                                } else {
                                    grid(photos, phase: phase)
                                }
                            }
                            .id(phase)
                        }
                    }
                    .padding(Spacing.m)
                }
                .task {
                    if let phase = initialSelection?.phase {
                        await Task.yield()
                        proxy.scrollTo(phase, anchor: .top)
                    }
                }
            }
        }
    }

    /// As many columns as the width holds: three on every iPhone, the 375pt ones included, as before, and more on a
    /// wider screen. The minimum sits under the 112pt thumbnail because a 375pt phone's three cells are 109pt.
    static let gridColumns = [GridItem(.adaptive(minimum: 108))]

    private func grid(_ photos: [GetOrderPhotosOrderPhotoDto], phase: OrderPhotoPhase) -> some View {
        LazyVGrid(columns: Self.gridColumns, spacing: Spacing.xs) {
            ForEach(Array(photos.enumerated()), id: \.offset) { index, photo in
                Button {
                    fullscreen = PhotoIndex(id: index, photos: photos, phase: phase)
                } label: {
                    PhotoThumb(urlString: photo.blobUrl, size: 112)
                }
                .buttonStyle(.plain)
                .zoomSource(id: "\(phase.rawValue)-\(index)", in: zoom)
            }
        }
    }
}
