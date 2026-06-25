import Foundation

public enum ActionState: Equatable {
    case idle
    case submitting
    case error(String)
}

public extension ActionState {
    var isSubmitting: Bool {
        if case .submitting = self { return true }
        return false
    }

    var errorMessage: String? {
        if case let .error(message) = self { return message }
        return nil
    }
}
