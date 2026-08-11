import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol RecurringBookingClient: Sendable {
    func getMine() async -> ApiResult<[RecurringTemplate]>
    func create(_ input: CreateRecurringInput) async -> ApiResult<RecurringTemplate>
    func update(_ input: UpdateRecurringInput) async -> ApiResult<RecurringTemplate>
    func setActive(templateId: String, isActive: Bool) async -> ApiResult<Void>
    func delete(templateId: String) async -> ApiResult<Void>
}

struct LiveRecurringBookingClient: RecurringBookingClient {
    func getMine() async -> ApiResult<[RecurringTemplate]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI.recurringBookingGetMine().map { try $0.toDomain() }
        }
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
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI
                .recurringBookingCreate(createRecurringBookingCommand: command)
                .toDomain()
        }
    }

    func update(_ input: UpdateRecurringInput) async -> ApiResult<RecurringTemplate> {
        let command = UpdateRecurringBookingCommand(
            templateId: input.templateId,
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
            endsOn: input.endsOn
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerRecurringBookingAPI
                .recurringBookingUpdate(updateRecurringBookingCommand: command)
                .toDomain()
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

/// **Refuse the page.** A dropped template is a standing booking that keeps materializing orders the
/// customer can no longer see, pause or cancel from this screen — the one place they can. `rooms`
/// and `bathrooms` are the scope every generated order is priced from, so a `0` understates a repeat
/// charge rather than one.
private extension RecurringBookingTemplateDto {
    func toDomain() throws -> RecurringTemplate {
        try RecurringTemplate(
            id: id.requireNonBlank("id"),
            frequency: frequency.require("frequency"),
            dayOfWeek: dayOfWeek.require("dayOfWeek"),
            timeOfDay: timeOfDay.requireNonBlank("timeOfDay"),
            rooms: rooms.require("rooms"),
            bathrooms: bathrooms.require("bathrooms"),
            savedAddressId: savedAddressId.requireNonBlank("savedAddressId"),
            addressLine: addressLine,
            selectedServiceIds: selectedServiceIds ?? [],
            selectedPackageIds: selectedPackageIds ?? [],
            paymentType: paymentType.require("paymentType"),
            startsOn: startsOn.require("startsOn"),
            endsOn: endsOn,
            isActive: isActive.require("isActive")
        )
    }
}
