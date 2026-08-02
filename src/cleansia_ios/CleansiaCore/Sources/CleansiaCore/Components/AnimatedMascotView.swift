import ImageIO
import SwiftUI
import UIKit

public enum AnimatedMascot: String {
    case cleaningInProgress = "mascot_cleaning_in_progress"
    case welcoming = "mascot_welcoming"

    /// The asset-catalog data assets that make up this animation, in playback order.
    ///
    /// ImageIO replays an animated WebP from frame 0 for EVERY frame it is asked for, so ONE file of n
    /// frames costs O(n²) to decode: measured 4263 ms for the 125-frame cleaning loop, against 637 ms for
    /// the same 125 frames split across 7 files — the playhead catches a single file's decoder and freezes
    /// mid-loop. So the long loop ships as segments that concatenate back into one animation (identical
    /// frames, identical per-frame delays, ~8 KB less than the single file); short ones stay whole.
    ///
    /// Segment 0 keeps the bare mascot name so the catalog entry the rest of the app knows still exists.
    public var segmentNames: [String] {
        (0 ..< segmentCount).map { $0 == 0 ? rawValue : "\(rawValue)_\($0)" }
    }

    /// The pixel size frames are decoded at — a pure MEMORY lever, not a speed one (measured: 360/288/240/180
    /// all decode 125 frames in ~4.2 s). Both mascots decode at the asset's native 360 px: the heroes render
    /// in a 140 pt box, which is 420 px on an @3x screen, so 360 already upscales slightly (1.17x) and
    /// dropping to 240 would make it 1.75x — visibly soft on the app's most prominent animation. The memory
    /// that buys (61.8 MiB for the 125-frame loop) is bounded by the single-slot pin below.
    var maxPixel: CGFloat {
        360
    }

    private var segmentCount: Int {
        switch self {
        case .cleaningInProgress: 7
        case .welcoming: 1
        }
    }
}

/// Pure playback decisions for `AnimatedImageView`, factored out so they are unit-testable
/// without a running UIKit view or a real asset.
///
/// Playback runs over a PARTIAL, GROWING frame buffer. ImageIO cannot random-access an animated
/// WebP — every `CGImageSourceCreateThumbnailAtIndex` replays the file from frame 0, so decoding a
/// whole loop is O(n²) (measured: 4263 ms for 125 frames in one file, 637 ms for the same 125 frames
/// across 7 files, 499 ms for the 50-frame welcoming one). Waiting for that means seconds of static
/// poster, so the decoder publishes a prefix within milliseconds and keeps appending — which means
/// every decision below has to cope with "the frame I want next does not exist yet".
enum AnimatedMascotPlayback {
    /// ImageIO reports no delay for some frames and WebP permits a literal 0; either would spin the
    /// playhead at display-link speed.
    static let fallbackFrameDelay: TimeInterval = 1.0 / 30.0

    /// Frames 0-3 of the shipped mascots cost ~7 ms of ImageIO work in total (measured) — under half
    /// a display frame — and buy ~165 ms of playback at their 24 fps, far more than the next chunk
    /// needs. A larger first chunk only delays the first animated frame.
    private static let firstChunkFrames = 4

    /// Past this many overdue frames the view was not "slightly late", it was stalled (backgrounded,
    /// a slow decode). Replaying that backlog at display-link speed sprints through the loop, so the
    /// frame clock is restarted instead.
    private static let overdueRebaseFrames = 4.0

    /// A chunk this size costs a few ms even at the tail of a segment, so frames land steadily. Without a
    /// cap the doubling makes the LAST chunk cover half the animation and publish only once all of it is
    /// decoded — the playhead then holds one frame for that entire chunk, which is the visible drag.
    private static let maxChunkFrames = 8

    /// Where playback sits inside the (possibly still growing) frame buffer. `frameStart` is a
    /// `CACurrentMediaTime`-based timestamp; `isFinished` is the frozen end of a one-shot.
    struct Playhead: Equatable {
        var index = 0
        var frameStart: TimeInterval = 0
        var isFinished = false
    }

    /// Whether a render pass should (re)start playback: a brand-new view (`force`), or a
    /// changed mascot/loop. An identical, non-forced re-render is skipped so a running loop
    /// isn't restarted and no work is redone.
    static func shouldRestart(currentName: String?, currentLoop: Bool?, name: String, loop: Bool, force: Bool) -> Bool {
        force || currentName != name || currentLoop != loop
    }

