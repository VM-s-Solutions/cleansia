import ImageIO
import SwiftUI
import UIKit

public enum AnimatedMascot: String {
    case cleaningInProgress = "mascot_cleaning_in_progress"
    case welcoming = "mascot_welcoming"
}

/// Pure playback decisions for `AnimatedImageView`, factored out so they are unit-testable
/// without a running UIKit view or a real asset.
enum AnimatedMascotPlayback {
    /// Whether a render pass should (re)start playback: a brand-new view (`force`), or a
    /// changed mascot/loop. An identical, non-forced re-render is skipped so a running loop
    /// isn't restarted and no work is redone.
    static func shouldRestart(currentName: String?, currentLoop: Bool?, name: String, loop: Bool, force: Bool) -> Bool {
        force || currentName != name || currentLoop != loop
    }

    /// `UIImageView.animationRepeatCount`: infinite (0) for a loop, once (1) for a one-shot —
    /// which then freezes on its final frame via the pinned `image`.
    static func animationRepeatCount(loop: Bool) -> Int {
        loop ? 0 : 1
    }

    /// A decode delivered after a newer render superseded it must be dropped.
    static func isSuperseded(token: Int, generation: Int) -> Bool {
        token != generation
    }

    /// Whether a view that just entered a window should (re)start playback: it has frames staged and
    /// isn't already animating. Guards the off-window `startAnimating` that leaves the mascot frozen.
    static func shouldResumePlayback(hasWindow: Bool, hasFrames: Bool, isAnimating: Bool) -> Bool {
        hasWindow && hasFrames && !isAnimating
    }

    /// Total loop duration from the summed per-frame delays, with a ~30fps fallback when the
    /// source reports none.
    static func totalDuration(summedDelays: TimeInterval, frameCount: Int) -> TimeInterval {
        summedDelays > 0 ? summedDelays : Double(max(frameCount, 1)) / 30.0
    }
}

/// Plays an animated WebP mascot bundled as an asset-catalog data asset,
/// mirroring Android's Coil-backed `MascotAnimation`. With `loop: false` the
/// animation plays once and freezes on the final frame. Falls back to the
/// static mascot image when the data asset is missing or cannot be animated.
///
/// Performance: the frames are decoded ONCE — downsampled, off the main thread —
/// then cached and played by `UIImageView`'s built-in frame animator. This
/// avoids the previous `CGAnimateImageDataWithBlock` path, which re-decoded the
/// full-size WebP frame-by-frame on the MAIN thread on every loop (and reloaded
/// the ~1.7 MB asset on every SwiftUI body evaluation), the source of the jank
/// on the order-detail hero and the busy loader.
public struct AnimatedMascotView: View {
    private let mascot: AnimatedMascot
    private let data: Data?
    private let loop: Bool
    private let fallback: Mascot
    private let bundle: Bundle

    public init(_ mascot: AnimatedMascot, loop: Bool = true, fallback: Mascot, bundle: Bundle = .main) {
        self.mascot = mascot
        data = MascotAssetCache.shared.data(for: mascot, bundle: bundle)
        self.loop = loop
        self.fallback = fallback
        self.bundle = bundle
    }

    public var body: some View {
        if let data {
            AnimatedImageView(name: mascot.rawValue, data: data, loop: loop, fallback: fallback, bundle: bundle)
        } else {
            Image(fallback.rawValue, bundle: bundle)
                .resizable()
                .scaledToFit()
        }
    }
}

/// One decoded, ready-to-play animation: pre-rendered frames plus the total loop
/// duration (sum of the WebP per-frame delays).
struct MascotAnimation {
    let frames: [UIImage]
    let duration: TimeInterval
}

/// Process-wide caches for the raw asset `Data` and the decoded frames, so the
/// heavy work happens at most once per asset. Both are `NSCache`, so the system
/// evicts them under memory pressure.
final class MascotAssetCache {
    static let shared = MascotAssetCache()

    private let dataCache = NSCache<NSString, NSData>()
    private let frameCache = NSCache<NSString, FrameBox>()
    private let decodeQueue = DispatchQueue(label: "cleansia.mascot.decode", qos: .userInitiated)

