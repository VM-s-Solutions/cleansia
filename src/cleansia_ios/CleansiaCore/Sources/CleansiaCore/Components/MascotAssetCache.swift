import ImageIO
import SwiftUI
import UIKit

/// A decoded prefix of an animation — its frames and their WebP delays. `isComplete` marks the whole
/// loop, which is what lets a looping playhead wrap and a one-shot freeze.
struct MascotAnimation {
    let frames: [UIImage]
    let delays: [TimeInterval]
    let isComplete: Bool
}

/// A mascot's bundled bytes: one entry per segment file, in playback order, plus the size its frames
/// are decoded at. One logical animation, several files — see `AnimatedMascot.segmentNames`.
struct MascotAsset {
    let name: String
    let segments: [Data]
    let maxPixel: CGFloat
}

/// Process-wide caches for the raw asset `Data` and the decoded frames, so the heavy work happens at
/// most once per asset. Both are `NSCache`, so the system evicts them under memory pressure; the frame
/// cache is bounded by decoded bytes on top of that, because a whole mascot loop is tens of MiB.
final class MascotAssetCache {
    static let shared = MascotAssetCache()

    private let dataCache = NSCache<NSString, NSData>()
    private let frameCache = NSCache<NSString, FrameBox>()
    /// CONCURRENT, with the QoS chosen per chunk: on a serial queue a background prewarm of one mascot
    /// blocks the visible decode of another for its whole (quadratic) run.
    private let decodeQueue = DispatchQueue(label: "cleansia.mascot.decode", attributes: .concurrent)

    /// AT MOST ONE strong reference that survives `frameCache` eviction, for the mascot we deliberately
    /// keep hot (the order-in-progress hero). The `frameCache` is purgeable under memory pressure; when it
    /// drops the 125-frame `cleaningInProgress` loop the next paint pays the full re-decode again, so the
    /// pin holds it. One slot, and dropped on a memory warning: an unbounded pin map would be a leak by
    /// another name, since nothing else ever removes from it. Main-thread only (set from `prewarm`'s
    /// main-thread completion, read from `cachedAnimation` on the main/UI thread).
    private var pinned: (key: NSString, animation: MascotAnimation)?

    /// One decode per asset, shared by every view and by `prewarm`, so a visible mascot never repeats
    /// work a prewarm is already doing. Main-thread only, like `pinned`.
    private var jobs: [NSString: DecodeJob] = [:]

    /// 125 frames of the cleaning loop at its native 360 px would be 61.8 MiB of decoded bitmaps, held
    /// for the life of the process. The decoded cache is bounded instead — the pin plus the two mascots
    /// at their current sizes come to ~52 MiB.
    private let decodedByteLimit = 64 * 1024 * 1024

    final class FrameBox {
        let animation: MascotAnimation
        init(_ animation: MascotAnimation) {
            self.animation = animation
        }
    }

    private init() {
        frameCache.totalCostLimit = decodedByteLimit
        NotificationCenter.default.addObserver(
            forName: UIApplication.didReceiveMemoryWarningNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.pinned = nil
            self?.frameCache.removeAllObjects()
        }
    }

    /// The mascot's segment files in playback order. Stops at the first missing segment rather than
    /// skipping it, so a mis-exported asset plays a shorter loop instead of a jumping one.
    func asset(for mascot: AnimatedMascot) -> MascotAsset? {
        var segments: [Data] = []
        for name in mascot.segmentNames {
            guard let data = segmentData(name: name) else { break }
            segments.append(data)
        }
        guard !segments.isEmpty else { return nil }
        return MascotAsset(name: mascot.rawValue, segments: segments, maxPixel: mascot.maxPixel)
    }

    func cachedAnimation(_ asset: MascotAsset) -> MascotAnimation? {
        let key = frameKey(asset)
        // A pinned animation outlives frameCache eviction — check it first so a purged hero replays
        // instantly instead of re-decoding.
        if let pinned, pinned.key == key { return pinned.animation }
        return frameCache.object(forKey: key)?.animation
    }

    /// Decode a mascot's frames off the main thread AHEAD of the view that needs them and PIN the result,
    /// so the visible hero gets a whole loop on its first paint instead of one that is still growing, and a
    /// later NSCache purge can't force the decode again. Idempotent; call from the main thread (e.g. an
    /// order's ViewModel `load()` before the in-progress hero appears, or at app launch).
    func prewarm(_ mascot: AnimatedMascot) {
        // Check the pin BEFORE touching the catalog: prewarm runs on every order-detail load and at shell
        // entry, and `asset(for:)` materialises every segment's Data (~1.4 MB across 7 reads) synchronously.
        // The key is derivable from the mascot alone, so an already-pinned loop costs no I/O at all.
        let key = frameKey(name: mascot.rawValue, maxPixel: mascot.maxPixel)
        if pinned?.key == key { return }
        guard let asset = asset(for: mascot) else { return }
        if let hit = cachedAnimation(asset) {
            pinned = (key, hit)
            return
        }
        if let job = jobs[key] {
            job.isPinned = true
            return
        }
        startJob(key: key, asset: asset, isVisible: false, isPinned: true, onUpdate: nil)
    }