    /// A decode delivered after a newer render superseded it must be dropped.
    static func isSuperseded(token: Int, generation: Int) -> Bool {
        token != generation
    }

    /// Whether a view that just entered a window should (re)start playback: it has frames staged,
    /// isn't already animating, and hasn't already FINISHED. Guards two distinct bugs: the off-window
    /// start that leaves the mascot frozen, and — for a `loop: false` mascot that already settled on its
    /// final frame — restarting a display link that would then fire every frame forever with nothing to
    /// do, waking the main thread for the life of the screen.
    static func shouldResumePlayback(hasWindow: Bool, hasFrames: Bool, isAnimating: Bool, isFinished: Bool) -> Bool {
        hasWindow && hasFrames && !isAnimating && !isFinished
    }

    static func normalizedDelay(_ delay: TimeInterval) -> TimeInterval {
        delay.isFinite && delay > 0 ? delay : fallbackFrameDelay
    }

    /// Step the playhead by AT MOST one frame, over the frames decoded so far.
    static func advance(
        _ playhead: Playhead,
        now: TimeInterval,
        delays: [TimeInterval],
        isComplete: Bool,
        loop: Bool
    ) -> Playhead {
        guard !playhead.isFinished, !delays.isEmpty else { return playhead }
        guard playhead.index < delays.count else {
            return Playhead(index: delays.count - 1, frameStart: now)
        }
        let delay = normalizedDelay(delays[playhead.index])
        let elapsed = now - playhead.frameStart
        guard elapsed >= delay else { return playhead }

        let nextStart = elapsed > delay * overdueRebaseFrames ? now : playhead.frameStart + delay
        let next = playhead.index + 1
        if next < delays.count {
            return Playhead(index: next, frameStart: nextStart)
        }
        guard isComplete else {
            // The decoder is behind the playhead: hold the newest frame and restart its clock, so the
            // frame that lands next gets a full delay instead of flashing past.
            return Playhead(index: playhead.index, frameStart: now)
        }
        guard loop else {
            return Playhead(index: playhead.index, frameStart: playhead.frameStart, isFinished: true)
        }
        return Playhead(index: 0, frameStart: nextStart)
    }

    /// How many frames to decode before playback can start.
    static func firstChunkSize(frameCount: Int) -> Int {
        max(min(frameCount, firstChunkFrames), 0)
    }

    /// How many frames the next decode chunk should cover: the first chunk, then a doubling of what
    /// is already on screen, capped by `maxChunkFrames` and by what is left.
    static func nextChunkLength(published: Int, remaining: Int, firstChunk: Int) -> Int {
        guard remaining > 0 else { return 0 }
        let grown = published > 0 ? published : firstChunk
        return min(remaining, max(min(grown, maxChunkFrames), 1))
    }

    /// Where the next chunk sits inside the segment files, or `nil` when every frame is decoded.
    ///
    /// A chunk never straddles two segments: each `CGImageSource` replays only its own frames, and that
    /// bound is the entire point of splitting the asset.
    static func nextChunkSlice(
        published: Int,
        cursor: Int,
        counts: [Int],
        firstChunk: Int
    ) -> ChunkSlice? {
        var segment = 0
        var local = cursor
        while segment < counts.count, local >= counts[segment] {
            local -= counts[segment]
            segment += 1
        }
        guard segment < counts.count else { return nil }
        let remaining = counts.reduce(0, +) - cursor
        let length = min(
            nextChunkLength(published: published, remaining: remaining, firstChunk: firstChunk),
            counts[segment] - local
        )
        guard length > 0 else { return nil }
        return ChunkSlice(segment: segment, range: local ..< (local + length))
    }

    struct ChunkSlice: Equatable {
        let segment: Int
        let range: Range<Int>
    }

