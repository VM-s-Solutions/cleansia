import Foundation

/// Orthogonal to a list's sealed `UiState`: the refresh phase drives WHO sees a
/// spinner. The pull-to-refresh indicator binds to `.userRefreshing` ONLY — a
/// `.backgroundRefreshing` (auto / resume / silent re-fetch) is invisible. The
/// canonical replacement for Android's three boolean refresh flags (the E1
/// flag-bag); shared by every list view model.
public enum RefreshPhase: Equatable {
    case idle
    case userRefreshing
    case backgroundRefreshing
}