    /// Decode off the main thread and publish PROGRESSIVELY: `onUpdate` runs on the main thread with a
    /// growing prefix of the animation — the first frames within milliseconds, then a steady chunk at a
    /// time, then the complete loop. A cache hit calls back once, synchronously. Call from the main
    /// thread. The frame cache is keyed by asset name + size, which is unique across the one bundle
    /// mascot art ships in.
    func loadAnimation(_ asset: MascotAsset, onUpdate: @escaping (MascotAnimation) -> Void) {
        if let hit = cachedAnimation(asset) {
            onUpdate(hit)
            return
        }
        let key = frameKey(asset)
        if let job = jobs[key] {
            // A prewarm (or another view) is already decoding this asset: attach instead of duplicating
            // the work, and promote it — this caller is on screen.
            job.isVisible = true
            job.subscribers.append(onUpdate)
            if !job.frames.isEmpty { onUpdate(job.snapshot) }
            return
        }
        startJob(key: key, asset: asset, isVisible: true, isPinned: false, onUpdate: onUpdate)
    }

    /// A single downsampled poster frame (frame 0 of the first segment), for an instant image while the
    /// full animation decodes. Cheap — one thumbnail, not the whole loop.
    func posterFrame(_ asset: MascotAsset) -> UIImage? {
        guard let data = asset.segments.first,
              let source = CGImageSourceCreateWithData(data as CFData, nil),
              CGImageSourceGetCount(source) > 0,
              let frame = CGImageSourceCreateThumbnailAtIndex(
                  source, 0, MascotAssetCache.thumbnailOptions(asset.maxPixel)
              )
        else { return nil }
        return UIImage(cgImage: frame)
    }

    private func segmentData(name: String) -> Data? {
        let key = name as NSString
        if let cached = dataCache.object(forKey: key) { return cached as Data }
        guard let data = NSDataAsset(name: name, bundle: MascotAssets.bundle)?.data else { return nil }
        dataCache.setObject(data as NSData, forKey: key)
        return data
    }

    private func startJob(
        key: NSString,
        asset: MascotAsset,
        isVisible: Bool,
        isPinned: Bool,
        onUpdate: ((MascotAnimation) -> Void)?
    ) {
        var sources: [CGImageSource] = []
        var counts: [Int] = []
        for data in asset.segments {
            guard let source = CGImageSourceCreateWithData(data as CFData, nil) else { continue }
            let count = CGImageSourceGetCount(source)
            guard count > 0 else { continue }
            sources.append(source)
            counts.append(count)
        }
        guard !sources.isEmpty else { return }
        let job = DecodeJob(
            key: key,
            sources: sources,
            counts: counts,
            maxPixel: asset.maxPixel,
            isVisible: isVisible,
            isPinned: isPinned
        )
        if let onUpdate { job.subscribers.append(onUpdate) }
        jobs[key] = job
        pump(job)
    }

    /// Dispatch the next chunk. Everything on the job is mutated on the main thread only; the background
    /// block just turns a frame range into images, so no lock is needed. Re-reading `isVisible` here is
    /// what lets a prewarm promoted mid-flight finish at a visible QoS.
    private func pump(_ job: DecodeJob) {
        guard let slice = AnimatedMascotPlayback.nextChunkSlice(
            published: job.published,
            cursor: job.cursor,
            counts: job.counts,
            firstChunk: job.firstChunk
        ) else {
            finish(job)
            return
        }
        job.cursor += slice.range.count
        let source = job.sources[slice.segment]
        let maxPixel = job.maxPixel
        decodeQueue.async(qos: job.isVisible ? .userInitiated : .utility) { [weak self] in
            let chunk = MascotAssetCache.decodeChunk(source: source, range: slice.range, maxPixel: maxPixel)
            DispatchQueue.main.async { self?.append(chunk, to: job) }
        }
    }

    private func append(_ chunk: (frames: [UIImage], delays: [TimeInterval]), to job: DecodeJob) {
        job.frames += chunk.frames
        job.delays += chunk.delays
        let isComplete = job.cursor >= job.count
        if AnimatedMascotPlayback.shouldPublish(
            decoded: job.frames.count,
            published: job.published,
            isComplete: isComplete,
            firstChunk: job.firstChunk
        ) {
            job.published = job.frames.count
            let snapshot = job.snapshot
            for subscriber in job.subscribers {
                subscriber(snapshot)
            }
        }
        if isComplete {
            finish(job)
        } else {
            pump(job)
        }
    }

