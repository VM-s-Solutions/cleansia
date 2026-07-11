import ImageIO
import SwiftUI
import UIKit

public enum AnimatedMascot: String {
    case cleaningInProgress = "mascot_cleaning_in_progress"
    case welcoming = "mascot_welcoming"
}

enum AnimatedMascotPlayback {
    static func shouldStop(loop: Bool, frameIndex: Int, frameCount: Int) -> Bool {
        !loop && frameCount > 0 && frameIndex >= frameCount - 1
    }

    static func shouldRestart(activeData: Data?, activeLoop: Bool?, data: Data, loop: Bool) -> Bool {
        activeData != data || activeLoop != loop
    }

    static func isSuperseded(generation: Int, activeGeneration: Int) -> Bool {
        generation != activeGeneration
    }

    /// Whether a layout/update pass should re-assert the pinned final frame.
    /// Only for a completed one-shot that is still the current run: a looping
    /// run has no final frame, an un-finished run hasn't captured one yet, and
    /// a superseded run must yield the view to its successor.
    static func shouldPinFinalFrameOnUpdate(loop: Bool, hasCompletedFrame: Bool, superseded: Bool) -> Bool {
        !loop && hasCompletedFrame && !superseded
    }
}

/// Plays an animated WebP mascot bundled as an asset-catalog data asset,
/// mirroring Android's Coil-backed `MascotAnimation`. With `loop: false` the
/// animation plays once and freezes on the final frame. Falls back to the
/// static mascot image when the data asset is missing or cannot be animated.
public struct AnimatedMascotView: View {
    private let data: Data?
    private let loop: Bool
    private let fallback: Mascot
    private let bundle: Bundle

    public init(_ mascot: AnimatedMascot, loop: Bool = true, fallback: Mascot, bundle: Bundle = .main) {
        data = NSDataAsset(name: mascot.rawValue, bundle: bundle)?.data
        self.loop = loop
        self.fallback = fallback
        self.bundle = bundle
    }

    public var body: some View {
        if let data {
            AnimatedImageView(data: data, loop: loop, fallback: fallback, bundle: bundle)
        } else {
            Image(fallback.rawValue, bundle: bundle)
                .resizable()
                .scaledToFit()
        }
    }
}

private struct AnimatedImageView: UIViewRepresentable {
    let data: Data
    let loop: Bool
    let fallback: Mascot
    let bundle: Bundle

    func makeCoordinator() -> Coordinator {
        Coordinator()
    }

    func makeUIView(context: Context) -> UIImageView {
        let view = UIImageView()
        view.contentMode = .scaleAspectFit
        view.setContentHuggingPriority(.defaultLow, for: .horizontal)
        view.setContentHuggingPriority(.defaultLow, for: .vertical)
        view.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        view.setContentCompressionResistancePriority(.defaultLow, for: .vertical)
        context.coordinator.animateIfNeeded(self, on: view)
        return view
    }

    func updateUIView(_ view: UIImageView, context: Context) {
        context.coordinator.animateIfNeeded(self, on: view)
    }

    final class Coordinator {
        private var activeData: Data?
        private var activeLoop: Bool?
        private var activeGeneration = 0
        private var pinnedFinalFrame: UIImage?
        private var completedGeneration: Int?

        func animateIfNeeded(_ representable: AnimatedImageView, on view: UIImageView) {
            guard AnimatedMascotPlayback.shouldRestart(
                activeData: activeData,
                activeLoop: activeLoop,
                data: representable.data,
                loop: representable.loop
            ) else {
                reassertPinnedFinalFrame(loop: representable.loop, on: view)
                return
            }
            activeData = representable.data
            activeLoop = representable.loop
            activeGeneration += 1
            pinnedFinalFrame = nil
            completedGeneration = nil
            start(representable, on: view, generation: activeGeneration)
        }

        private func start(_ representable: AnimatedImageView, on view: UIImageView, generation: Int) {
            let data = representable.data
            let loop = representable.loop
            let source = CGImageSourceCreateWithData(data as CFData, nil)
            let frameCount = source.map(CGImageSourceGetCount) ?? 0
            // A superseded run must stop itself via the stop flag: CGAnimateImageData
            // has no cancel handle, so the generation token is the only way to kill
            // the old animation when SwiftUI reuses the UIImageView for a new mascot.
            let frameHandler: CGImageSourceAnimationBlock = { [weak view, weak self] index, cgImage, stop in
                guard let view, let self, !AnimatedMascotPlayback.isSuperseded(
                    generation: generation, activeGeneration: activeGeneration
                ) else {
                    stop.pointee = true
                    return
                }
                view.image = UIImage(cgImage: cgImage)
                // CGAnimateImageDataWithBlock ignores the WebP's baked-in loop count and
                // repeats forever, so the one-shot must stop itself on the final frame.
                if AnimatedMascotPlayback.shouldStop(loop: loop, frameIndex: index, frameCount: frameCount) {
                    stop.pointee = true
                    completeOneShot(
                        source: source, frameIndex: index, delivered: cgImage,
                        on: view, generation: generation
                    )
                }
            }
            let status = CGAnimateImageDataWithBlock(data as CFData, nil, frameHandler)
            if status != noErr {
                let fallback = staticFrame(from: source)
                    ?? UIImage(named: representable.fallback.rawValue, in: representable.bundle, with: nil)
                view.image = fallback
                if !loop, let fallback { pin(fallback, generation: generation, on: view) }
            }
        }

        /// Freezes the ending pose so it survives SwiftUI relayout and view reuse.
        /// The block's own last frame is transient — a later `updateUIView`, or a
        /// fresh `UIImageView` SwiftUI hands us after the one-shot ends, leaves the
        /// view imageless. Pinning the decoded final frame and re-asserting it on
        /// every update keeps the mascot on screen indefinitely.
        private func completeOneShot(
            source: CGImageSource?,
            frameIndex: Int,
            delivered: CGImage,
            on view: UIImageView,
            generation: Int
        ) {
            let finalFrame = source
                .flatMap { CGImageSourceCreateImageAtIndex($0, max(frameIndex, 0), nil) }
                .map(UIImage.init(cgImage:)) ?? UIImage(cgImage: delivered)
            pin(finalFrame, generation: generation, on: view)
            DispatchQueue.main.async { [weak self, weak view] in
                guard let self, let view else { return }
                reassertPinnedFinalFrame(loop: false, on: view)
            }
        }

        private func pin(_ frame: UIImage, generation: Int, on view: UIImageView) {
            pinnedFinalFrame = frame
            completedGeneration = generation
            view.image = frame
        }

        private func reassertPinnedFinalFrame(loop: Bool, on view: UIImageView) {
            let superseded = completedGeneration != activeGeneration
            guard AnimatedMascotPlayback.shouldPinFinalFrameOnUpdate(
                loop: loop, hasCompletedFrame: pinnedFinalFrame != nil, superseded: superseded
            ), let frame = pinnedFinalFrame, view.image !== frame else { return }
            view.image = frame
        }

        private func staticFrame(from source: CGImageSource?) -> UIImage? {
            guard let source, CGImageSourceGetCount(source) > 0,
                  let cgImage = CGImageSourceCreateImageAtIndex(source, 0, nil)
            else { return nil }
            return UIImage(cgImage: cgImage)
        }
    }
}
