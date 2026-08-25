import CleansiaCore
import CleansiaPartnerApi
@testable import CleansiaPartner

@MainActor
final class FakePartnerProfileClient: PartnerProfileClient {
    var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
    var jobRadiusResult: ApiResult<Int?> = .success(nil)
    var statusResult: ApiResult<RegistrationCompletionStatus> = .success(RegistrationCompletionStatus())
    var servicedCountriesResult: ApiResult<[CountryListItem]> = .success([])
    var allCountriesResult: ApiResult<[CountryListItem]> = .success([])
    var documentsResult: ApiResult<[GetMyDocumentsMyDocumentDto]> = .success([])
    var requirementsResult: ApiResult<[MyDocumentRequirementDto]> = .success([])
    var fieldLabelsResult: ApiResult<GetCountryFieldLabelsCountryFieldLabelsDto?> = .success(nil)
    var payoutResult: ApiResult<MyPayoutDetails?> = .success(nil)

    var personalUpdateResult: ApiResult<Void> = .success(())
    var addressUpdateResult: ApiResult<Void> = .success(())
    var identificationUpdateResult: ApiResult<Void> = .success(())
    var bankUpdateResult: ApiResult<Void> = .success(())
    var emergencyUpdateResult: ApiResult<Void> = .success(())
    var saveDocumentsResult: ApiResult<Void> = .success(())
    var replaceDocumentResult: ApiResult<Void> = .success(())
    var requestDeletionResult: ApiResult<Void> = .success(())

    private(set) var personalCommand: UpdatePersonalInfoCommand?
    private(set) var addressCommand: UpdateAddressInfoCommand?
    private(set) var identificationCommand: UpdateIdentificationInfoCommand?
    private(set) var bankCommand: UpdateBankDetailsCommand?
    private(set) var emergencyCommand: UpdateEmergencyContactCommand?
    private(set) var saveDocumentsCommand: SaveMyDocumentsCommand?
    private(set) var replacedDocumentId: String?
    private(set) var replacedFile: BlobFileDto?
    private(set) var replacedDescription: String?
    private(set) var deletionRequestedFor: String?
    private(set) var deletionReason: String?
    private(set) var fieldLabelsRequestedFor: [String] = []
    private(set) var checkCount = 0
    private(set) var servicedCountriesCallCount = 0
    private(set) var jobRadiusCommand: UpdateJobRadiusCommand?

    func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
        employeeResult
    }

    func updateJobRadius(_ command: UpdateJobRadiusCommand) async -> ApiResult<Int?> {
        jobRadiusCommand = command
        return jobRadiusResult
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

    func getMyPayoutDetails() async -> ApiResult<MyPayoutDetails?> {
        payoutResult
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

    func getDocumentRequirements() async -> ApiResult<[MyDocumentRequirementDto]> {
        requirementsResult
    }

    func replaceDocument(
        documentId: String,
        file: BlobFileDto,
        description: String?
    ) async -> ApiResult<Void> {
        replacedDocumentId = documentId
        replacedFile = file
        replacedDescription = description
        return replaceDocumentResult
    }

    func requestDocumentDeletion(documentId: String, reason: String) async -> ApiResult<Void> {
        deletionRequestedFor = documentId
        deletionReason = reason
        return requestDeletionResult
    }

    func getServicedCountries() async -> ApiResult<[CountryListItem]> {
        servicedCountriesCallCount += 1
        return servicedCountriesResult
    }

    func getAllCountries() async -> ApiResult<[CountryListItem]> {
        allCountriesResult
    }

    func getCountryFieldLabels(
        countryId: String
    ) async -> ApiResult<GetCountryFieldLabelsCountryFieldLabelsDto?> {
        fieldLabelsRequestedFor.append(countryId)
        return fieldLabelsResult
    }
}
