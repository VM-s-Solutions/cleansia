import CleansiaCore
import Combine
import Foundation

@MainActor
final class UserProfileRepository: SessionScopedCache {
    @Published private(set) var currentUser: CurrentUserProfile?

    private let client: UserProfileClient
    private var sessionGeneration = 0

    init(client: UserProfileClient) {
        self.client = client
    }

    @discardableResult
    func refresh() async -> ApiResult<CurrentUserProfile> {
        let generation = sessionGeneration
        let result = await client.currentUser()
        guard generation == sessionGeneration else { return .failure(ApiError(code: ApiError.cancelledCode)) }
        if case let .success(user) = result {
            currentUser = user
        }
        return result
    }

    func update(_ update: ProfileUpdate) async -> ApiResult<Void> {
        let result = await client.updateCurrentUser(update)
        if case .success = result {
            await refresh()
        }
        return result
    }

    func clear() async {
        sessionGeneration += 1
        currentUser = nil
    }
}