    /// Whether the decoded prefix should be handed to the view yet: not before the FIRST chunk is whole
    /// (one frame on screen would just flash), and then on every chunk. Chunks are capped, so that is a
    /// steady drip rather than the churn of publishing frame by frame — and it makes the cap the only
    /// knob on how long the playhead can be starved. Holding frames back for a doubling instead was the
    /// visible drag: at `published: 64` nothing reached the screen until 61 more frames were decoded.
    static func shouldPublish(decoded: Int, published: Int, isComplete: Bool, firstChunk: Int) -> Bool {
        guard decoded > published else { return false }
        return isComplete || published > 0 || decoded >= firstChunk
    }
}

/// Plays an animated WebP mascot bundled as an asset-catalog data asset,
/// mirroring Android's Coil-backed `MascotAnimation`. With `loop: false` the
/// animation plays once and freezes on the final frame. Falls back to the
/// static mascot image when the data asset is missing or cannot be animated.
///
/// Performance: frames are decoded ONCE per asset — downsampled, off the main thread — and cached.
/// The decode publishes PROGRESSIVELY: the first few frames land within milliseconds and playback
/// starts there, growing into the full loop without a restart, because ImageIO's frame-0 replay
/// makes decoding a whole animated WebP quadratic (seconds for the shipped mascots). A long mascot is
/// bundled as several segment files that decode in order and concatenate into one continuous loop.
public struct AnimatedMascotView: View {
    private let asset: MascotAsset?
    private let loop: Bool
    private let fallback: Mascot

    public init(_ mascot: AnimatedMascot, loop: Bool = true, fallback: Mascot) {
        asset = MascotAssetCache.shared.asset(for: mascot)
        self.loop = loop
        self.fallback = fallback
    }

    public var body: some View {
        if let asset {
            AnimatedImageView(asset: asset, loop: loop, fallback: fallback)
        } else {
            fallback.image
                .resizable()
                .scaledToFit()
        }
    }

    /// Decode + pin a mascot's frames off the main thread AHEAD of the view that shows it. Call this as a
    /// screen loads (e.g. an in-progress order's ViewModel) so the heavy 125-frame cleaning loop is whole
    /// when its hero renders, rather than still growing. Idempotent; call on the main thread. The public
    /// seam onto the module-internal `MascotAssetCache`.
    public static func prewarm(_ mascot: AnimatedMascot) {
        MascotAssetCache.shared.prewarm(mascot)
    }
}

private struct AnimatedImageView: UIViewRepresentable {
    let asset: MascotAsset
    let loop: Bool
    let fallback: Mascot

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    func makeUIView(context: Context) -> MascotAnimationView {
        let view = MascotAnimationView()
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

    func updateUIView(_ view: MascotAnimationView, context: Context) {
        context.coordinator.render(self, on: view, force: false)
    }

    static func dismantleUIView(_ view: MascotAnimationView, coordinator _: Coordinator) {
        view.teardown()
    }

    final class Coordinator {
        private var currentName: String?
        private var currentLoop: Bool?
        private var generation = 0

        func render(_ representable: AnimatedImageView, on view: MascotAnimationView, force: Bool) {
            let asset = representable.asset
            let loop = representable.loop
            guard AnimatedMascotPlayback.shouldRestart(
                currentName: currentName, currentLoop: currentLoop, name: asset.name, loop: loop, force: force
            ) else { return }
            currentName = asset.name
            currentLoop = loop
            generation += 1
            let token = generation // snapshot; a newer mascot supersedes this run
            let cache = MascotAssetCache.shared

            if let animation = cache.cachedAnimation(asset) {
                view.prepare(poster: animation.frames.first, loop: loop)
                view.update(animation)
                return
            }

            // Clear any prior run before the poster so a reused, still-playing view can't keep
            // showing the previous mascot until the new frames land.
            view.prepare(
                poster: cache.posterFrame(asset)
                    ?? UIImage(named: representable.fallback.rawValue, in: MascotAssets.bundle, with: nil),
                loop: loop
            )

            cache.loadAnimation(asset) { [weak self, weak view] animation in
                guard let self, let view,
                      !AnimatedMascotPlayback.isSuperseded(token: token, generation: generation)
                else { return }
                view.update(animation)
            }
        }
    }
}

/// What drives the playhead: `CADisplayLink` in production, a stub under test. Everything the view
/// asks of the link is here, so a test can assert that playback stopped without waiting on a run loop.
protocol MascotTicker: AnyObject {
    var isPaused: Bool { get set }
    func invalidate()
}

extension CADisplayLink: MascotTicker {}

/// Plays a growing frame buffer off a `CADisplayLink`.
///
/// `UIImageView.animationImages` can't do this: it owns its own timeline, so every appended chunk
/// would mean re-assigning `animationImages` and restarting the loop from frame 0 — a visible jump on
/// every publish. A display-link playhead just keeps stepping, and gets the loop's real per-frame
/// delays (the built-in animator only takes one average duration for the whole sequence).
///
/// The link runs at the DISPLAY's rate and `advance` decides from the timestamp when a frame changes.
/// Asking for the animation's own rate instead (`preferredFramesPerSecond`) is what made it judder:
/// CADisplayLink quantizes that request to a factor of the panel rate, so a 24 fps loop's 49 became a
/// 30 Hz tick, which holds alternate frames for 67 ms instead of 42.
final class MascotAnimationView: UIImageView {
    private var frames: [UIImage] = []
    private var delays: [TimeInterval] = []
    private var isComplete = false
    private var loop = true
    private var playhead = AnimatedMascotPlayback.Playhead()
    private var ticker: MascotTicker?

