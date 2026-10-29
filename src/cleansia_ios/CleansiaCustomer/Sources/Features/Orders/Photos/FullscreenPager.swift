import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct FullscreenPager: View {
    let photos: [GetOrderPhotosOrderPhotoDto]
    let startIndex: Int
    let zoom: Namespace.ID
    let zoomPrefix: String
    let collectionTitle: String?
    let onClose: () -> Void

    @State private var selection: Int

    init(
        photos: [GetOrderPhotosOrderPhotoDto],
        startIndex: Int,
        zoom: Namespace.ID,
        zoomPrefix: String = "",
        collectionTitle: String? = nil,
        onClose: @escaping () -> Void
    ) {
        self.photos = photos
        self.startIndex = startIndex
        self.zoom = zoom
        self.zoomPrefix = zoomPrefix
        self.collectionTitle = collectionTitle
        self.onClose = onClose
        _selection = State(initialValue: startIndex)
    }

    var body: some View {
        ZStack(alignment: .topTrailing) {
            Color.black.ignoresSafeArea()

            TabView(selection: $selection) {
                ForEach(Array(photos.enumerated()), id: \.offset) { index, photo in
                    AsyncImage(url: photo.blobUrl.flatMap(URL.init(string:))) { image in
                        image.resizable().scaledToFit()
                    } placeholder: {
                        ProgressView().tint(.white)
                    }
                    .tag(index)
                }
            }
            .tabViewStyle(.page(indexDisplayMode: .always))
            .ignoresSafeArea()

            HStack {
                if let collectionTitle {
                    Text("\(collectionTitle) · \(selection + 1)/\(photos.count)")
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(.white)
                }
                Spacer()
                MediaCloseButton(action: onClose)
            }
            .padding(Spacing.m)
        }
        // Keyed on the page on screen, not the one opened: a dismiss after paging shrinks into the
        // thumbnail of the photo being looked at.
        .zoomDestination(id: "\(zoomPrefix)\(selection)", in: zoom)
    }
}
