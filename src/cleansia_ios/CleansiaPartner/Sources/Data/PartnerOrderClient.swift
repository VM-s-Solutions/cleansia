import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct OrderPageQuery {
    var statuses: [OrderStatus]
    var isUnassigned: Bool?
    var employeeId: String?
    var cleaningDateFrom: Date?
    var cleaningDateTo: Date?
    var sortField: String
    var sortAscending: Bool
    var offset: Int = 0
    var limit: Int = 50
}

protocol PartnerOrderClient: AnyObject {
    /// The signed-in cleaner's own employeeId — JWT-truth surrogate the VM passes
    /// to the "mine" panes (Active/History). O3: the client offers ONLY the
    /// caller's own id; it never echoes a foreign one.
    func currentEmployeeId() async -> ApiResult<String>

    func getPaged(_ query: OrderPageQuery) async -> ApiResult<[OrderListItem]>
    func getById(orderId: String) async -> ApiResult<OrderItem>

    func takeOrder(orderId: String) async -> ApiResult<Void>
    func notifyOnTheWay(orderId: String) async -> ApiResult<Void>
    func startOrder(orderId: String) async -> ApiResult<Void>
    func completeOrder(orderId: String, actualMinutes: Int?, notes: String?) async -> ApiResult<Void>

    func addNote(orderId: String, content: String) async -> ApiResult<Void>
    func updateNote(orderId: String, noteId: String, content: String) async -> ApiResult<Void>
    func deleteNote(orderId: String, noteId: String) async -> ApiResult<Void>

    func reportIssue(orderId: String, description: String) async -> ApiResult<Void>
    func updateIssue(orderId: String, issueId: String, description: String) async -> ApiResult<Void>
    func deleteIssue(orderId: String, issueId: String) async -> ApiResult<Void>
}

final class LivePartnerOrderClient: PartnerOrderClient {
    func currentEmployeeId() async -> ApiResult<String> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let employee = try await PartnerEmployeeAPI.employeeGetCurrentEmployee()
            guard let id = employee.id, !id.isEmpty else {
                throw ApiError(code: "orders.employee_id_missing")
            }
            return id
        }
    }

    func getPaged(_ query: OrderPageQuery) async -> ApiResult<[OrderListItem]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let sort = [SortDefinition(
                field: query.sortField,
                direction: query.sortAscending ? ._0 : ._1
            )]
            let paged = try await PartnerOrderAPI.orderGetPaged(
                filterEmployeeId: query.employeeId,
                filterCleaningDateFrom: query.cleaningDateFrom,
                filterCleaningDateTo: query.cleaningDateTo,
                filterOrderStatuses: query.statuses,
                filterIsUnassigned: query.isUnassigned,
                sort: sort,
                offset: query.offset,
                limit: query.limit
            )
            return paged.data ?? []
        }
    }

    func getById(orderId: String) async -> ApiResult<OrderItem> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerOrderAPI.orderGetById(orderId: orderId)
        }
    }

    func takeOrder(orderId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderTakeOrder(takeOrderCommand: TakeOrderCommand(orderId: orderId))
        }
    }

    func notifyOnTheWay(orderId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderNotifyOnTheWay(
                notifyOnTheWayCommand: NotifyOnTheWayCommand(orderId: orderId)
            )
        }
    }

    func startOrder(orderId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderStartOrder(startOrderCommand: StartOrderCommand(orderId: orderId))
        }
    }

    func completeOrder(orderId: String, actualMinutes: Int?, notes: String?) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderCompleteOrder(
                completeOrderCommand: CompleteOrderCommand(
                    orderId: orderId,
                    actualCompletionTimeMinutes: actualMinutes,
                    completionNotes: notes
                )
            )
        }
    }

    func addNote(orderId: String, content: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderAddNote(
                addOrderNoteCommand: AddOrderNoteCommand(orderId: orderId, content: content)
            )
        }
    }

    func updateNote(orderId: String, noteId: String, content: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderUpdateNote(
                updateOrderNoteCommand: UpdateOrderNoteCommand(orderId: orderId, noteId: noteId, content: content)
            )
        }
    }

    func deleteNote(orderId: String, noteId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderDeleteNote(orderId: orderId, noteId: noteId)
        }
    }

    func reportIssue(orderId: String, description: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderReportIssue(
                reportOrderIssueCommand: ReportOrderIssueCommand(orderId: orderId, description: description)
            )
        }
    }

    func updateIssue(orderId: String, issueId: String, description: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderUpdateIssue(
                updateOrderIssueCommand: UpdateOrderIssueCommand(
                    orderId: orderId,
                    issueId: issueId,
                    description: description
                )
            )
        }
    }

    func deleteIssue(orderId: String, issueId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerOrderAPI.orderDeleteIssue(orderId: orderId, issueId: issueId)
        }
    }
}