    private func finish(_ job: DecodeJob) {
        jobs.removeValue(forKey: job.key)
        job.subscribers.removeAll()
        guard !job.frames.isEmpty else { return }
        let animation = job.snapshot
        frameCache.setObject(FrameBox(animation), forKey: job.key, cost: MascotAssetCache.byteCount(animation))
        if job.isPinned { pinned = (job.key, animation) }
    }

    private func frameKey(_ asset: MascotAsset) -> NSString {
        frameKey(name: asset.name, maxPixel: asset.maxPixel)
    }

    /// The same key, derivable without loading any asset data — so callers can test the cache cheaply.
    private func frameKey(name: String, maxPixel: CGFloat) -> NSString {
        "\(name)#\(Int(maxPixel))" as NSString
    }

    private static func thumbnailOptions(_ maxPixel: CGFloat) -> CFDictionary {
        [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceThumbnailMaxPixelSize: maxPixel,
            kCGImageSourceShouldCacheImmediately: true
        ] as CFDictionary
    }

    private static func byteCount(_ animation: MascotAnimation) -> Int {
        animation.frames.reduce(0) { total, frame in
            guard let image = frame.cgImage else { return total }
            return total + image.bytesPerRow * image.height
        }
    }

    private static func decodeChunk(
        source: CGImageSource,
        range: Range<Int>,
        maxPixel: CGFloat
    ) -> (frames: [UIImage], delays: [TimeInterval]) {
        let options = thumbnailOptions(maxPixel)
        var frames: [UIImage] = []
        var delays: [TimeInterval] = []
        frames.reserveCapacity(range.count)
        delays.reserveCapacity(range.count)
        for index in range {
            guard let frame = CGImageSourceCreateThumbnailAtIndex(source, index, options)
            else { continue }
            frames.append(UIImage(cgImage: frame))
            delays.append(frameDelay(source, index))
        }
        return (frames, delays)
    }

    /// Where each animated format keeps its per-frame delay. The unclamped value first: the clamped one
    /// silently rounds a short frame up.
    private struct DelayKeys {
        let format: CFString
        let unclamped: CFString
        let clamped: CFString
    }

    private static let delayKeys = [
        DelayKeys(
            format: kCGImagePropertyWebPDictionary,
            unclamped: kCGImagePropertyWebPUnclampedDelayTime,
            clamped: kCGImagePropertyWebPDelayTime
        ),
        DelayKeys(
            format: kCGImagePropertyGIFDictionary,
            unclamped: kCGImagePropertyGIFUnclampedDelayTime,
            clamped: kCGImagePropertyGIFDelayTime
        )
    ]

    private static func frameDelay(_ source: CGImageSource, _ index: Int) -> TimeInterval {
        guard let props = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any] else {
            return AnimatedMascotPlayback.fallbackFrameDelay
        }
        for keys in delayKeys {
            guard let container = props[keys.format] as? [CFString: Any],
                  let delay = (container[keys.unclamped] as? Double) ?? (container[keys.clamped] as? Double)
            else { continue }
            return AnimatedMascotPlayback.normalizedDelay(delay)
        }
        return AnimatedMascotPlayback.fallbackFrameDelay
    }
}

/// One in-flight progressive decode over a mascot's ordered segment files. Mutated on the main thread
/// only — the background chunks receive an immutable frame range and hand their images back through the
/// main queue. `cursor`, `frames` and `delays` run across the concatenation of the segments, so playback
/// never sees the seams.
private final class DecodeJob {
    let key: NSString
    let sources: [CGImageSource]
    let counts: [Int]
    let count: Int
    let maxPixel: CGFloat
    let firstChunk: Int
    /// The next frame index to decode, which runs ahead of `frames.count` when ImageIO refuses a frame.
    var cursor = 0
    var frames: [UIImage] = []
    var delays: [TimeInterval] = []
    var published = 0
    var isVisible: Bool
    var isPinned: Bool
    var subscribers: [(MascotAnimation) -> Void] = []

    init(
        key: NSString,
        sources: [CGImageSource],
        counts: [Int],
        maxPixel: CGFloat,
        isVisible: Bool,
        isPinned: Bool
    ) {
        self.key = key
        self.sources = sources
        self.counts = counts
        count = counts.reduce(0, +)
        self.maxPixel = maxPixel
        firstChunk = AnimatedMascotPlayback.firstChunkSize(frameCount: count)
        self.isVisible = isVisible
        self.isPinned = isPinned
    }

    var snapshot: MascotAnimation {
        MascotAnimation(frames: frames, delays: delays, isComplete: cursor >= count)
    }
}
