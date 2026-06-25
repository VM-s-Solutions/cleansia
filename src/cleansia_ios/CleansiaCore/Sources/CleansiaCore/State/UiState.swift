import Foundation

public enum UiState<T> {
    case loading
    case error(ApiError)
    case loaded(T)
}

public extension UiState {
    var loadedValue: T? {
        if case let .loaded(value) = self { return value }
        return nil
    }

    var isLoading: Bool {
        if case .loading = self { return true }
        return false
    }
}
