package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.core.database.dao.ProfileDao
import cz.cleansia.partner.core.database.entities.CachedProfile
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.NetworkMonitor
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.ResponseBody
import javax.inject.Inject
import javax.inject.Singleton

interface ProfileRepository {
    suspend fun getCurrentEmployee(): ApiResult<EmployeeProfile>
    suspend fun updateEmployee(profile: EmployeeProfile): ApiResult<EmployeeProfile>
    suspend fun getMyDocuments(): ApiResult<List<EmployeeDocument>>
    suspend fun saveDocuments(documents: List<Pair<ByteArray, String>>): ApiResult<List<EmployeeDocument>>
    suspend fun deleteDocument(documentId: String): ApiResult<Unit>
    suspend fun downloadDocument(documentId: String): ApiResult<ResponseBody>

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
        return safeApiCall(json) {
            apiService.getMyDocuments()
        }
    }

    override suspend fun saveDocuments(
        documents: List<Pair<ByteArray, String>>
    ): ApiResult<List<EmployeeDocument>> {
        val parts = documents.map { (data, fileName) ->
            val requestBody = data.toRequestBody("application/octet-stream".toMediaTypeOrNull())
            MultipartBody.Part.createFormData("documents", fileName, requestBody)
        }

        return safeApiCall(json) {
            apiService.saveDocuments(parts)
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
