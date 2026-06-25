import Foundation

public enum SnackbarSeverity: Equatable {
    case error
    case success
    case info
    case warning
}

public struct SnackbarMessage: Identifiable, Equatable {
    public let id: UUID
    public let text: String
    public let severity: SnackbarSeverity

    public init(id: UUID = UUID(), text: String, severity: SnackbarSeverity) {
        self.id = id
        self.text = text
        self.severity = severity
    }

    public var autoDismissDuration: TimeInterval {
        severity == .error ? 6 : 3.5
    }
}
