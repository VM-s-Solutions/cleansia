package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.core.database.dao.ProfileDao
import cz.cleansia.partner.core.database.entities.CachedProfile
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.NetworkMonitor
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.domain.models.profile.BlobFileDto
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.DocumentToSave
import cz.cleansia.partner.domain.models.profile.DocumentType
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.domain.models.profile.RegistrationCompletionStatus
import cz.cleansia.partner.domain.models.profile.SaveMyDocumentsRequest
import cz.cleansia.partner.domain.models.profile.UpdateAddressInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateAvailabilityRequest
import cz.cleansia.partner.domain.models.profile.UpdateBankDetailsRequest
import cz.cleansia.partner.domain.models.profile.UpdateEmergencyContactRequest
import cz.cleansia.partner.domain.models.profile.UpdateIdentificationInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdatePersonalInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateSectionResponse
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.ResponseBody
import android.util.Base64
import javax.inject.Inject
import javax.inject.Singleton

interface ProfileRepository {
    suspend fun getCountries(): ApiResult<List<Country>>
    suspend fun checkRegistrationStatus(): ApiResult<RegistrationCompletionStatus>
    suspend fun getCurrentEmployee(): ApiResult<EmployeeProfile>
    suspend fun updateEmployee(profile: EmployeeProfile): ApiResult<EmployeeProfile>
    suspend fun getMyDocuments(): ApiResult<List<EmployeeDocument>>
    suspend fun saveDocuments(documents: List<Triple<ByteArray, String, DocumentType>>): ApiResult<List<EmployeeDocument>>
    suspend fun deleteDocument(documentId: String): ApiResult<Unit>
    suspend fun downloadDocument(documentId: String): ApiResult<ResponseBody>
    suspend fun uploadProfilePhoto(photoData: ByteArray, fileName: String): ApiResult<EmployeeProfile>

    // Per-section update methods
    suspend fun updatePersonalInfo(request: UpdatePersonalInfoRequest): ApiResult<UpdateSectionResponse>
    suspend fun updateIdentificationInfo(request: UpdateIdentificationInfoRequest): ApiResult<UpdateSectionResponse>
    suspend fun updateAddressInfo(request: UpdateAddressInfoRequest): ApiResult<UpdateSectionResponse>
    suspend fun updateBankDetails(request: UpdateBankDetailsRequest): ApiResult<UpdateSectionResponse>
    suspend fun updateEmergencyContact(request: UpdateEmergencyContactRequest): ApiResult<UpdateSectionResponse>
    suspend fun updateAvailability(request: UpdateAvailabilityRequest): ApiResult<UpdateSectionResponse>

    // Offline support methods
    fun getCachedProfile(): Flow<EmployeeProfile?>
    suspend fun getCachedProfileSync(): EmployeeProfile?
    suspend fun clearCache()
}

