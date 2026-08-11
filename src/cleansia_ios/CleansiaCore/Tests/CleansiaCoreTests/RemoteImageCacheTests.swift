#if canImport(UIKit)
    import UIKit
    import XCTest
    @testable import CleansiaCore

    @MainActor
    final class RemoteImageCacheTests: XCTestCase {
        func testFetchesOnceAndServesTheSecondReadFromMemory() async throws {
            let loader = RecordingLoader()
            let cache = RemoteImageCache(loader: loader.load)

            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one?sig=first"))
            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one?sig=first"))

            XCTAssertEqual(loader.requested.count, 1)
        }

        /// The profile photo's URL is a per-request SAS that changes on every fetch, so a URL-keyed
        /// cache would miss every time. The key is the content-addressed blob name.
        func testASecondUrlForTheSameKeyIsServedFromMemory() async throws {
            let loader = RecordingLoader()
            let cache = RemoteImageCache(loader: loader.load)

            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one?sig=first"))
            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one?sig=second"))

            XCTAssertEqual(loader.requested.map(\.absoluteString), ["https://blob/one?sig=first"])
        }

        func testADifferentKeyIsFetchedEvenBehindTheSameUrl() async throws {
            let loader = RecordingLoader()
            let cache = RemoteImageCache(loader: loader.load)

            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one"))
            _ = try await cache.image(forKey: "blob-2", url: url("https://blob/one"))

            XCTAssertEqual(loader.requested.count, 2)
        }

        func testClearDropsEveryDecodedImage() async throws {
            let loader = RecordingLoader()
            let cache = RemoteImageCache(loader: loader.load)
            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one"))

            await cache.clear()

            XCTAssertNil(cache.image(forKey: "blob-1"))
            _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one"))
            XCTAssertEqual(loader.requested.count, 2)
        }

        func testTransportFailureThrowsAndCachesNothing() async {
            let loader = RecordingLoader()
            loader.result = .failure(RemoteImageError.unreachable)
            let cache = RemoteImageCache(loader: loader.load)

            do {
                _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one"))
                XCTFail("expected the load to throw")
            } catch {
                XCTAssertNil(cache.image(forKey: "blob-1"))
            }
        }

        func testUndecodableBytesThrowRatherThanCachingAnEmptyImage() async {
            let loader = RecordingLoader()
            loader.result = .success(Data("not an image".utf8))
            let cache = RemoteImageCache(loader: loader.load)

            do {
                _ = try await cache.image(forKey: "blob-1", url: url("https://blob/one"))
                XCTFail("expected the decode to throw")
            } catch {
                XCTAssertEqual(error as? RemoteImageError, .undecodable)
                XCTAssertNil(cache.image(forKey: "blob-1"))
            }
        }

        private func url(_ value: String) -> URL {
            guard let url = URL(string: value) else {
                preconditionFailure("test fixture is not a URL: \(value)")
            }
            return url
        }
    }

    private final class RecordingLoader: @unchecked Sendable {
        private(set) var requested: [URL] = []
        var result: Result<Data, Error> = .success(RecordingLoader.pixelJpeg())

        func load(_ url: URL) async throws -> Data {
            requested.append(url)
            return try result.get()
        }

        static func pixelJpeg() -> Data {
            let renderer = UIGraphicsImageRenderer(size: CGSize(width: 4, height: 4))
            return renderer.image { context in
                UIColor.systemTeal.setFill()
                context.fill(CGRect(x: 0, y: 0, width: 4, height: 4))
            }.jpegData(compressionQuality: 0.8) ?? Data()
        }
    }
#endif
