import SwiftUI

public enum MascotArt: Equatable {
    case animated(AnimatedMascot, loop: Bool, fallback: Mascot)
    case still(Mascot)
}

/// The mascot that rides a `SnapSheet`'s top edge, half over the backdrop and half over the sheet.
/// Which art belongs to which status is the app's decision — the two apps map the same order
/// statuses differently — so this only renders one.
///
/// Decorative: VoiceOver reads the order's status from the sheet, and a second announcement of the
/// same thing in a different vocabulary is noise.
public struct MascotPuck: View {
    private let art: MascotArt?

    public init(_ art: MascotArt?) {
        self.art = art
    }

    public var body: some View {
        Group {
            switch art {
            case let .animated(mascot, loop, fallback):
                AnimatedMascotView(mascot, loop: loop, fallback: fallback)
            case let .still(mascot):
                mascot.image
                    .resizable()
                    .scaledToFit()
            case nil:
                EmptyView()
            }
        }
        .scaleEffect(artScale)
        .accessibilityHidden(true)
    }

    private var artScale: CGFloat {
        switch art {
        case .still(.leaning), .still(.ready): 0.903
        case .still(.waving): 1.009
        case .still(.sprayAndCloth): 1.119
        case .still(.thumbsUp): 0.979
        case .animated(.welcoming, _, _): 1.147
        case .animated(.cleaningInProgress, _, _): 1.048
        default: 1
        }
    }
}

#if DEBUG
    struct MascotPuck_Previews: PreviewProvider {
        static var previews: some View {
            HStack(spacing: Spacing.m) {
                MascotPuck(.still(.sprayAndCloth))
                MascotPuck(.still(.thumbsUp))
                MascotPuck(.animated(.cleaningInProgress, loop: true, fallback: .cleaning))
            }
            .frame(height: 128)
            .padding()
            .previewLayout(.sizeThatFits)
        }
    }
#endif
