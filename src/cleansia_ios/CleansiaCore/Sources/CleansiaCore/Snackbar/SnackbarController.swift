import Combine
import Foundation

@MainActor
public final class SnackbarController: ObservableObject {
    @Published public private(set) var current: SnackbarMessage?

    private let localizer: ApiErrorLocalizing

    public init(localizer: ApiErrorLocalizing = ApiErrorLocalizer()) {
        self.localizer = localizer
    }

    public func show(_ message: SnackbarMessage) {
        current = message
    }

    public func dismiss() {
        current = nil
    }

    public func dismiss(id: UUID) {
        if current?.id == id { current = nil }
    }

    public func showApiError(_ error: ApiError) {
        show(SnackbarMessage(text: localizer.message(for: error), severity: .error))
    }

    public func showError(_ text: String) {
        show(SnackbarMessage(text: text, severity: .error))
    }

    public func showSuccess(_ text: String) {
        show(SnackbarMessage(text: text, severity: .success))
    }

    public func showInfo(_ text: String) {
        show(SnackbarMessage(text: text, severity: .info))
    }

    public func showWarning(_ text: String) {
        show(SnackbarMessage(text: text, severity: .warning))
    }
}
