import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol RecurringBookingClient: Sendable {
    func getMine() async -> ApiResult<[RecurringTemplate]>
    func create(_ input: CreateRecurringInput) async -> ApiResult<RecurringTemplate>
    func setActive(templateId: String, isActive: Bool) async -> ApiResult<Void>
    func delete(templateId: String) async -> ApiResult<Void>
}

struct LiveRecurringBookingClient: RecurringBookingClient {
    func getMine() async -> ApiResult<[RecurringTemplate]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI.recurringBookingGetMine()
        }
        return result.map { $0.compactMap { $0.toDomain() } }
    }

    func create(_ input: CreateRecurringInput) async -> ApiResult<RecurringTemplate> {
        let command = CreateRecurringBookingCommand(
            frequency: input.frequency,
            dayOfWeek: input.dayOfWeek,
            timeOfDay: input.timeOfDay,
            rooms: input.rooms,
            bathrooms: input.bathrooms,
            savedAddressId: input.savedAddressId,
            selectedServiceIds: input.selectedServiceIds,
            selectedPackageIds: input.selectedPackageIds,
            paymentType: input.paymentType,
            startsOn: input.startsOn,
            endsOn: nil
        )
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI.recurringBookingCreate(createRecurringBookingCommand: command)
        }
        switch result {
        case let .success(dto):
            guard let template = dto.toDomain() else { return .failure(ApiError(code: "recurring.malformed")) }
            return .success(template)
        case let .failure(error):
            return .failure(error)
        }
    }

    func setActive(templateId: String, isActive: Bool) async -> ApiResult<Void> {
        let command = SetRecurringBookingActiveCommand(templateId: templateId, isActive: isActive)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI.recurringBookingSetActive(setRecurringBookingActiveCommand: command)
        }
    }

    func delete(templateId: String) async -> ApiResult<Void> {
        let command = DeleteRecurringBookingCommand(templateId: templateId)
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI.recurringBookingDelete(deleteRecurringBookingCommand: command)
        }
    }
}

private extension RecurringBookingTemplateDto {
    func toDomain() -> RecurringTemplate? {
        guard let id,
              let frequency,
              let dayOfWeek,
              let timeOfDay,
              let savedAddressId,
              let paymentType,
              let startsOn,
              let isActive
        else { return nil }
        return RecurringTemplate(
            id: id,
            frequency: frequency,
            dayOfWeek: dayOfWeek,
            timeOfDay: timeOfDay,
            rooms: rooms ?? 0,
            bathrooms: bathrooms ?? 0,
            savedAddressId: savedAddressId,
            addressLine: addressLine,
            selectedServiceIds: selectedServiceIds ?? [],
            selectedPackageIds: selectedPackageIds ?? [],
            paymentType: paymentType,
            startsOn: startsOn,
            endsOn: endsOn,
            isActive: isActive
        )
    }
}
