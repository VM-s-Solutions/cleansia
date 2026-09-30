import CleansiaCustomerApi

/// How soiled the customer says the home is. The server prices it; the app only offers the choice and
/// names what was priced.
enum Dirtiness: CaseIterable, Equatable {
    case normal
    case increased
    case heavy

    var wire: DirtinessLevel {
        switch self {
        case .normal: ._0
        case .increased: ._1
        case .heavy: ._2
        }
    }

    init(wire: DirtinessLevel) {
        switch wire {
        case ._0: self = .normal
        case ._1: self = .increased
        case ._2: self = .heavy
        }
    }
}
