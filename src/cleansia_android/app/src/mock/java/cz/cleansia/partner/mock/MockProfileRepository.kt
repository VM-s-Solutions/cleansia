package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.DocumentType
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.domain.models.profile.RegistrationCompletionStatus
import cz.cleansia.partner.domain.models.profile.UpdateAddressInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateAvailabilityRequest
import cz.cleansia.partner.domain.models.profile.UpdateBankDetailsRequest
import cz.cleansia.partner.domain.models.profile.UpdateEmergencyContactRequest
import cz.cleansia.partner.domain.models.profile.UpdateIdentificationInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdatePersonalInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateSectionResponse
import cz.cleansia.partner.domain.repositories.ProfileRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.ResponseBody
import okhttp3.ResponseBody.Companion.toResponseBody
import java.time.LocalDateTime

class MockProfileRepository : ProfileRepository {

    private val profileFlow = MutableStateFlow<EmployeeProfile?>(MockDataProvider.employeeProfile())
    private var currentProfile = MockDataProvider.employeeProfile()
    private val documents = MockDataProvider.employeeDocuments().toMutableList()

    override suspend fun getCountries(): ApiResult<List<Country>> {
        delay(200)
        return ApiResult.Success(MockDataProvider.countries())
    }

    override suspend fun checkRegistrationStatus(): ApiResult<RegistrationCompletionStatus> {
        delay(300)
        return ApiResult.Success(
            RegistrationCompletionStatus(
                areDocumentsUploaded = true,
                hasCompletedProfile = true
            )
        )
    }

    override suspend fun getCurrentEmployee(): ApiResult<EmployeeProfile> {
        delay(400)
        return ApiResult.Success(currentProfile)
    }

    override suspend fun updateEmployee(profile: EmployeeProfile): ApiResult<EmployeeProfile> {
        delay(600)
        currentProfile = profile
        profileFlow.value = profile
        return ApiResult.Success(profile)
    }

    override suspend fun getMyDocuments(): ApiResult<List<EmployeeDocument>> {
        delay(400)
        return ApiResult.Success(documents.toList())
    }

    override suspend fun saveDocuments(
        documents: List<Triple<ByteArray, String, DocumentType>>
    ): ApiResult<List<EmployeeDocument>> {
        delay(1000)
        val newDocs = documents.mapIndexed { index, (_, fileName, docType) ->
            EmployeeDocument(
                id = "doc-new-$index-${System.currentTimeMillis()}",
                fileName = fileName,
                mimeType = "application/octet-stream",
                fileSize = 100_000,
                documentTypeValue = docType.apiNumericValue,
                statusValue = 1, // Pending
                uploadedAt = LocalDateTime.now().toString(),
                version = 1
            )
        }
        this.documents.addAll(newDocs)
        return ApiResult.Success(this.documents.toList())
    }

    override suspend fun deleteDocument(documentId: String): ApiResult<Unit> {
        delay(400)
        documents.removeAll { it.id == documentId }
        return ApiResult.Success(Unit)
    }

    override suspend fun downloadDocument(documentId: String): ApiResult<ResponseBody> {
        delay(500)
        val mockContent = "Mock document content for $documentId".toByteArray()
        return ApiResult.Success(mockContent.toResponseBody(null))
    }

    override suspend fun uploadProfilePhoto(photoData: ByteArray, fileName: String): ApiResult<EmployeeProfile> {
        delay(800)
        // In mock, we just return the current profile (no actual upload)
        return ApiResult.Success(currentProfile)
    }

    // Per-section update methods

    override suspend fun updatePersonalInfo(request: UpdatePersonalInfoRequest): ApiResult<UpdateSectionResponse> {
        delay(500)
        return ApiResult.Success(UpdateSectionResponse(employeeId = MockDataProvider.MOCK_EMPLOYEE_ID))
    }

    override suspend fun updateIdentificationInfo(request: UpdateIdentificationInfoRequest): ApiResult<UpdateSectionResponse> {
        delay(500)
        return ApiResult.Success(UpdateSectionResponse(employeeId = MockDataProvider.MOCK_EMPLOYEE_ID))
    }

    override suspend fun updateAddressInfo(request: UpdateAddressInfoRequest): ApiResult<UpdateSectionResponse> {
        delay(500)
        return ApiResult.Success(UpdateSectionResponse(employeeId = MockDataProvider.MOCK_EMPLOYEE_ID))
    }

    override suspend fun updateBankDetails(request: UpdateBankDetailsRequest): ApiResult<UpdateSectionResponse> {
        delay(500)
        return ApiResult.Success(UpdateSectionResponse(employeeId = MockDataProvider.MOCK_EMPLOYEE_ID))
    }

    override suspend fun updateEmergencyContact(request: UpdateEmergencyContactRequest): ApiResult<UpdateSectionResponse> {
        delay(500)
        return ApiResult.Success(UpdateSectionResponse(employeeId = MockDataProvider.MOCK_EMPLOYEE_ID))
    }

    override suspend fun updateAvailability(request: UpdateAvailabilityRequest): ApiResult<UpdateSectionResponse> {
        delay(500)
        return ApiResult.Success(UpdateSectionResponse(employeeId = MockDataProvider.MOCK_EMPLOYEE_ID))
    }

    override fun getCachedProfile(): Flow<EmployeeProfile?> = profileFlow

    override suspend fun getCachedProfileSync(): EmployeeProfile? = currentProfile

    override suspend fun clearCache() { /* no-op */ }
}
