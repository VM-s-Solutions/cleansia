#if canImport(UIKit)
    import Combine
    import UIKit

    public enum RemoteImageError: Error, Equatable {
        case unreachable
        case undecodable
    }

    /// Decoded remote images held in memory under a caller-chosen key.
    ///
    /// The key identifies the CONTENT; the URL is only where the bytes come from. Blob URLs on this
    /// backend are per-request SAS links that carry a fresh signature and expiry every time the owning
    /// DTO is fetched, so keying on the URL would miss on every read and re-download the same image.
    ///
    /// Nothing is written to disk: the default loader runs on an `.ephemeral` session so neither the
    /// bytes nor the credential-bearing URL that keyed them reach a `URLCache` on disk, and the images
    /// are per-user state, so a holder registers with the `SessionScopedCacheRegistry`.
    @MainActor
    public final class RemoteImageCache: ObservableObject, SessionScopedCache {
        public typealias Loader = @Sendable (URL) async throws -> Data

        private let cache = NSCache<NSString, UIImage>()
        private let load: Loader

        public init(countLimit: Int = 16, loader: Loader? = nil) {
            cache.countLimit = countLimit
            load = loader ?? RemoteImageCache.ephemeralLoader()
        }

        public func image(forKey key: String) -> UIImage? {
            cache.object(forKey: key as NSString)
        }

        @discardableResult
        public func image(forKey key: String, url: URL) async throws -> UIImage {
            if let hit = image(forKey: key) { return hit }
            let data = try await load(url)
            guard let image = UIImage(data: data) else { throw RemoteImageError.undecodable }
            cache.setObject(image, forKey: key as NSString)
            return image
        }

        public func clear() async {
            cache.removeAllObjects()
        }

        private static func ephemeralLoader() -> Loader {
            let session = URLSession(configuration: .ephemeral)
            return { url in
                let (data, response) = try await session.data(from: url)
                guard let http = response as? HTTPURLResponse, (200 ..< 300).contains(http.statusCode) else {
                    throw RemoteImageError.unreachable
                }
                return data
            }
        }
    }
#endif
