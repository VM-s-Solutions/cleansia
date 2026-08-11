#if canImport(UIKit)
    import SwiftUI
    import UIKit

    /// Renders a remote image that is cached under `cacheKey` rather than under its URL, for the
    /// SAS-signed blob URLs this backend mints per request (see `RemoteImageCache`).
    ///
    /// `onLoadFailure` fires once per failed attempt so the owner can re-fetch the DTO that carries the
    /// URL. The loader cannot tell an expired signature from a deleted blob, so the caller retries
    /// blind and guards the retry itself; a cancelled load (the view left the screen) is not a failure.
    public struct CachedRemoteImage<Placeholder: View>: View {
        private let cacheKey: String
        private let url: URL
        private let onLoadFailure: () -> Void
        private let placeholder: () -> Placeholder

        @ObservedObject private var cache: RemoteImageCache
        @State private var image: UIImage?

        public init(
            cacheKey: String,
            url: URL,
            cache: RemoteImageCache,
            onLoadFailure: @escaping () -> Void = {},
            @ViewBuilder placeholder: @escaping () -> Placeholder
        ) {
            self.cacheKey = cacheKey
            self.url = url
            self.cache = cache
            self.onLoadFailure = onLoadFailure
            self.placeholder = placeholder
        }

        public var body: some View {
            ZStack {
                if let image {
                    Image(uiImage: image)
                        .resizable()
                        .scaledToFill()
                } else {
                    placeholder()
                }
            }
            .task(id: "\(cacheKey)|\(url.absoluteString)") { await load() }
        }

        private func load() async {
            if let hit = cache.image(forKey: cacheKey) {
                image = hit
                return
            }
            image = nil
            do {
                image = try await cache.image(forKey: cacheKey, url: url)
            } catch {
                guard !Task.isCancelled else { return }
                onLoadFailure()
            }
        }
    }
#endif
