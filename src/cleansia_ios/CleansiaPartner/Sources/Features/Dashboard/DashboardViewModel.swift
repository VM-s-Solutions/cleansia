import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class DashboardViewModel: ViewModel {
    @Published private(set) var state: UiState<DashboardData> = .loading

    private let client: PartnerDashboardClient

    init(client: PartnerDashboardClient) {
        self.client = client
    }

    func load() async {
        state = .loading

        let employee = await client.getCurrentEmployee()
        let firstName = employee.loadedFirstName
        let employeeId = employee.loadedEmployeeId

        switch await client.getStats(employeeId: employeeId) {
        case let .success(stats):
            state = .loaded(DashboardData.from(stats: stats, firstName: firstName))
        case let .failure(error):
            state = .error(error)
        }
    }
}

private extension ApiResult where Success == EmployeeItem {
    var loadedFirstName: String? {
        try? get().firstName
    }

    var loadedEmployeeId: String? {
        try? get().id
    }
}
