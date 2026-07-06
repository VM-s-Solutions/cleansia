import CleansiaCore
import CleansiaPartnerApi
@testable import CleansiaPartner

@MainActor
final class FakePartnerProfileClient: PartnerProfileClient {
    var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
    var statusResult: ApiResult<RegistrationCompletionStatus> = .success(RegistrationCompletionStatus())
    var servicedCountriesResult: ApiResult<[CountryListItem]> = .success([])
    var allCountriesResult: ApiResult<[CountryListItem]> = .success([])
    var documentsResult: ApiResult<[GetMyDocumentsMyDocumentDto]> = .success([])

    var personalUpdateResult: ApiResult<Void> = .success(())
    var addressUpdateResult: ApiResult<Void> = .success(())
    var identificationUpdateResult: ApiResult<Void> = .success(())
    var bankUpdateResult: ApiResult<Void> = .success(())
    var emergencyUpdateResult: ApiResult<Void> = .success(())
    var saveDocumentsResult: ApiResult<Void> = .success(())
    var deleteDocumentResult: ApiResult<Void> = .success(())

    private(set) var personalCommand: UpdatePersonalInfoCommand?
    private(set) var addressCommand: UpdateAddressInfoCommand?
    private(set) var identificationCommand: UpdateIdentificationInfoCommand?
    private(set) var bankCommand: UpdateBankDetailsCommand?
    private(set) var emergencyCommand: UpdateEmergencyContactCommand?
    private(set) var saveDocumentsCommand: SaveMyDocumentsCommand?
    private(set) var deletedDocumentId: String?
    private(set) var checkCount = 0
    private(set) var servicedCountriesCallCount = 0

    func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
        employeeResult
    }

    func checkCurrentEmployee() async -> ApiResult<RegistrationCompletionStatus> {
        checkCount += 1
        return statusResult
    }

    func updatePersonalInfo(_ command: UpdatePersonalInfoCommand) async -> ApiResult<Void> {
        personalCommand = command
        return personalUpdateResult
    }

    func updateAddressInfo(_ command: UpdateAddressInfoCommand) async -> ApiResult<Void> {
        addressCommand = command
        return addressUpdateResult
    }

    func updateIdentificationInfo(_ command: UpdateIdentificationInfoCommand) async -> ApiResult<Void> {
        identificationCommand = command
        return identificationUpdateResult
    }

    func updateBankDetails(_ command: UpdateBankDetailsCommand) async -> ApiResult<Void> {
        bankCommand = command
        return bankUpdateResult
    }

    func updateEmergencyContact(_ command: UpdateEmergencyContactCommand) async -> ApiResult<Void> {
        emergencyCommand = command
        return emergencyUpdateResult
    }

    func getMyDocuments() async -> ApiResult<[GetMyDocumentsMyDocumentDto]> {
        documentsResult
    }

    func saveMyDocuments(_ command: SaveMyDocumentsCommand) async -> ApiResult<Void> {
        saveDocumentsCommand = command
        return saveDocumentsResult
    }

    func deleteMyDocument(documentId: String) async -> ApiResult<Void> {
        deletedDocumentId = documentId
        return deleteDocumentResult
    }

    func getServicedCountries() async -> ApiResult<[CountryListItem]> {
        servicedCountriesCallCount += 1
        return servicedCountriesResult
    }

    func getAllCountries() async -> ApiResult<[CountryListItem]> {
        allCountriesResult
    }
}
