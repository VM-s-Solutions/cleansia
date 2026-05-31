package cz.cleansia.partner.data.profile

import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.EmployeeEntityType
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.api.model.GetMyDocumentsMyDocumentDto
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.api.model.SaveMyDocumentsCommand
import cz.cleansia.partner.api.model.SaveMyDocumentsDocumentToSave
import cz.cleansia.partner.api.model.UpdateAddressInfoCommand
import cz.cleansia.partner.api.model.UpdateAvailabilityCommand
import cz.cleansia.partner.api.model.UpdateAvailabilityTimeRangeDto
import cz.cleansia.partner.api.model.UpdateBankDetailsCommand
import cz.cleansia.partner.api.model.UpdateEmergencyContactCommand
import cz.cleansia.partner.api.model.UpdateIdentificationInfoCommand
import cz.cleansia.partner.api.model.UpdatePersonalInfoCommand
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.safeApiCall
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Profile read + section-by-section update contract. Mirrors the backend's
 * partial-update endpoints (one PATCH-style command per section) so the UI
 * can persist a section without sending the whole employee object back.
 *
 * `employeeId` is always required on the update commands — it comes from
 * the cached profile (read once on screen entry, cached in the VM).
 */
interface ProfileRepository {
    suspend fun getCurrentEmployee(): ApiResult<EmployeeItem>

    /**
     * Checks whether the cleaner can take orders. Returns the same shape
     * the partner web reads from `/api/Employee/CheckCurrentEmployee`:
     * which of profile / availability / documents are complete, plus
     * contract status (Pending/Approved/Active/Rejected). Used by the
     * registration lock that gates the Orders tab.
     */
    suspend fun getRegistrationStatus(): ApiResult<RegistrationCompletionStatus>

    /**
     * Freshness watermark for [getRegistrationStatus]. ViewModels check
     * [Staleness.isStale] before triggering background refreshes (silent-
     * stale pattern) so screen-resume on a still-fresh cache skips the
     * network round-trip. User-initiated pulls ignore this and always
     * fetch — the user's intent is the source of truth, not the cache age.
     *
     * Marked fresh ONLY on a successful fetch; errors leave the watermark
     * untouched so the next entry will still retry. Watermark survives
     * for the lifetime of the singleton repo (no per-screen reset).
     */
    fun getRegistrationStatusStaleness(): Staleness

    suspend fun updatePersonalInfo(
        employeeId: String,
        firstName: String,
        lastName: String,
        birthDate: String?,
        phone: String?,
        email: String?,
    ): ApiResult<Unit>

    /**
     * Saves the cleaner's home address. [latitude]/[longitude] are
     * optional — when both are non-null the backend trusts them as-is
     * (map-picker flow); when either is null the backend falls back to
     * server-side geocoding (typed-text flow). Mirrors the customer
     * SavedAddress contract — coords picked on a map shouldn't get
     * silently re-geocoded into the neighbouring building.
     */
    suspend fun updateAddress(
        employeeId: String,
        street: String,
        city: String,
        zipCode: String,
        countryId: String,
        state: String?,
        latitude: Double?,
        longitude: Double?,
    ): ApiResult<Unit>

    /**
     * Saves nationality + passport AND the cleaner's business identity
     * (entity type, IČO/registration number, optional VAT number, legal
     * entity name when applicable) in one server call.
     *
     * [businessCountryId] scopes the IČO/VAT format check on the server —
     * different countries have different patterns. The UI defaults this
     * to the cleaner's home-address country but lets them override.
     */
    suspend fun updateIdentification(
        employeeId: String,
        nationalityId: String,
        passportId: String,
        entityType: EmployeeEntityType,
        businessCountryId: String,
        registrationNumber: String,
        vatNumber: String?,
        legalEntityName: String?,
    ): ApiResult<Unit>

    suspend fun updateBankDetails(
        employeeId: String,
        iban: String,
    ): ApiResult<Unit>

    suspend fun updateEmergencyContact(
        employeeId: String,
        emergencyName: String,
        emergencyPhone: String,
    ): ApiResult<Unit>

    /** [availability] maps `Monday..Sunday` (ISO English) → list of `HH:mm-HH:mm` windows. */
    suspend fun updateAvailability(
        employeeId: String,
        availability: Map<String, List<Pair<String, String>>>,
    ): ApiResult<Unit>

    suspend fun getMyDocuments(): ApiResult<List<GetMyDocumentsMyDocumentDto>>

    suspend fun saveDocuments(
        documents: List<SaveMyDocumentsDocumentToSave>,
    ): ApiResult<Unit>

    suspend fun deleteDocument(documentId: String): ApiResult<Unit>
}