    /// The playhead's clock. Every timestamp playback compares against comes from here or from the
    /// ticker, so a test can replay a frame sequence over literal times instead of racing the run loop.
    var currentTime: () -> TimeInterval = CACurrentMediaTime

    /// Builds the ticker for a given view. The view is a PARAMETER rather than a capture so the default
    /// below stays a context-free closure that cannot retain the view it drives — the whole point of
    /// `DisplayLinkProxy`.
    var makeTicker: (MascotAnimationView) -> MascotTicker = { view in
        let link = CADisplayLink(target: DisplayLinkProxy(view), selector: #selector(DisplayLinkProxy.tick))
        link.preferredFrameRateRange = .default
        link.add(to: .main, forMode: .common)
        return link
    }

    private var isPlaying: Bool {
        ticker.map { !$0.isPaused } ?? false
    }

    /// Start over: a new mascot (or a new view) drops the old buffer and playhead.
    func prepare(poster: UIImage?, loop: Bool) {
        ticker?.isPaused = true
        frames = []
        delays = []
        isComplete = false
        self.loop = loop
        playhead = AnimatedMascotPlayback.Playhead(frameStart: currentTime())
        image = poster
    }

    /// Take a longer prefix of the same animation WITHOUT touching the playhead — that is what makes
    /// the growing buffer invisible: no restart, no jump, the loop simply gets longer until it's whole.
    func update(_ animation: MascotAnimation) {
        guard animation.frames.count >= frames.count else { return }
        frames = animation.frames
        delays = animation.delays
        isComplete = animation.isComplete
        if image == nil { image = frames.first }
        resumeIfNeeded()
    }

    func teardown() {
        ticker?.invalidate()
        ticker = nil
    }

    override func didMoveToWindow() {
        super.didMoveToWindow()
        guard window != nil else {
            ticker?.isPaused = true
            return
        }
        resumeIfNeeded()
    }

    deinit {
        ticker?.invalidate()
    }

    private func resumeIfNeeded() {
        guard AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: window != nil,
            hasFrames: frames.count > 1,
            isAnimating: isPlaying,
            isFinished: playhead.isFinished
        ) else { return }
        playhead.frameStart = currentTime()
        if let ticker {
            ticker.isPaused = false
            return
        }
        ticker = makeTicker(self)
    }

    /// One tick of the ticker's clock.
    func step(now: TimeInterval) {
        let next = AnimatedMascotPlayback.advance(
            playhead,
            now: now,
            delays: delays,
            isComplete: isComplete,
            loop: loop
        )
        guard next != playhead else { return }
        let moved = next.index != playhead.index
        playhead = next
        if next.isFinished {
            ticker?.isPaused = true
            image = frames.last
            return
        }
        if moved, next.index < frames.count { image = frames[next.index] }
    }
}

/// `CADisplayLink` retains its target, and the run loop retains the link: a direct target would keep
/// the view alive forever and never reach `deinit`.
private final class DisplayLinkProxy {
    private weak var view: MascotAnimationView?

    init(_ view: MascotAnimationView) {
        self.view = view
    }

    @objc func tick(_ link: CADisplayLink) {
        view?.step(now: link.timestamp)
    }
}
