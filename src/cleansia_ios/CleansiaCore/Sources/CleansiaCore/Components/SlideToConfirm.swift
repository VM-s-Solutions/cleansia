import SwiftUI

/// The drag state behind `SlideToConfirm`, kept pure so the threshold/reset
/// contract is unit-testable: clamp to `[0, maxX]`, fire at ≥ 90% of the
/// track, spring back below it, lock after firing until `reset()`.
public struct SlideToConfirmThumb: Equatable {
    public static let fireFraction: CGFloat = 0.9

    public private(set) var offset: CGFloat = 0
    public private(set) var hasFired = false

    public init() {}

    public mutating func drag(translation: CGFloat, maxX: CGFloat) {
        guard !hasFired else { return }
        offset = min(max(translation, 0), max(maxX, 0))
    }

    @discardableResult
    public mutating func endDrag(maxX: CGFloat) -> Bool {
        guard !hasFired else { return false }
        if maxX > 0, offset >= maxX * Self.fireFraction {
            offset = maxX
            hasFired = true
            return true
        }
        offset = 0
        return false
    }

    public mutating func reset() {
        offset = 0
        hasFired = false
    }

    public func progress(maxX: CGFloat) -> CGFloat {
        maxX > 0 ? offset / maxX : 0
    }
}

/// A deliberate slide-to-confirm control: the user drags the thumb to the
/// trailing edge to fire `onConfirm` — a mis-tap must not commit (the
/// `SwipeToConfirmButton.kt` parity). While `isBusy` the thumb locks at the
/// end with a spinner and snaps back when the busy state ends; the parent can
/// also bump `resetTrigger` to snap the thumb back after a failed submit so
/// the user can retry.
public struct SlideToConfirm: View {
    public enum Style {
        case subtle
        case prominent
    }

    let idleLabel: String
    let busyLabel: String
    let isBusy: Bool
    let enabled: Bool
    let resetTrigger: Int
    let style: Style
    let onConfirm: () -> Void

    @State private var thumb = SlideToConfirmThumb()

    private let thumbInset: CGFloat = 4

    public init(
        idleLabel: String,
        busyLabel: String,
        isBusy: Bool,
        enabled: Bool = true,
        resetTrigger: Int = 0,
        style: Style = .subtle,
        onConfirm: @escaping () -> Void
    ) {
        self.idleLabel = idleLabel
        self.busyLabel = busyLabel
        self.isBusy = isBusy
        self.enabled = enabled
        self.resetTrigger = resetTrigger
        self.style = style
        self.onConfirm = onConfirm
    }

    private var trackHeight: CGFloat {
        style == .prominent ? 56 : 52
    }

    public var body: some View {
        GeometryReader { geometry in
            let thumbSize = trackHeight - thumbInset * 2
            let maxX = max(geometry.size.width - thumbSize - thumbInset * 2, 0)

            ZStack(alignment: .leading) {
                Capsule().fill(trackColor)

                Text(isBusy ? busyLabel : idleLabel)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(labelColor)
                    .frame(maxWidth: .infinity)
                    .opacity(isBusy ? 1 : 1 - Double(thumb.progress(maxX: maxX)))

                thumbView(size: thumbSize, maxX: maxX)
                    .padding(thumbInset)
            }
            .frame(height: trackHeight)
            .onChange(of: isBusy) { busy in
                if !busy { withAnimation(.spring()) { thumb.reset() } }
            }
            .onChange(of: resetTrigger) { _ in
                withAnimation(.spring()) { thumb.reset() }
            }
        }
        .frame(height: trackHeight)
        .opacity(enabled || isBusy ? 1 : 0.6)
    }

    private func thumbView(size: CGFloat, maxX: CGFloat) -> some View {
        Circle()
            .fill(thumbColor)
            .frame(width: size, height: size)
            .overlay(thumbIcon(size: size))
            .offset(x: thumb.offset)
            .gesture(dragGesture(maxX: maxX))
    }

    @ViewBuilder
    private func thumbIcon(size: CGFloat) -> some View {
        if isBusy {
            ProgressView()
                .tint(thumbGlyphColor)
        } else {
            Image(systemName: style == .prominent ? "chevron.right" : "chevron.right.2")
                .font(.system(size: size * 0.4, weight: .bold))
                .foregroundColor(thumbGlyphColor)
        }
    }

    private func dragGesture(maxX: CGFloat) -> some Gesture {
        DragGesture()
            .onChanged { value in
                guard enabled, !isBusy else { return }
                thumb.drag(translation: value.translation.width, maxX: maxX)
            }
            .onEnded { _ in
                guard enabled, !isBusy else { return }
                var fired = false
                withAnimation(.spring()) { fired = thumb.endDrag(maxX: maxX) }
                if fired { onConfirm() }
            }
    }

    private var trackColor: Color {
        switch style {
        case .subtle:
            CleansiaColors.primary.opacity(0.12)
        case .prominent:
            enabled || isBusy ? CleansiaColors.primary : CleansiaColors.surfaceVariant
        }
    }

    private var labelColor: Color {
        switch style {
        case .subtle:
            CleansiaColors.primary
        case .prominent:
            enabled || isBusy ? CleansiaColors.onPrimary : CleansiaColors.onSurfaceVariant
        }
    }

    private var thumbColor: Color {
        switch style {
        case .subtle:
            CleansiaColors.primary
        case .prominent:
            CleansiaColors.surface
        }
    }

    private var thumbGlyphColor: Color {
        switch style {
        case .subtle:
            CleansiaColors.onPrimary
        case .prominent:
            enabled || isBusy ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant
        }
    }
}

#if DEBUG
    struct SlideToConfirm_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: 16) {
                SlideToConfirm(idleLabel: "Slide to take order", busyLabel: "Taking…", isBusy: false, onConfirm: {})
                SlideToConfirm(idleLabel: "Slide to complete", busyLabel: "Completing…", isBusy: true, onConfirm: {})
                SlideToConfirm(
                    idleLabel: "Slide to confirm · 1 200 Kč",
                    busyLabel: "Booking…",
                    isBusy: false,
                    style: .prominent,
                    onConfirm: {}
                )
                SlideToConfirm(
                    idleLabel: "Slide to confirm",
                    busyLabel: "Booking…",
                    isBusy: false,
                    enabled: false,
                    style: .prominent,
                    onConfirm: {}
                )
            }
            .padding()
        }
    }
#endif