@Singleton
class ProfileRepositoryImpl @Inject constructor(
    private val employeeApi: EmployeeApi,
    private val json: Json,
) : ProfileRepository {

    /**
     * Watermark for the registration-status cache. Stamped on every
     * successful [getRegistrationStatus] fetch; errors intentionally
     * leave it untouched so a failed first attempt doesn't gate out
     * the immediate retry. Exposed via [getRegistrationStatusStaleness]
     * so the registration-lock ViewModel can run its silent-stale check
     * without us having to leak mutable state to it.
     */
    private val registrationStatusStaleness = Staleness()

    override suspend fun getCurrentEmployee(): ApiResult<EmployeeItem> =
        safeApiCall(json) { employeeApi.employeeGetCurrentEmployee() }

    override suspend fun getRegistrationStatus(): ApiResult<RegistrationCompletionStatus> =
        safeApiCall(json) { employeeApi.employeeCheckCurrentEmployee() }
            .also { result ->
                // Only stamp the watermark on success — a failed fetch
                // should NOT reset/advance the stale gate, otherwise the
                // next entry would think we have fresh data when we just
                // logged a network error and have nothing new to show.
                if (result is ApiResult.Success) registrationStatusStaleness.markFresh()
            }

    override fun getRegistrationStatusStaleness(): Staleness = registrationStatusStaleness

    override suspend fun updatePersonalInfo(
        employeeId: String,
        firstName: String,
        lastName: String,
        birthDate: String?,
        phone: String?,
        email: String?,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeUpdatePersonalInfo(
            UpdatePersonalInfoCommand(
                employeeId = employeeId,
                firstName = firstName,
                lastName = lastName,
                birthDate = birthDate,
                phone = phone,
                email = email,
            ),
        )
    }.map { }

    override suspend fun updateAddress(
        employeeId: String,
        street: String,
        city: String,
        zipCode: String,
        countryId: String,
        state: String?,
        latitude: Double?,
        longitude: Double?,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeUpdateAddressInfo(
            UpdateAddressInfoCommand(
                employeeId = employeeId,
                street = street,
                city = city,
                zipCode = zipCode,
                countryId = countryId,
                state = state,
                latitude = latitude,
                longitude = longitude,
            ),
        )
    }.map { }

    override suspend fun updateIdentification(
        employeeId: String,
        nationalityId: String,
        passportId: String,
        entityType: EmployeeEntityType,
        businessCountryId: String,
        registrationNumber: String,
        vatNumber: String?,
        legalEntityName: String?,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeUpdateIdentificationInfo(
            UpdateIdentificationInfoCommand(
                employeeId = employeeId,
                nationalityId = nationalityId,
                passportId = passportId,
                entityType = entityType,
                businessCountryId = businessCountryId,
                registrationNumber = registrationNumber,
                vatNumber = vatNumber,
                legalEntityName = legalEntityName,
            ),
        )
    }.map { }

    override suspend fun updateBankDetails(
        employeeId: String,
        iban: String,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeUpdateBankDetails(
            UpdateBankDetailsCommand(employeeId = employeeId, iban = iban),
        )
    }.map { }

    override suspend fun updateEmergencyContact(
        employeeId: String,
        emergencyName: String,
        emergencyPhone: String,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeUpdateEmergencyContact(
            UpdateEmergencyContactCommand(
                employeeId = employeeId,
                emergencyName = emergencyName,
                emergencyPhone = emergencyPhone,
            ),
        )
    }.map { }

    override suspend fun updateAvailability(
        employeeId: String,
        availability: Map<String, List<Pair<String, String>>>,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeUpdateAvailability(
            UpdateAvailabilityCommand(
                employeeId = employeeId,
                availability = availability.mapValues { (_, ranges) ->
                    ranges.map { (start, end) ->
                        UpdateAvailabilityTimeRangeDto(start = start, end = end)
                    }
                },
            ),
        )
    }.map { }

    override suspend fun getMyDocuments(): ApiResult<List<GetMyDocumentsMyDocumentDto>> =
        safeApiCall(json) { employeeApi.employeeGetMyDocuments() }
            .map { it.documents.orEmpty() }

    override suspend fun saveDocuments(
        documents: List<SaveMyDocumentsDocumentToSave>,
    ): ApiResult<Unit> = safeApiCall(json) {
        employeeApi.employeeSaveMyDocuments(SaveMyDocumentsCommand(documents = documents))
    }.map { }

    override suspend fun deleteDocument(documentId: String): ApiResult<Unit> =
        safeApiCall(json) { employeeApi.employeeDeleteMyDocument(documentId = documentId) }
            .map { }
}
