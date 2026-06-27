import CleansiaCore
import SwiftUI

/// A deliberate slide-to-confirm control: the cleaner drags the thumb to the
/// trailing edge to fire `onConfirm`. Advancing an active job changes what the
/// customer sees, so a mis-tap is a real cost — the slide gesture is the point
/// (the native replacement for Android's custom `SlideToCommit`). While `isBusy`
/// the thumb locks at the end with a spinner.
struct SlideToConfirm: View {
    let idleLabel: String
    let busyLabel: String
    let isBusy: Bool
    let onConfirm: () -> Void

    @State private var dragX: CGFloat = 0

    private let trackHeight: CGFloat = 52
    private let thumbInset: CGFloat = 4

    var body: some View {
        GeometryReader { geometry in
            let thumbSize = trackHeight - thumbInset * 2
            let maxX = max(geometry.size.width - thumbSize - thumbInset * 2, 0)
            let progress = maxX > 0 ? dragX / maxX : 0

            ZStack(alignment: .leading) {
                Capsule().fill(CleansiaColors.primary.opacity(0.12))

                Text(isBusy ? busyLabel : idleLabel)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                    .frame(maxWidth: .infinity)
                    .opacity(isBusy ? 1 : 1 - Double(progress))

                thumb(size: thumbSize, maxX: maxX)
                    .padding(thumbInset)
            }
            .frame(height: trackHeight)
            .onChange(of: isBusy) { busy in
                if !busy { withAnimation(.spring()) { dragX = 0 } }
            }
        }
        .frame(height: trackHeight)
    }

    private func thumb(size: CGFloat, maxX: CGFloat) -> some View {
        Circle()
            .fill(CleansiaColors.primary)
            .frame(width: size, height: size)
            .overlay(thumbIcon(size: size))
            .offset(x: dragX)
            .gesture(dragGesture(maxX: maxX))
    }

    @ViewBuilder
    private func thumbIcon(size: CGFloat) -> some View {
        if isBusy {
            ProgressView()
                .tint(CleansiaColors.onPrimary)
        } else {
            Image(systemName: "chevron.right.2")
                .font(.system(size: size * 0.4, weight: .bold))
                .foregroundColor(CleansiaColors.onPrimary)
        }
    }

    private func dragGesture(maxX: CGFloat) -> some Gesture {
        DragGesture()
            .onChanged { value in
                guard !isBusy else { return }
                dragX = min(max(value.translation.width, 0), maxX)
            }
            .onEnded { _ in
                guard !isBusy else { return }
                if dragX >= maxX * 0.9 {
                    dragX = maxX
                    onConfirm()
                } else {
                    withAnimation(.spring()) { dragX = 0 }
                }
            }
    }
}

#if DEBUG
    struct SlideToConfirm_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.m) {
                SlideToConfirm(idleLabel: "Slide to take order", busyLabel: "Taking…", isBusy: false, onConfirm: {})
                SlideToConfirm(idleLabel: "Slide to complete", busyLabel: "Completing…", isBusy: true, onConfirm: {})
            }
            .padding()
        }
    }
#endif