@Singleton
class ProfileRepositoryImpl @Inject constructor(
    private val apiService: ApiService,
    private val json: Json,
    private val profileDao: ProfileDao,
    private val networkMonitor: NetworkMonitor
) : ProfileRepository {

    override suspend fun getCountries(): ApiResult<List<Country>> {
        return safeApiCall(json) {
            apiService.getCountries()
        }
    }

    override suspend fun checkRegistrationStatus(): ApiResult<RegistrationCompletionStatus> {
        return safeApiCall(json) {
            apiService.checkCurrentEmployee()
        }
    }

    override suspend fun getCurrentEmployee(): ApiResult<EmployeeProfile> {
        val result = safeApiCall(json) {
            apiService.getCurrentEmployee()
        }

        // Cache successful result
        if (result is ApiResult.Success) {
            cacheProfile(result.data)
        }

        return result
    }

    /**
     * Cache profile to local database
     */
    private suspend fun cacheProfile(profile: EmployeeProfile) {
        try {
            profileDao.insertProfile(CachedProfile.fromDomainModel(profile))
        } catch (e: Exception) {
            // Ignore cache errors
        }
    }

    override suspend fun updateEmployee(profile: EmployeeProfile): ApiResult<EmployeeProfile> {
        return safeApiCall(json) {
            apiService.updateEmployee(profile)
        }
    }

    override suspend fun getMyDocuments(): ApiResult<List<EmployeeDocument>> {
        return when (val result = safeApiCall(json) { apiService.getMyDocuments() }) {
            is ApiResult.Success -> ApiResult.Success(result.data.documents)
            is ApiResult.Error -> result
        }
    }

    override suspend fun saveDocuments(
        documents: List<Triple<ByteArray, String, DocumentType>>
    ): ApiResult<List<EmployeeDocument>> {
        val request = SaveMyDocumentsRequest(
            documents = documents.map { (data, fileName, docType) ->
                val base64Content = Base64.encodeToString(data, Base64.NO_WRAP)
                val extension = fileName.substringAfterLast('.', "").lowercase()
                val contentType = when (extension) {
                    "pdf" -> "application/pdf"
                    "jpg", "jpeg" -> "image/jpeg"
                    "png" -> "image/png"
                    "doc" -> "application/msword"
                    "docx" -> "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    else -> "application/octet-stream"
                }
                DocumentToSave(
                    documentType = docType.apiNumericValue,
                    file = BlobFileDto(
                        fileName = fileName,
                        base64Content = base64Content,
                        contentType = contentType
                    )
                )
            }
        )

        return when (val result = safeApiCall(json) { apiService.saveDocuments(request) }) {
            is ApiResult.Success -> {
                // After successful save, reload all documents to get the full list
                getMyDocuments()
            }
            is ApiResult.Error -> result
        }
    }

    override suspend fun deleteDocument(documentId: String): ApiResult<Unit> {
        return safeApiCall(json) {
            apiService.deleteDocument(documentId)
        }
    }

    override suspend fun downloadDocument(documentId: String): ApiResult<ResponseBody> {
        return safeApiCall(json) {
            apiService.downloadDocument(documentId)
        }
    }

    override suspend fun uploadProfilePhoto(photoData: ByteArray, fileName: String): ApiResult<EmployeeProfile> {
        val requestBody = photoData.toRequestBody("image/*".toMediaTypeOrNull())
        val part = MultipartBody.Part.createFormData("photo", fileName, requestBody)
        return safeApiCall(json) {
            apiService.uploadProfilePhoto(part)
        }
    }

    // Per-section update methods

    override suspend fun updatePersonalInfo(request: UpdatePersonalInfoRequest): ApiResult<UpdateSectionResponse> {
        return safeApiCall(json) { apiService.updatePersonalInfo(request) }
    }

    override suspend fun updateIdentificationInfo(request: UpdateIdentificationInfoRequest): ApiResult<UpdateSectionResponse> {
        return safeApiCall(json) { apiService.updateIdentificationInfo(request) }
    }

    override suspend fun updateAddressInfo(request: UpdateAddressInfoRequest): ApiResult<UpdateSectionResponse> {
        return safeApiCall(json) { apiService.updateAddressInfo(request) }
    }

    override suspend fun updateBankDetails(request: UpdateBankDetailsRequest): ApiResult<UpdateSectionResponse> {
        return safeApiCall(json) { apiService.updateBankDetails(request) }
    }

    override suspend fun updateEmergencyContact(request: UpdateEmergencyContactRequest): ApiResult<UpdateSectionResponse> {
        return safeApiCall(json) { apiService.updateEmergencyContact(request) }
    }

    override suspend fun updateAvailability(request: UpdateAvailabilityRequest): ApiResult<UpdateSectionResponse> {
        return safeApiCall(json) { apiService.updateAvailability(request) }
    }

    // Offline support methods

    override fun getCachedProfile(): Flow<EmployeeProfile?> {
        return profileDao.getProfile().map { it?.toDomainModel() }
    }

    override suspend fun getCachedProfileSync(): EmployeeProfile? {
        return profileDao.getProfileSync()?.toDomainModel()
    }

    override suspend fun clearCache() {
        profileDao.deleteProfile()
    }
}
