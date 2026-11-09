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
        maxX > 0 ? min(offset / maxX, 1) : 0
    }

    /// Where the thumb is drawn on a track `maxX` long: held on it when a fold has shrunk the track under a thumb
    /// parked at its old end.
    public func drawnOffset(maxX: CGFloat) -> CGFloat {
        min(offset, max(maxX, 0))
    }
}

/// A deliberate slide-to-confirm control: the user drags the thumb to the
/// trailing edge to fire `onConfirm` — a mis-tap must not commit (the
/// `SwipeToConfirmButton.kt` parity). While `isBusy` the thumb locks at the
/// end with a spinner and snaps back when the busy state ends; the parent can
/// also bump `resetTrigger` to snap the thumb back after a failed submit so
/// the user can retry. VoiceOver cannot drag, so it reads the open slide as one
/// button whose activation confirms.
///
/// `thumbLanes` keeps the words clear of the thumb at both ends and `maxLabelLines` lets them wrap there; the track
/// grows from its height to hold them. Both are off by default, so a one-line label keeps the fixed track and the
/// caller's own line limit and scale factor still govern it.
public struct SlideToConfirm: View {
    let idleLabel: String
    let busyLabel: String
    let isBusy: Bool
    let enabled: Bool
    let resetTrigger: Int
    let maxLabelLines: Int
    let thumbLanes: Bool
    let onConfirm: () -> Void
    private var detail: String?
    private var isLocked = false

    @State private var thumb = SlideToConfirmThumb()
    @State private var trackWidth: CGFloat = 0

    private let thumbInset: CGFloat = 4

    public init(
        idleLabel: String,
        busyLabel: String,
        isBusy: Bool,
        enabled: Bool = true,
        resetTrigger: Int = 0,
        maxLabelLines: Int = 1,
        thumbLanes: Bool = false,
        detail: String? = nil,
        onConfirm: @escaping () -> Void
    ) {
        self.idleLabel = idleLabel
        self.busyLabel = busyLabel
        self.isBusy = isBusy
        self.enabled = enabled
        self.resetTrigger = resetTrigger
        self.maxLabelLines = maxLabelLines
        self.thumbLanes = thumbLanes
        self.detail = detail
        self.onConfirm = onConfirm
    }

    /// The slide that cannot be used yet: a lock where the thumb would be, why in its label, and when (or what
    /// else) under it. Nothing drags; VoiceOver reads a disabled button named by the reason and the detail.
    public init(lockedReason reason: String, detail: String? = nil) {
        self.init(
            idleLabel: reason,
            busyLabel: reason,
            isBusy: false,
            enabled: false,
            detail: detail,
            onConfirm: {}
        )
        isLocked = true
    }

    public static func lockedLabel(reason: String, detail: String?) -> String {
        guard let detail, !detail.trimmingCharacters(in: .whitespaces).isEmpty else { return reason }
        return "\(reason), \(detail)"
    }

    private var trackHeight: CGFloat {
        56
    }

    private var thumbSize: CGFloat {
        trackHeight - thumbInset * 2
    }

    /// The thumb, its inset and a gap: what a laned label keeps clear at each end, so it stays centred on the track.
    private var laneWidth: CGFloat {
        thumbSize + thumbInset + Spacing.xs
    }

    public var body: some View {
        if isLocked {
            lockedTrack()
        } else {
            openTrack
                .onChange(of: isBusy) { busy in
                    if !busy { withAnimation(.spring()) { thumb.reset() } }
                }
                .onChange(of: resetTrigger) { _ in
                    withAnimation(.spring()) { thumb.reset() }
                }
                .accessibilityElement(children: .ignore)
                .accessibilityLabel(Self.lockedLabel(reason: isBusy ? busyLabel : idleLabel, detail: detail))
                .accessibilityAddTraits(.isButton)
                .accessibilityAction {
                    if enabled, !isBusy { onConfirm() }
                }
                .disabled(!enabled || isBusy)
        }
    }

    @ViewBuilder
    private var openTrack: some View {
        if thumbLanes || maxLabelLines > 1 {
            lanedTrack
        } else {
            slidingTrack
        }
    }

