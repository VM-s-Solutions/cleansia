import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerProfileClient: AnyObject {
    func getCurrentEmployee() async -> ApiResult<EmployeeItem>
    func checkCurrentEmployee() async -> ApiResult<RegistrationCompletionStatus>

    func updatePersonalInfo(_ command: UpdatePersonalInfoCommand) async -> ApiResult<Void>
    func updateAddressInfo(_ command: UpdateAddressInfoCommand) async -> ApiResult<Void>
    func updateIdentificationInfo(_ command: UpdateIdentificationInfoCommand) async -> ApiResult<Void>
    func updateBankDetails(_ command: UpdateBankDetailsCommand) async -> ApiResult<Void>
    func updateEmergencyContact(_ command: UpdateEmergencyContactCommand) async -> ApiResult<Void>

    func getMyDocuments() async -> ApiResult<[GetMyDocumentsMyDocumentDto]>
    func saveMyDocuments(_ command: SaveMyDocumentsCommand) async -> ApiResult<Void>
    func deleteMyDocument(documentId: String) async -> ApiResult<Void>

    func getServicedCountries() async -> ApiResult<[CountryListItem]>
    func getAllCountries() async -> ApiResult<[CountryListItem]>
}

final class LivePartnerProfileClient: PartnerProfileClient, SessionScopedCache {
    func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeGetCurrentEmployee()
        }
    }

    func checkCurrentEmployee() async -> ApiResult<RegistrationCompletionStatus> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeCheckCurrentEmployee()
        }
    }

    func updatePersonalInfo(_ command: UpdatePersonalInfoCommand) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeUpdatePersonalInfo(updatePersonalInfoCommand: command)
        }
    }

    func updateAddressInfo(_ command: UpdateAddressInfoCommand) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeUpdateAddressInfo(updateAddressInfoCommand: command)
        }
    }

    func updateIdentificationInfo(_ command: UpdateIdentificationInfoCommand) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeUpdateIdentificationInfo(updateIdentificationInfoCommand: command)
        }
    }

    func updateBankDetails(_ command: UpdateBankDetailsCommand) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeUpdateBankDetails(updateBankDetailsCommand: command)
        }
    }

    func updateEmergencyContact(_ command: UpdateEmergencyContactCommand) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeUpdateEmergencyContact(updateEmergencyContactCommand: command)
        }
    }

    func getMyDocuments() async -> ApiResult<[GetMyDocumentsMyDocumentDto]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeGetMyDocuments().documents ?? []
        }
    }

    func saveMyDocuments(_ command: SaveMyDocumentsCommand) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeSaveMyDocuments(saveMyDocumentsCommand: command)
        }
    }

    func deleteMyDocument(documentId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeDeleteMyDocument(documentId: documentId)
        }
    }

    func getServicedCountries() async -> ApiResult<[CountryListItem]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerCountryAPI.countryGetServiced()
        }
    }

    func getAllCountries() async -> ApiResult<[CountryListItem]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerCountryAPI.countryGetOverview()
        }
    }

    func clear() async {}
}
