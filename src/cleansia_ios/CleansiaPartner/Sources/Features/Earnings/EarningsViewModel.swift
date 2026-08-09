import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class EarningsViewModel: ViewModel {
    @Published private(set) var state: UiState<DashboardStatsDto> = .loading

    private let client: PartnerDashboardClient

    init(client: PartnerDashboardClient) {
        self.client = client
    }

    func load() async {
        if state.loadedValue == nil {
            state = .loading
        }

        let employeeId = await client.getCurrentEmployeeProfile().loadedEmployeeId

        switch await client.getStats(employeeId: employeeId) {
        case let .success(stats):
            state = .loaded(stats)
        case let .failure(error):
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }
}

private extension ApiResult where Success == EmployeeProfile {
    var loadedEmployeeId: String? {
        try? get().employee.id
    }
}
