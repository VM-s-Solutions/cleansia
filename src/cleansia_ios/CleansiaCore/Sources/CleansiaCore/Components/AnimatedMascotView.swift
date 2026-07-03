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
}

/// Plays an animated WebP mascot bundled as an asset-catalog data asset,
/// mirroring Android's Coil-backed `MascotAnimation`. With `loop: false` the
/// animation plays once and freezes on the final frame. Falls back to the
/// static mascot image when the data asset is missing or cannot be animated.
public struct AnimatedMascotView: View {
    private let data: Data?
    private let loop: Bool
    private let fallback: Mascot

    public init(_ mascot: AnimatedMascot, loop: Bool = true, fallback: Mascot, bundle: Bundle = .main) {
        data = NSDataAsset(name: mascot.rawValue, bundle: bundle)?.data
        self.loop = loop
        self.fallback = fallback
    }

    public var body: some View {
        if let data {
            AnimatedImageView(data: data, loop: loop, fallback: fallback)
        } else {
            fallback.image
                .resizable()
                .scaledToFit()
        }
    }
}

private struct AnimatedImageView: UIViewRepresentable {
    let data: Data
    let loop: Bool
    let fallback: Mascot

    func makeUIView(context _: Context) -> UIImageView {
        let view = UIImageView()
        view.contentMode = .scaleAspectFit
        view.setContentHuggingPriority(.defaultLow, for: .horizontal)
        view.setContentHuggingPriority(.defaultLow, for: .vertical)
        view.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        view.setContentCompressionResistancePriority(.defaultLow, for: .vertical)
        startAnimation(on: view)
        return view
    }

    func updateUIView(_: UIImageView, context _: Context) {}

    private func startAnimation(on view: UIImageView) {
        let source = CGImageSourceCreateWithData(data as CFData, nil)
        let frameCount = source.map(CGImageSourceGetCount) ?? 0
        let loop = loop
        let status = CGAnimateImageDataWithBlock(data as CFData, nil) { [weak view] index, cgImage, stop in
            guard let view else {
                stop.pointee = true
                return
            }
            view.image = UIImage(cgImage: cgImage)
            if AnimatedMascotPlayback.shouldStop(loop: loop, frameIndex: index, frameCount: frameCount) {
                stop.pointee = true
            }
        }
        if status != noErr {
            view.image = staticFrame(from: source) ?? UIImage(named: fallback.rawValue)
        }
    }

    private func staticFrame(from source: CGImageSource?) -> UIImage? {
        guard let source, CGImageSourceGetCount(source) > 0,
              let cgImage = CGImageSourceCreateImageAtIndex(source, 0, nil)
        else { return nil }
        return UIImage(cgImage: cgImage)
    }
}
