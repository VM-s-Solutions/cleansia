import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct FullscreenPager: View {
    let photos: [GetOrderPhotosOrderPhotoDto]
    let startIndex: Int
    let onClose: () -> Void

    @State private var selection: Int

    init(photos: [GetOrderPhotosOrderPhotoDto], startIndex: Int, onClose: @escaping () -> Void) {
        self.photos = photos
        self.startIndex = startIndex
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

            Button(action: onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundColor(.white)
                    .frame(width: 44, height: 44)
                    .background(Color.black.opacity(0.4), in: Circle())
            }
            .padding(Spacing.m)
        }
    }
}
