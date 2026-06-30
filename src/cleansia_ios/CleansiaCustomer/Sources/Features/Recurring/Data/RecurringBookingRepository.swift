import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's recurring-booking templates (the
/// `RecurringBookingRepository.kt` parity). Keeps its own cache fresh on every
/// mutation. Registered in the `SessionScopedCacheRegistry` so sign-out /
/// forced-401 wipes it.
@MainActor
final class RecurringBookingRepository: SessionScopedCache {
    @Published private(set) var templates: [RecurringTemplate] = []
    @Published private(set) var loaded = false
    @Published private(set) var loading = false

    private let client: RecurringBookingClient

    init(client: RecurringBookingClient) {
        self.client = client
    }

    @discardableResult
    func refresh() async -> ApiResult<[RecurringTemplate]> {
        if loading { return .success(templates) }
        loading = true
        defer { loading = false }
        let result = await client.getMine()
        if case let .success(items) = result {
            templates = items
            loaded = true
        }
        return result
    }

    func create(_ input: CreateRecurringInput) async -> ApiResult<RecurringTemplate> {
        let result = await client.create(input)
        if case .success = result {
            await refreshForce()
        }
        return result
    }

    @discardableResult
    func setActive(templateId: String, isActive: Bool) async -> ApiResult<Void> {
        let result = await client.setActive(templateId: templateId, isActive: isActive)
        if case .success = result {
            await refreshForce()
        }
        return result
    }

    @discardableResult
    func delete(templateId: String) async -> ApiResult<Void> {
        let result = await client.delete(templateId: templateId)
        if case .success = result {
            await refreshForce()
        }
        return result
    }

    private func refreshForce() async {
        if case let .success(items) = await client.getMine() {
            templates = items
            loaded = true
        }
    }

    func clear() async {
        templates = []
        loaded = false
    }
}
