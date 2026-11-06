import CleansiaCore
import SwiftUI

/// Full-screen busy overlay with the cleaning mascot, mirroring Android's
/// `BusyMascotOverlay`. The scrim swallows touches so the user can't interact
/// with the underlying screen mid-flight; the card springs in from 85% scale.
struct BusyMascotOverlay: View {
    let visible: Bool
    let message: String
    /// Nil keeps the spring scale-out; otherwise the card fades out with this animation, and so does the scrim
    /// unless `scrimExit` gives it its own.
    var exit: Animation?
    var scrimExit: Animation?
    /// Nil keeps the 140 pt box; otherwise the box draws the cleaner this tall.
    var mascotFigureHeight: CGFloat?
    var mascotHidden = false
    var onMascotFrame: ((CGRect) -> Void)?
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    static let mascotResize = 0.25
    private static let entrance = Animation.spring(response: 0.4, dampingFraction: 0.7)
    private static let plainMascotSide: CGFloat = 140

    private var mascotSide: CGFloat {
        mascotFigureHeight.map(MascotFigureBox.cleaning.canvasSide(figureHeight:)) ?? Self.plainMascotSide
    }

    var body: some View {
        ZStack {
            if visible {
                Color.black.opacity(0.45)
                    .ignoresSafeArea()
                    .transition(scrimExit
                        .map { .asymmetric(insertion: .opacity, removal: .opacity.animation($0)) } ?? .opacity)
                VStack(spacing: Spacing.xs) {
                    AnimatedMascotView(.cleaningInProgress, fallback: .cleaning)
                        .modifier(MascotBox(side: mascotSide))
                        .opacity(mascotHidden ? 0 : 1)
                        .onGeometryChange(for: CGRect.self, of: { $0.frame(in: .global) }, action: { frame in
                            onMascotFrame?(frame)
                        })
                    Text(message)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .multilineTextAlignment(.center)
                }
                .padding(.horizontal, Spacing.l)
                .padding(.vertical, 28)
                .frame(maxWidth: 360)
                .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
                // Flatten before shadowing (the SnapSheet precedent): `.shadow` falls on every opaque
                // thing in the subtree, so without this the mascot and the message each cast their own
                // shadow onto the card. Composited first, only the card's edge does — Android's
                // elevation shadow, which the mascot never had there.
                .compositingGroup()
                .shadow(radius: 16)
                .animation(reduceMotion ? nil : .easeInOut(duration: Self.mascotResize), value: mascotSide)
                .padding(.horizontal, Spacing.xl)
                .transition(exit == nil ? cardEntrance : .asymmetric(insertion: cardEntrance, removal: .opacity))
            }
        }
        .animation(visible ? Self.entrance : exit ?? Self.entrance, value: visible)
    }

    private var cardEntrance: AnyTransition {
        .scale(scale: 0.85).combined(with: .opacity)
    }
}

/// The mascot is a UIKit view, whose frame SwiftUI does not animate, so a change of size is laid out frame by frame.
private struct MascotBox: ViewModifier, Animatable {
    var side: CGFloat

    var animatableData: CGFloat {
        get { side }
        set { side = newValue }
    }

    func body(content: Content) -> some View {
        content.frame(width: side, height: side)
    }
}

/// Where each mascot's figure stands on its 360 px canvas. The cleaning box is the rows the still's alpha covers, the
/// loop's first frame, which every other frame's height is within 2 % of; the welcoming box is the one the hero hangs
/// its canvas from (`mascotCanvas`), which sits 2 px above the art's own alpha, rows 53-301.
enum MascotFigureBox {
    case cleaning
    case welcoming

    private static let canvas: CGFloat = 360

    private var top: CGFloat {
        self == .cleaning ? 43 : 51
    }

    private var height: CGFloat {
        self == .cleaning ? 272 : 249
    }

    func canvasSide(figureHeight: CGFloat) -> CGFloat {
        figureHeight * Self.canvas / height
    }

    func figureHeight(canvasSide: CGFloat) -> CGFloat {
        canvasSide * height / Self.canvas
    }

    /// The canvas that stands this figure where `other`'s stands on `canvas`: as tall, its top on the same line,
    /// centred on the same vertical.
    func canvas(standingAs other: MascotFigureBox, on canvas: CGRect) -> CGRect {
        let side = canvasSide(figureHeight: other.figureHeight(canvasSide: canvas.height))
        let line = canvas.minY + canvas.height * other.top / Self.canvas
        return CGRect(x: canvas.midX - side / 2, y: line - side * top / Self.canvas, width: side, height: side)
    }
}

#if DEBUG
    struct BusyMascotOverlay_Previews: PreviewProvider {
        static var previews: some View {
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                BusyMascotOverlay(visible: true, message: "Activating Cleansia Plus…")
            }
        }
    }
#endif