    /// Frames are decoded at the asset's native size (360 px). The mascots render up to 220 pt
    /// (booking/membership success hero) and 140 pt (loader / order hero) — i.e. ~660 / ~420 px
    /// at @3x, both already above native — so there is no useful detail to downsample away.
    /// Trade-off: the full loop is held in memory (~65 MB for 125 frames) in an NSCache the system
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
        frameCache.object(forKey: frameKey(name))?.animation
    }

    /// Decode all frames off the main thread, cache them, and call back on the main thread. If
    /// already cached, calls back synchronously. The frame cache is keyed by asset name + size;
    /// every call site uses the main bundle, where asset names are unique.
    func loadAnimation(name: String, data: Data, completion: @escaping (MascotAnimation?) -> Void) {
        if let hit = cachedAnimation(name: name) {
            completion(hit)
            return
        }
        let maxPixel = maxPixel
        let key = frameKey(name)
        decodeQueue.async { [weak self] in
            // Re-check: a concurrent first-load may have populated the cache while this was queued.
            if let hit = self?.frameCache.object(forKey: key)?.animation {
                DispatchQueue.main.async { completion(hit) }
                return
            }
            let animation = MascotAssetCache.decode(data: data, maxPixel: maxPixel)
            if let animation {
                self?.frameCache.setObject(FrameBox(animation), forKey: key)
            }
            DispatchQueue.main.async { completion(animation) }
        }
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

    private static func decode(data: Data, maxPixel: CGFloat) -> MascotAnimation? {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil) else { return nil }
        let count = CGImageSourceGetCount(source)
        guard count > 0 else { return nil }

        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceThumbnailMaxPixelSize: maxPixel,
            kCGImageSourceShouldCacheImmediately: true
        ]

        var frames: [UIImage] = []
        frames.reserveCapacity(count)
        var total: TimeInterval = 0
        for index in 0 ..< count {
            guard let frame = CGImageSourceCreateThumbnailAtIndex(source, index, options as CFDictionary)
            else { continue }
            frames.append(UIImage(cgImage: frame))
            total += frameDelay(source, index)
        }
        guard !frames.isEmpty else { return nil }
        let duration = AnimatedMascotPlayback.totalDuration(summedDelays: total, frameCount: frames.count)
        return MascotAnimation(frames: frames, duration: duration)
    }

    private static func frameDelay(_ source: CGImageSource, _ index: Int) -> TimeInterval {
        guard let props = CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any],
              let webp = props[kCGImagePropertyWebPDictionary] as? [CFString: Any]
        else { return 1.0 / 30.0 }
        let delay = (webp[kCGImagePropertyWebPUnclampedDelayTime] as? Double)
            ?? (webp[kCGImagePropertyWebPDelayTime] as? Double)
        if let delay, delay > 0 { return delay }
        return 1.0 / 30.0
    }
}

private struct AnimatedImageView: UIViewRepresentable {
    let name: String
    let data: Data
    let loop: Bool
    let fallback: Mascot
    let bundle: Bundle

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    func makeUIView(context: Context) -> UIImageView {
        let view = AnimatingImageView()
        view.contentMode = .scaleAspectFit
        view.setContentHuggingPriority(.defaultLow, for: .horizontal)
        view.setContentHuggingPriority(.defaultLow, for: .vertical)
        view.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        view.setContentCompressionResistancePriority(.defaultLow, for: .vertical)
        // A brand-new view always needs its frames, even if the coordinator was
        // already showing this mascot in a previous (reused) view.
        context.coordinator.render(self, on: view, force: true)
        return view
    }

    func updateUIView(_ view: UIImageView, context: Context) {
        context.coordinator.render(self, on: view, force: false)
    }

    final class Coordinator {
        private var currentName: String?
        private var currentLoop: Bool?
        private var generation = 0

        func render(_ representable: AnimatedImageView, on view: UIImageView, force: Bool) {
            let name = representable.name
            let loop = representable.loop
            guard AnimatedMascotPlayback.shouldRestart(
                currentName: currentName, currentLoop: currentLoop, name: name, loop: loop, force: force
            ) else { return }
            currentName = name
            currentLoop = loop
            generation += 1
            let token = generation // snapshot; a newer mascot supersedes this run
            let cache = MascotAssetCache.shared

            if let animation = cache.cachedAnimation(name: name) {
                apply(animation, loop: loop, on: view)
                return
            }

            // Stop any prior run before the poster so a reused, still-looping view can't keep
            // showing the previous mascot until the new decode lands.
            view.stopAnimating()
            view.animationImages = nil
            // Instant poster while the full loop decodes off the main thread.
            view.image = cache.posterFrame(data: representable.data)
                ?? UIImage(named: representable.fallback.rawValue, in: representable.bundle, with: nil)

            cache.loadAnimation(name: name, data: representable.data) { [weak self, weak view] animation in
                guard let self, let view, let animation,
                      !AnimatedMascotPlayback.isSuperseded(token: token, generation: generation)
                else { return }
                apply(animation, loop: loop, on: view)
            }
        }

        private func apply(_ animation: MascotAnimation, loop: Bool, on view: UIImageView) {
            guard animation.frames.count > 1 else {
                view.stopAnimating()
                view.animationImages = nil
                view.image = animation.frames.first
                return
            }
            view.animationImages = animation.frames
            view.animationDuration = animation.duration
            view.animationRepeatCount = AnimatedMascotPlayback.animationRepeatCount(loop: loop)
            // The `image` shows through once a one-shot stops animating, so pin
            // the final frame there — that is the frozen ending pose.
            view.image = animation.frames.last
            // startAnimating() only takes effect once the view is on a window; apply() can run
            // synchronously (cache hit) from makeUIView before that, so AnimatingImageView also
            // (re)starts it in didMoveToWindow. Calling here too covers the already-on-window case.
            view.startAnimating()
        }
    }
}

/// A UIImageView that (re)starts its frame animation whenever it enters a window. UIKit's built-in
/// animator only runs while the view is in a window and is dropped by an off-window
/// `startAnimating()`; without this, a mascot applied synchronously from `makeUIView` (a frame-cache
/// hit, i.e. every appearance after the first) would freeze on a single frame.
private final class AnimatingImageView: UIImageView {
    override func didMoveToWindow() {
        super.didMoveToWindow()
        if AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: window != nil,
            hasFrames: animationImages?.isEmpty == false,
            isAnimating: isAnimating
        ) {
            startAnimating()
        }
    }
}
