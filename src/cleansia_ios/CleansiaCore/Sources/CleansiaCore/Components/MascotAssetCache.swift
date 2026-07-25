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

/// Process-wide caches for the raw asset `Data` and the decoded frames, so the
/// heavy work happens at most once per asset. Both are `NSCache`, so the system
/// evicts them under memory pressure.
final class MascotAssetCache {
    static let shared = MascotAssetCache()

    private let dataCache = NSCache<NSString, NSData>()
    private let frameCache = NSCache<NSString, FrameBox>()
    /// CONCURRENT, with the QoS chosen per chunk: on a serial queue a background prewarm of one mascot
    /// blocks the visible decode of another for its whole (quadratic) run.
    private let decodeQueue = DispatchQueue(label: "cleansia.mascot.decode", attributes: .concurrent)

    /// Strong references that survive `frameCache` eviction, for mascots we deliberately keep hot (the
    /// order-in-progress hero). The `frameCache` is purgeable under memory pressure; when it drops the
    /// heavy 63-frame `cleaningInProgress` loop the next paint pays the full re-decode again. Pinning holds
    /// the decoded frames so that re-decode never happens twice. Main-thread only (set from `prewarm`'s
    /// main-thread completion, read from `cachedAnimation` on the main/UI thread).
    private var pinnedAnimations: [NSString: MascotAnimation] = [:]

    /// One decode per asset, shared by every view and by `prewarm`, so a visible mascot never repeats
    /// work a prewarm is already doing. Main-thread only, like `pinnedAnimations`.
    private var jobs: [NSString: DecodeJob] = [:]

    /// Frames are decoded at the asset's native size (360 px). The mascots render up to 220 pt
    /// (booking/membership success hero) and 140 pt (loader / order hero) — i.e. ~660 / ~420 px
    /// at @3x, both already above native — so there is no useful detail to downsample away.
    /// Trade-off: the full loop is held in memory (~33 MB for 63 frames) in an NSCache the system
    /// purges under pressure; a running animation keeps its own strong ref, so a purge only forces
    /// a re-decode next time.
    private let maxPixel: CGFloat = 360

    final class FrameBox {
        let animation: MascotAnimation
        init(_ animation: MascotAnimation) {
            self.animation = animation
        }
    }

    func data(for mascot: AnimatedMascot, bundle: Bundle) -> Data? {
        let key = "\(mascot.rawValue)#\(bundle.bundleIdentifier ?? "main")" as NSString
        if let cached = dataCache.object(forKey: key) { return cached as Data }
        guard let data = NSDataAsset(name: mascot.rawValue, bundle: bundle)?.data else { return nil }
        dataCache.setObject(data as NSData, forKey: key)
        return data
    }

    func cachedAnimation(name: String) -> MascotAnimation? {
        let key = frameKey(name)
        // A pinned animation outlives frameCache eviction — check it first so a purged hero replays
        // instantly instead of re-decoding.
        if let pinned = pinnedAnimations[key] { return pinned }
        return frameCache.object(forKey: key)?.animation
    }

    /// Decode a mascot's frames off the main thread AHEAD of the view that needs them and PIN the result,
    /// so the visible hero gets a whole loop on its first paint instead of one that is still growing, and a
    /// later NSCache purge can't force the decode again. Idempotent; call from the main thread (e.g. an
    /// order's ViewModel `load()` before the in-progress hero appears, or at app launch).
    func prewarm(_ mascot: AnimatedMascot, bundle: Bundle = .main) {
        let key = frameKey(mascot.rawValue)
        if pinnedAnimations[key] != nil { return }
        if let hit = cachedAnimation(name: mascot.rawValue) {
            pinnedAnimations[key] = hit
            return
        }
        if let job = jobs[key] {
            job.isPinned = true
            return
        }
        guard let data = data(for: mascot, bundle: bundle) else { return }
        startJob(key: key, data: data, isVisible: false, isPinned: true, onUpdate: nil)
    }

    /// Decode off the main thread and publish PROGRESSIVELY: `onUpdate` runs on the main thread with a
    /// growing prefix of the animation — the first frames within milliseconds, then each doubling, then
    /// the complete loop. A cache hit calls back once, synchronously. Call from the main thread. The
    /// frame cache is keyed by asset name + size; every call site uses the main bundle, where asset
    /// names are unique.
    func loadAnimation(name: String, data: Data, onUpdate: @escaping (MascotAnimation) -> Void) {
        if let hit = cachedAnimation(name: name) {
            onUpdate(hit)
            return
        }
        let key = frameKey(name)
        if let job = jobs[key] {
            // A prewarm (or another view) is already decoding this asset: attach instead of duplicating
            // the work, and promote it — this caller is on screen.
            job.isVisible = true
            job.subscribers.append(onUpdate)
            if !job.frames.isEmpty { onUpdate(job.snapshot) }
            return
        }
        startJob(key: key, data: data, isVisible: true, isPinned: false, onUpdate: onUpdate)
    }

