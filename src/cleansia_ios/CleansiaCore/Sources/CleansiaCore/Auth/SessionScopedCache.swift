import Foundation

public protocol SessionScopedCache: AnyObject {
    func clear() async
}

public final class SessionScopedCacheRegistry: @unchecked Sendable {
    private let lock = NSLock()
    private var caches: [WeakBox] = []

    public init() {}

    public func register(_ cache: SessionScopedCache) {
        lock.lock()
        defer { lock.unlock() }
        caches.append(WeakBox(cache))
    }

    public func clearAll() async {
        lock.lock()
        let live = caches.compactMap(\.value)
        caches.removeAll { $0.value == nil }
        lock.unlock()

        for cache in live {
            await cache.clear()
        }
    }

    private final class WeakBox {
        weak var value: SessionScopedCache?
        init(_ value: SessionScopedCache) { self.value = value }
    }
}
