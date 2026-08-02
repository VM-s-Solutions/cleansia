import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

enum OrderMascotArt: Equatable {
    case animated(AnimatedMascot, loop: Bool, fallback: Mascot)
    case still(Mascot)
}

/// Which mascot rides the sheet edge, per order status (Android's `FloatingMascot`
/// `mascotForStatus`). Hoisted out of the view because the animated-vs-still split is
/// the whole point of the affordance and nothing else in the app can see it change.
enum OrderDetailMascotArt {
    static func art(for status: OrderStatus?) -> OrderMascotArt? {
        switch status {
        case ._0, ._1: .still(.leaning)
        case ._2, ._3: .animated(.welcoming, loop: false, fallback: .waving)
        case ._4: .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        case ._5: .still(.ready)
        default: nil
        }
    }
}

struct OrderDetailMascotPuck: View {
    let status: OrderStatus?

    var body: some View {
        Group {
            switch OrderDetailMascotArt.art(for: status) {
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
        .accessibilityHidden(true)
    }
}
