import Combine
import Foundation

@MainActor
public final class SnackbarController: ObservableObject {
    public static let defaultBottomInset: CGFloat = 16

    @Published public private(set) var current: SnackbarMessage?

    /// The Android `SnackbarInsetScope` parity: screens with bottom chrome
    /// (the customer shell's pill bar) lift every host that doesn't pin its
    /// own inset. Hosts inside modal sheets pass an explicit `bottomInset:`
    /// so a shell-scoped lift never leaks into them.
    @Published public private(set) var bottomInset: CGFloat = SnackbarController.defaultBottomInset

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

    public func setBottomInset(_ inset: CGFloat) {
        bottomInset = inset
    }

    public func resetBottomInset() {
        bottomInset = Self.defaultBottomInset
    }
}