    private func startJob(
        key: NSString,
        data: Data,
        isVisible: Bool,
        isPinned: Bool,
        onUpdate: ((MascotAnimation) -> Void)?
    ) {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil) else { return }
        let count = CGImageSourceGetCount(source)
        guard count > 0 else { return }
        let job = DecodeJob(key: key, source: source, count: count, isVisible: isVisible, isPinned: isPinned)
        if let onUpdate { job.subscribers.append(onUpdate) }
        jobs[key] = job
        pump(job)
    }

    /// Dispatch the next chunk. Everything on the job is mutated on the main thread only; the background
    /// block just turns a frame range into images, so no lock is needed. Re-reading `isVisible` here is
    /// what lets a prewarm promoted mid-flight finish at a visible QoS.
    private func pump(_ job: DecodeJob) {
        let length = AnimatedMascotPlayback.nextChunkLength(
            published: job.published,
            remaining: job.count - job.cursor,
            firstChunk: job.firstChunk
        )
        guard length > 0 else {
            finish(job)
            return
        }
        let range = job.cursor ..< (job.cursor + length)
        job.cursor = range.upperBound
        let source = job.source
        let maxPixel = maxPixel
        decodeQueue.async(qos: job.isVisible ? .userInitiated : .utility) { [weak self] in
            let chunk = MascotAssetCache.decodeChunk(source: source, range: range, maxPixel: maxPixel)
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
        frameCache.setObject(FrameBox(animation), forKey: job.key)
        if job.isPinned { pinnedAnimations[job.key] = animation }
    }

    private func frameKey(_ name: String) -> NSString {
        "\(name)#\(Int(maxPixel))" as NSString
    }

    /// A single downsampled poster frame (frame 0), for an instant image while
    /// the full animation decodes. Cheap — one thumbnail, not the whole loop.
    func posterFrame(data: Data) -> UIImage? {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil),
              CGImageSourceGetCount(source) > 0,
              let frame = CGImageSourceCreateThumbnailAtIndex(source, 0, [
                  kCGImageSourceCreateThumbnailFromImageAlways: true,
                  kCGImageSourceCreateThumbnailWithTransform: true,
                  kCGImageSourceThumbnailMaxPixelSize: maxPixel
              ] as CFDictionary)
        else { return nil }
        return UIImage(cgImage: frame)
    }

    private static func decodeChunk(
        source: CGImageSource,
        range: Range<Int>,
        maxPixel: CGFloat
    ) -> (frames: [UIImage], delays: [TimeInterval]) {
        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceThumbnailMaxPixelSize: maxPixel,
            kCGImageSourceShouldCacheImmediately: true
        ]
        var frames: [UIImage] = []
        var delays: [TimeInterval] = []
        frames.reserveCapacity(range.count)
        delays.reserveCapacity(range.count)
        for index in range {
            guard let frame = CGImageSourceCreateThumbnailAtIndex(source, index, options as CFDictionary)
            else { continue }
            frames.append(UIImage(cgImage: frame))
            delays.append(frameDelay(source, index))
        }
        return (frames, delays)
    }

    private static func frameDelay(_ source: CGImageSource, _ index: Int) -> TimeInterval {
        guard let props = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any],
              let webp = props[kCGImagePropertyWebPDictionary] as? [CFString: Any]
        else { return AnimatedMascotPlayback.fallbackFrameDelay }
        let delay = (webp[kCGImagePropertyWebPUnclampedDelayTime] as? Double)
            ?? (webp[kCGImagePropertyWebPDelayTime] as? Double)
        return AnimatedMascotPlayback.normalizedDelay(delay ?? 0)
    }
}

/// One in-flight progressive decode. Mutated on the main thread only — the background chunks receive
/// an immutable frame range and hand their images back through the main queue.
private final class DecodeJob {
    let key: NSString
    let source: CGImageSource
    let count: Int
    let firstChunk: Int
    /// The next frame index to decode, which runs ahead of `frames.count` when ImageIO refuses a frame.
    var cursor = 0
    var frames: [UIImage] = []
    var delays: [TimeInterval] = []
    var published = 0
    var isVisible: Bool
    var isPinned: Bool
    var subscribers: [(MascotAnimation) -> Void] = []

    init(key: NSString, source: CGImageSource, count: Int, isVisible: Bool, isPinned: Bool) {
        self.key = key
        self.source = source
        self.count = count
        firstChunk = AnimatedMascotPlayback.firstChunkSize(frameCount: count)
        self.isVisible = isVisible
        self.isPinned = isPinned
    }

    var snapshot: MascotAnimation {
        MascotAnimation(frames: frames, delays: delays, isComplete: cursor >= count)
    }
}
