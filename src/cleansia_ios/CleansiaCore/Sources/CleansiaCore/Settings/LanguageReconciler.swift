import Combine
import Foundation

/// Calls the language-push seam once per session start, which is the whole remedy for the one hole the
/// push leaves open: it is silent on failure and has no retry, so a push that never reached the server
/// used to self-heal only when the user next opened the picker.
///
/// Three decisions, each deliberate and each mirrored on the other platforms:
///
/// - **A session beginning, not the sign-in callback.** A credential exchange is only one of the ways a
///   session starts — a token restored at cold start is another, and it is far more frequent. Each auth
///   path (password, social, email confirmation, the registration lock) is its own callback that a later
///   path can forget to call; the session signal is derived from the token state and is the single
///   funnel device registration already rides.
/// - **The seam decides whether anything is written.** What is passed in reads the server, compares, and
///   writes only on disagreement, so a server that already agrees costs one GET and no write. An
///   unconditional write would replay a locally cached profile over the server's on every launch, for a
///   value that was already correct.
/// - **Only an explicit choice reconciles.** With nothing persisted the resolved language is the
///   handset's locale ordering — an accident of the device, not evidence that anyone chose anything.
///   Pushing it would overwrite a language picked on another client, or set by support, and there is no
///   failed push to heal for a user who has never made one.
///
/// The reconcile is fired and never awaited. That is the same reason the push seam is not `async`: a
/// reconcile a view could bind to a `.task` dies with the view, and the view a sign-in navigates away
/// from is exactly the one it would be bound to.
public final class LanguageReconciler: @unchecked Sendable {
    private let settings: AppSettingsStore
    private let push: (String) -> Void
    private var cancellable: AnyCancellable?

    public init(settings: AppSettingsStore, push: @escaping (String) -> Void) {
        self.settings = settings
        self.push = push
    }

    public var isObservingSession: Bool {
        cancellable != nil
    }

    /// An edge, not a level: the session signal is re-sent at every route change, so a level would cost
    /// a profile round trip per screen the user walks through on one launch.
    public func attach(hasSession: AnyPublisher<Bool, Never>) {
        cancellable = hasSession
            .removeDuplicates()
            .filter { $0 }
            .sink { [weak self] _ in self?.reconcile() }
    }

    public func reconcile() {
        guard let tag = settings.persistedLanguageTag else { return }
        push(tag)
    }
}
