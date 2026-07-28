import UIKit
import XCTest
@testable import CleansiaCore

/// Drives `MascotAnimationView` over a synthetic frame buffer and a synthetic clock. The pure playhead
/// is covered by `AnimatedMascotPlaybackTests`; this covers the wiring around it — that playback starts
/// on a PARTIAL buffer, keeps going when the buffer grows, wraps, freezes a one-shot, stops off-window,
/// and does not outlive the view.
///
/// Every tick is a literal timestamp handed to `step(now:)`, so each expected frame sequence is
/// arithmetic and cannot be perturbed by how loaded the machine is. The two intervals are exact binary
/// fractions (1/4 and 5/16) so no `elapsed >= delay` comparison lands on a floating-point knife edge:
/// with the obvious 0.05 s frame, three delays sum to 0.15000000000000002 and a tick placed on that
/// boundary becomes a coin flip.
final class MascotAnimationViewTests: XCTestCase {
    private static let frameDelay: TimeInterval = 0.25
    private static let tickInterval: TimeInterval = 0.3125

    private var window: UIWindow?
    private var clock = TestClock()
    private var ticker = FakeTicker()

    override func setUp() {
        super.setUp()
        clock = TestClock()
        ticker = FakeTicker()
    }

    override func tearDown() {
        window = nil
        super.tearDown()
    }

    func testAPartialBufferPlaysAndThenHoldsItsNewestFrame() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: false))

        let painted = tick(view, 6, of: images)

        XCTAssertEqual(
            painted,
            [1, 2, 2, 2, 2, 2],
            "playback runs over the decoded prefix, then holds instead of wrapping"
        )
    }

    func testGrowingTheBufferContinuesInsteadOfRestarting() {
        let images = makeFrames(6)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(Array(images.prefix(3)), isComplete: false))
        XCTAssertEqual(tick(view, 3, of: images), [1, 2, 2])

        view.update(animation(images, isComplete: true))
        let painted = tick(view, 6, of: images)

        XCTAssertEqual(painted, [3, 4, 5, 0, 1, 2], "the appended frames continue the loop with no jump back to 0")
    }

    func testACompleteLoopWraps() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))

        let painted = tick(view, 6, of: images)

        XCTAssertEqual(painted, [1, 2, 0, 1, 2, 0], "a looping mascot keeps stepping past its final frame")
    }

    func testAOneShotFreezesOnItsFinalFrame() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: false)
        view.update(animation(images, isComplete: true))

        let painted = tick(view, 6, of: images)

        XCTAssertEqual(painted, [1, 2, 2, 2, 2, 2], "a one-shot never wraps")
        XCTAssertTrue(
            ticker.isPaused,
            "and it stops ticking rather than waking the main thread for the life of the screen"
        )
    }

    func testPlaybackStopsWhileOffWindow() {
        let images = makeFrames(6)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))
        XCTAssertEqual(tick(view, 2, of: images), [1, 2])

        view.removeFromSuperview()

        XCTAssertTrue(ticker.isPaused, "an off-window mascot must not burn display-link ticks")
        XCTAssertEqual(tick(view, 3, of: images), [2, 2, 2], "so its playhead stays where it stopped")
    }

    func testPlaybackResumesWhenTheViewReturnsToAWindow() {
        let images = makeFrames(6)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))
        tick(view, 2, of: images)
        view.removeFromSuperview()
        tick(view, 3, of: images)

        show(view)

        XCTAssertFalse(ticker.isPaused)
        XCTAssertEqual(
            tick(view, 4, of: images),
            [3, 4, 5, 0],
            "the playhead resumes where it stopped, on a restarted frame clock"
        )
    }

    func testTearingDownReleasesTheTicker() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))
        tick(view, 1, of: images)

        view.teardown()

        XCTAssertTrue(ticker.isInvalidated, "dismantling the SwiftUI view must take the link off the run loop")
    }

    func testTheDisplayLinkDoesNotKeepTheViewAlive() {
        weak var released: MascotAnimationView?
        autoreleasepool {
            // Deliberately on the REAL CADisplayLink: what is under test is the weak proxy that keeps the
            // run loop's link from retaining the view, and it is the only thing a stub could not show.
            let view = makeLiveView()
            released = view
            view.prepare(poster: nil, loop: true)
            view.update(animation(makeFrames(4), isComplete: true))
            RunLoop.current.run(until: Date().addingTimeInterval(0.1))
            view.removeFromSuperview()
        }

        XCTAssertNil(released, "a running display link must not retain the mascot view")
    }

    // MARK: helpers

    /// A view in a window, on the test's clock and ticker instead of a real display link.
    private func makeView() -> MascotAnimationView {
        let view = MascotAnimationView()
        view.currentTime = { [clock] in clock.now }
        view.makeTicker = { [ticker] _ in ticker }
        show(view)
        return view
    }

    private func makeLiveView() -> MascotAnimationView {
        let view = MascotAnimationView()
        show(view)
        return view
    }

    /// `MascotAnimationView` only plays while it has a window.
    private func show(_ view: MascotAnimationView) {
        let window = window ?? UIWindow(frame: CGRect(x: 0, y: 0, width: 200, height: 200))
        self.window = window
        window.addSubview(view)
    }

    /// Runs `count` ticks of the ticker's clock and reports the frame each one left on screen.
    @discardableResult
    private func tick(_ view: MascotAnimationView, _ count: Int, of frames: [UIImage]) -> [Int?] {
        (0 ..< count).map { _ in
            clock.now += Self.tickInterval
            // A paused link delivers no tick, so neither does this.
            if !ticker.isPaused { view.step(now: clock.now) }
            return frames.firstIndex { $0 === view.image }
        }
    }

    private func makeFrames(_ count: Int) -> [UIImage] {
        let renderer = UIGraphicsImageRenderer(size: CGSize(width: 2, height: 2))
        return (0 ..< count).map { index in
            renderer.image { context in
                UIColor(white: CGFloat(index) / CGFloat(count), alpha: 1).setFill()
                context.fill(CGRect(x: 0, y: 0, width: 2, height: 2))
            }
        }
    }

    private func animation(_ frames: [UIImage], isComplete: Bool) -> MascotAnimation {
        MascotAnimation(
            frames: frames,
            delays: Array(repeating: Self.frameDelay, count: frames.count),
            isComplete: isComplete
        )
    }
}

/// Held by reference so the closure the view keeps does not have to capture the test case.
private final class TestClock {
    var now: TimeInterval = 0
}

private final class FakeTicker: MascotTicker {
    var isPaused = false
    private(set) var isInvalidated = false

    func invalidate() {
        isInvalidated = true
    }
}
