import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct FullscreenPager: View {
    let photos: [GetOrderPhotosOrderPhotoDto]
    let startIndex: Int
    let zoom: Namespace.ID
    let onClose: () -> Void

    @State private var selection: Int

    init(
        photos: [GetOrderPhotosOrderPhotoDto],
        startIndex: Int,
        zoom: Namespace.ID,
        onClose: @escaping () -> Void
    ) {
        self.photos = photos
        self.startIndex = startIndex
        self.zoom = zoom
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

            closeButton
                .padding(Spacing.m)
        }
        // Keyed on the page on screen, not the one opened: a dismiss after paging shrinks into the
        // thumbnail of the photo being looked at.
        .zoomDestination(id: selection, in: zoom)
    }

    /// iOS 26: clear Liquid Glass, the variant for controls floating over media.
    @ViewBuilder
    private var closeButton: some View {
        if #available(iOS 26, *) {
            Button(action: onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.white)
                    .frame(width: 30, height: 30)
            }
            .buttonStyle(.glass(.clear))
            .buttonBorderShape(.circle)
        } else {
            Button(action: onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.white)
                    .frame(width: 44, height: 44)
                    .background(Color.black.opacity(0.4), in: Circle())
            }
        }
    }
}
