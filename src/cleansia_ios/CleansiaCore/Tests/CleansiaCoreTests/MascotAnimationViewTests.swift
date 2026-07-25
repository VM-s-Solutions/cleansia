import UIKit
import XCTest
@testable import CleansiaCore

/// Drives the real `CADisplayLink` playback with a synthetic frame buffer. The pure playhead is
/// covered by `AnimatedMascotPlaybackTests`; this covers the wiring around it — that playback starts
/// on a PARTIAL buffer, keeps going when the buffer grows, wraps, freezes a one-shot, stops
/// off-window, and does not outlive the view.
final class MascotAnimationViewTests: XCTestCase {
    private var window: UIWindow?

    override func tearDown() {
        window = nil
        super.tearDown()
    }

    func testAPartialBufferPlaysAndThenHoldsItsNewestFrame() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: false))

        let seen = record(view, for: 0.3)

        XCTAssertTrue(view.image === images[2], "playback runs over the decoded prefix")
        XCTAssertFalse(seen.contains { $0 === images[0] }, "a partial buffer must hold, not wrap")
    }

    func testGrowingTheBufferContinuesInsteadOfRestarting() {
        let images = makeFrames(6)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(Array(images.prefix(3)), isComplete: false))
        _ = record(view, for: 0.3)
        XCTAssertTrue(view.image === images[2])

        view.update(animation(images, isComplete: true))
        let seen = record(view, for: 0.25)

        XCTAssertTrue(seen.first === images[3], "the appended frames continue the loop with no jump back to 0")
    }

    func testACompleteLoopWraps() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))

        let seen = record(view, for: 0.5)

        XCTAssertTrue(seen.contains { $0 === images[0] })
        XCTAssertGreaterThan(seen.count, 3, "a looping mascot keeps stepping past its final frame")
    }

    func testAOneShotFreezesOnItsFinalFrame() {
        let images = makeFrames(3)
        let view = makeView()
        view.prepare(poster: nil, loop: false)
        view.update(animation(images, isComplete: true))

        let seen = record(view, for: 0.5)

        XCTAssertTrue(view.image === images[2])
        XCTAssertFalse(seen.contains { $0 === images[0] }, "a one-shot never wraps")
        XCTAssertTrue(record(view, for: 0.2).isEmpty, "a finished one-shot never resumes")
    }

    func testPlaybackStopsWhileOffWindow() {
        let images = makeFrames(6)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))
        _ = record(view, for: 0.12)

        view.removeFromSuperview()

        XCTAssertTrue(record(view, for: 0.3).isEmpty, "an off-window mascot must not burn display-link ticks")
    }

    func testPlaybackResumesWhenTheViewReturnsToAWindow() {
        let images = makeFrames(6)
        let view = makeView()
        view.prepare(poster: nil, loop: true)
        view.update(animation(images, isComplete: true))
        _ = record(view, for: 0.12)
        view.removeFromSuperview()

        window?.addSubview(view)

        XCTAssertFalse(record(view, for: 0.2).isEmpty)
    }

    func testTheDisplayLinkDoesNotKeepTheViewAlive() {
        weak var released: MascotAnimationView?
        autoreleasepool {
            let view = makeView()
            released = view
            view.prepare(poster: nil, loop: true)
            view.update(animation(makeFrames(4), isComplete: true))
            _ = record(view, for: 0.1)
            view.removeFromSuperview()
        }

        XCTAssertNil(released, "a running display link must not retain the mascot view")
    }

    // MARK: helpers

    private func makeView() -> MascotAnimationView {
        let window = window ?? UIWindow(frame: CGRect(x: 0, y: 0, width: 200, height: 200))
        self.window = window
        let view = MascotAnimationView()
        window.addSubview(view)
        return view
    }

    /// The frames the view actually painted, in order, sampled well under the 50 ms frame delay.
    private func record(_ view: MascotAnimationView, for seconds: TimeInterval) -> [UIImage] {
        var seen: [UIImage] = []
        var last = view.image
        let deadline = Date().addingTimeInterval(seconds)
        while Date() < deadline {
            RunLoop.current.run(until: Date().addingTimeInterval(0.01))
            if let image = view.image, image !== last {
                seen.append(image)
                last = image
            }
        }
        return seen
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
        MascotAnimation(frames: frames, delays: Array(repeating: 0.05, count: frames.count), isComplete: isComplete)
    }
}
