import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerProfileClient: AnyObject {
    func getCurrentEmployee() async -> ApiResult<EmployeeItem>
    func checkCurrentEmployee() async -> ApiResult<RegistrationCompletionStatus>

    func updateJobRadius(_ command: UpdateJobRadiusCommand) async -> ApiResult<Int?>

    func updatePersonalInfo(_ command: UpdatePersonalInfoCommand) async -> ApiResult<Void>
    func updateAddressInfo(_ command: UpdateAddressInfoCommand) async -> ApiResult<Void>
    func updateIdentificationInfo(_ command: UpdateIdentificationInfoCommand) async -> ApiResult<Void>
    func updateEmergencyContact(_ command: UpdateEmergencyContactCommand) async -> ApiResult<Void>

    /// `nil` means the cleaner has no payout destination yet — see `PayoutDetailsRead`.
    func getMyPayoutDetails() async -> ApiResult<MyPayoutDetails?>
    func updateBankDetails(_ command: UpdateBankDetailsCommand) async -> ApiResult<Void>

    func getMyDocuments() async -> ApiResult<[GetMyDocumentsMyDocumentDto]>
    func saveMyDocuments(_ command: SaveMyDocumentsCommand) async -> ApiResult<Void>

    /// What this cleaner's country asks for, resolved against what they have uploaded. Empty for a
    /// country that configures nothing, which is every unseeded market.
    func getDocumentRequirements() async -> ApiResult<[MyDocumentRequirementDto]>

    /// Supersede a document with a newer file. Needs no admin because the slot never empties: the
    /// server creates the new version before retiring the old one, so `AreDocumentsUploaded` never
    /// dips and the registration lock never re-engages. The document TYPE is not a parameter — the
    /// server carries it over from the version being replaced.
    func replaceDocument(documentId: String, file: BlobFileDto, description: String?)
        async -> ApiResult<Void>

    /// Ask an admin to remove a document. It removes NOTHING — the document stays active until the
    /// request is answered. This replaced `deleteMyDocument`, which soft-deleted on the spot and so
    /// re-engaged the registration lock: one tap, no dialog, and a cleaner had lost their access to
    /// work — on documents the employer is required to hold.
    func requestDocumentDeletion(documentId: String, reason: String) async -> ApiResult<Void>

    func getServicedCountries() async -> ApiResult<[CountryListItem]>
    func getAllCountries() async -> ApiResult<[CountryListItem]>

    /// What a country calls its registration number and VAT id. `nil` when we hold no configuration
    /// for it — the caller falls back to its own neutral wording rather than inventing a label.
    func getCountryFieldLabels(countryId: String)
        async -> ApiResult<GetCountryFieldLabelsCountryFieldLabelsDto?>
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

    func updateJobRadius(_ command: UpdateJobRadiusCommand) async -> ApiResult<Int?> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeUpdateJobRadius(updateJobRadiusCommand: command).radiusKm
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

    func getMyPayoutDetails() async -> ApiResult<MyPayoutDetails?> {
        await PayoutDetailsRead.normalize(
            apiResult(mapError: ApiError.fromGenerated) {
                try await PartnerEmployeeAPI.employeeGetMyPayoutDetails()
            }
        )
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

    func getDocumentRequirements() async -> ApiResult<[MyDocumentRequirementDto]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeGetMyDocumentRequirements()
        }
    }

    func replaceDocument(
        documentId: String,
        file: BlobFileDto,
        description: String?
    ) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeReplaceMyDocument(
                documentId: documentId,
                replaceMyDocumentRequest: ReplaceMyDocumentRequest(
                    file: file,
                    description: description
                )
            )
        }
    }

    func requestDocumentDeletion(documentId: String, reason: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await PartnerEmployeeAPI.employeeRequestMyDocumentDeletion(
                documentId: documentId,
                requestMyDocumentDeletionRequest: RequestMyDocumentDeletionRequest(reason: reason)
            )
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

    /// A 404 here is an ANSWER, not a failure: it says the platform holds no configuration for this
    /// country, which is exactly the case the caller's neutral fallback exists for. Surfacing it as
    /// an error would report a missing translation as a broken form.
    func getCountryFieldLabels(
        countryId: String
    ) async -> ApiResult<GetCountryFieldLabelsCountryFieldLabelsDto?> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerCountryAPI.countryGetFieldLabels(countryId: countryId)
        }
        switch result {
        case let .success(labels):
            return .success(labels)
        case .failure:
            return .success(nil)
        }
    }

    func clear() async {}
}
