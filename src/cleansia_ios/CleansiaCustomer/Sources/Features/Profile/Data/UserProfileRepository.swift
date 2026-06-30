import CleansiaCore
import Combine
import Foundation

@MainActor
final class UserProfileRepository: SessionScopedCache {
    @Published private(set) var currentUser: CurrentUserProfile?

    private let client: UserProfileClient

    init(client: UserProfileClient) {
        self.client = client
    }

    @discardableResult
    func refresh() async -> ApiResult<CurrentUserProfile> {
        let result = await client.currentUser()
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
        currentUser = nil
    }
}