    private func lockedTrack() -> some View {
        lanedShell(fill: CleansiaColors.surfaceVariant, lanes: true) {
            Text(idleLabel)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurface)
            if let detail {
                Text(detail)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        } knob: {
            Image(systemName: "lock")
                .font(.system(size: thumbSize * 0.4, weight: .semibold))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .frame(width: thumbSize, height: thumbSize)
                .background(CleansiaColors.surface, in: Circle())
                .overlay(Circle().strokeBorder(CleansiaColors.outline, lineWidth: 1.5))
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Self.lockedLabel(reason: idleLabel, detail: detail))
        .accessibilityAddTraits(.isButton)
        .disabled(true)
    }

    private var lanedTrack: some View {
        let maxX = max(trackWidth - thumbSize - thumbInset * 2, 0)
        return lanedShell(
            fill: trackColor,
            lanes: thumbLanes,
            wordsOpacity: isBusy ? 1 : 1 - Double(thumb.progress(maxX: maxX))
        ) {
            Text(isBusy ? busyLabel : idleLabel)
                .font(CleansiaTypography.labelLarge)
                .lineLimit(maxLabelLines)
            if let detail {
                Text(detail)
                    .font(CleansiaTypography.labelMedium)
            }
        } knob: {
            thumbView(size: thumbSize, maxX: maxX)
        }
        .foregroundColor(labelColor)
        .background(GeometryReader { geometry in
            Color.clear
                .onAppear { trackWidth = geometry.size.width }
                .onChange(of: geometry.size.width) { trackWidth = $0 }
        })
        .opacity(enabled || isBusy ? 1 : 0.6)
    }

    /// The words centred on the track, between the lanes when it has them, over a track at least its height tall.
    private func lanedShell(
        fill: Color,
        lanes: Bool,
        wordsOpacity: Double = 1,
        @ViewBuilder words: () -> some View,
        @ViewBuilder knob: () -> some View
    ) -> some View {
        VStack(spacing: 0) { words() }
            .multilineTextAlignment(.center)
            .fixedSize(horizontal: false, vertical: true)
            .opacity(wordsOpacity)
            .padding(.horizontal, lanes ? laneWidth : 0)
            .padding(.vertical, Spacing.xs)
            .frame(maxWidth: .infinity, minHeight: trackHeight)
            .background(fill, in: Capsule())
            .overlay(alignment: .leading) { knob().padding(thumbInset) }
    }

    private var slidingTrack: some View {
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
        }
        .frame(height: trackHeight)
        .opacity(enabled || isBusy ? 1 : 0.6)
    }

    private func thumbView(size: CGFloat, maxX: CGFloat) -> some View {
        Circle()
            .fill(thumbColor)
            .frame(width: size, height: size)
            .overlay(thumbIcon(size: size))
            .offset(x: thumb.drawnOffset(maxX: maxX))
            .gesture(dragGesture(maxX: maxX))
    }

    @ViewBuilder
    private func thumbIcon(size: CGFloat) -> some View {
        if isBusy {
            ProgressView()
                .tint(thumbGlyphColor)
        } else {
            Image(systemName: "chevron.right")
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
                guard fired else { return }
                // ADR-0018 D2: the commit is felt as well as seen (Android `SwipeToConfirmButton` parity).
                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                onConfirm()
            }
    }

    private var trackColor: Color {
        enabled || isBusy ? CleansiaColors.primary : CleansiaColors.surfaceVariant
    }

    private var labelColor: Color {
        enabled || isBusy ? CleansiaColors.onPrimary : CleansiaColors.onSurfaceVariant
    }

    private var thumbColor: Color {
        CleansiaColors.surface
    }

    private var thumbGlyphColor: Color {
        enabled || isBusy ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant
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
                    onConfirm: {}
                )
                SlideToConfirm(
                    idleLabel: "Slide to confirm",
                    busyLabel: "Booking…",
                    isBusy: false,
                    enabled: false,
                    onConfirm: {}
                )
                SlideToConfirm(
                    idleLabel: "Swipe to accept the contract for work",
                    busyLabel: "Accepting…",
                    isBusy: false,
                    maxLabelLines: 2,
                    thumbLanes: true,
                    onConfirm: {}
                )
                SlideToConfirm(lockedReason: "You can set off from 13:00", detail: "in 3 h 40 min")
            }
            .padding()
        }
    }
#endif
