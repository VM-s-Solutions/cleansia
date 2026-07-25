import ImageIO
import SwiftUI
import UIKit

public enum AnimatedMascot: String {
    case cleaningInProgress = "mascot_cleaning_in_progress"
    case welcoming = "mascot_welcoming"
}

/// Pure playback decisions for `AnimatedImageView`, factored out so they are unit-testable
/// without a running UIKit view or a real asset.
///
/// Playback runs over a PARTIAL, GROWING frame buffer. ImageIO cannot random-access an animated
/// WebP — every `CGImageSourceCreateThumbnailAtIndex` replays the file from frame 0, so decoding
/// the whole loop is O(n²) (measured: 1151 ms for the 63-frame cleaning mascot, 499 ms for the
/// 50-frame welcoming one). Waiting for that means seconds of static poster, so the decoder
/// publishes a prefix within milliseconds and keeps appending — which means every decision below
/// has to cope with "the frame I want next does not exist yet".
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

    /// Tick faster than the animation's own rate: ticking at exactly the frame rate makes each frame
    /// land on one or two ticks, which reads as judder.
    private static let displayLinkOversample = 2.0
    private static let minFramesPerSecond = 30
    private static let maxFramesPerSecond = 60

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
    /// is already on screen, capped by what is left.
    static func nextChunkLength(published: Int, remaining: Int, firstChunk: Int) -> Int {
        guard remaining > 0 else { return 0 }
        return min(remaining, max(published > 0 ? published : firstChunk, 1))
    }

    /// Whether the decoded prefix should be handed to the view yet. Publishing every frame would
    /// churn the view for no gain; publishing too rarely delays the start. So: the first chunk, then
    /// each doubling, and always the finished animation.
    static func shouldPublish(decoded: Int, published: Int, isComplete: Bool, firstChunk: Int) -> Bool {
        guard decoded > published else { return false }
        if isComplete { return true }
        if published == 0 { return decoded >= firstChunk }
        return decoded >= published * 2
    }

    /// The display link's tick rate, derived from the shortest frame delay in the animation.
    static func preferredFramesPerSecond(shortestDelay: TimeInterval) -> Int {
        let ticks = displayLinkOversample / normalizedDelay(shortestDelay)
        guard ticks > Double(minFramesPerSecond) else { return minFramesPerSecond }
        guard ticks < Double(maxFramesPerSecond) else { return maxFramesPerSecond }
        return Int(ticks.rounded(.up))
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
/// makes decoding a whole animated WebP quadratic (seconds for the shipped mascots).
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

    /// Decode + pin a mascot's frames off the main thread AHEAD of the view that shows it. Call this as a
    /// screen loads (e.g. an in-progress order's ViewModel) so the heavy 63-frame cleaning loop is whole
    /// when its hero renders, rather than still growing. Idempotent; call on the main thread. The public
    /// seam onto the module-internal `MascotAssetCache`.
    public static func prewarm(_ mascot: AnimatedMascot, bundle: Bundle = .main) {
        MascotAssetCache.shared.prewarm(mascot, bundle: bundle)
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
                view.prepare(poster: animation.frames.first, loop: loop)
                view.update(animation)
                return
            }

            // Clear any prior run before the poster so a reused, still-playing view can't keep
            // showing the previous mascot until the new frames land.
            view.prepare(
                poster: cache.posterFrame(data: representable.data)
                    ?? UIImage(named: representable.fallback.rawValue, in: representable.bundle, with: nil),
                loop: loop
            )

            cache.loadAnimation(name: name, data: representable.data) { [weak self, weak view] animation in
                guard let self, let view,
                      !AnimatedMascotPlayback.isSuperseded(token: token, generation: generation)
                else { return }
                view.update(animation)
            }
        }
    }
}

/// Plays a growing frame buffer off a `CADisplayLink`.
///
/// `UIImageView.animationImages` can't do this: it owns its own timeline, so every appended chunk
/// would mean re-assigning `animationImages` and restarting the loop from frame 0 — a visible jump on
/// every publish. A display-link playhead just keeps stepping, and gets the loop's real per-frame
/// delays (the built-in animator only takes one average duration for the whole sequence).
final class MascotAnimationView: UIImageView {
    private var frames: [UIImage] = []
    private var delays: [TimeInterval] = []
    private var isComplete = false
    private var loop = true
    private var playhead = AnimatedMascotPlayback.Playhead()
    private var framesPerSecond = 60
    private var displayLink: CADisplayLink?

    private var isPlaying: Bool {
        displayLink.map { !$0.isPaused } ?? false
    }

    /// Start over: a new mascot (or a new view) drops the old buffer and playhead.
    func prepare(poster: UIImage?, loop: Bool) {
        displayLink?.isPaused = true
        frames = []
        delays = []
        isComplete = false
        self.loop = loop
        playhead = AnimatedMascotPlayback.Playhead(frameStart: CACurrentMediaTime())
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
        framesPerSecond = AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: delays.min() ?? 0)
        displayLink?.preferredFramesPerSecond = framesPerSecond
        resumeIfNeeded()
    }

    func teardown() {
        displayLink?.invalidate()
        displayLink = nil
    }

    override func didMoveToWindow() {
        super.didMoveToWindow()
        guard window != nil else {
            displayLink?.isPaused = true
            return
        }
        resumeIfNeeded()
    }

    deinit {
        displayLink?.invalidate()
    }

    private func resumeIfNeeded() {
        guard AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: window != nil,
            hasFrames: frames.count > 1,
            isAnimating: isPlaying,
            isFinished: playhead.isFinished
        ) else { return }
        playhead.frameStart = CACurrentMediaTime()
        if let displayLink {
            displayLink.isPaused = false
            return
        }
        let link = CADisplayLink(target: DisplayLinkProxy(self), selector: #selector(DisplayLinkProxy.tick))
        link.preferredFramesPerSecond = framesPerSecond
        link.add(to: .main, forMode: .common)
        displayLink = link
    }

    fileprivate func tick(_ link: CADisplayLink) {
        let next = AnimatedMascotPlayback.advance(
            playhead,
            now: link.timestamp,
            delays: delays,
            isComplete: isComplete,
            loop: loop
        )
        guard next != playhead else { return }
        let moved = next.index != playhead.index
        playhead = next
        if next.isFinished {
            link.isPaused = true
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
        view?.tick(link)
    }
}
