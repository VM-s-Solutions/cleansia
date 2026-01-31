package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
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
        documents: List<Pair<ByteArray, String>>
    ): ApiResult<List<EmployeeDocument>> {
        delay(1000)
        val newDocs = documents.mapIndexed { index, (_, fileName) ->
            EmployeeDocument(
                id = "doc-new-$index-${System.currentTimeMillis()}",
                employeeId = MockDataProvider.MOCK_EMPLOYEE_ID,
                type = "Other",
                status = "Pending",
                fileName = fileName,
                mimeType = "application/octet-stream",
                fileSize = 100_000,
                uploadedAt = LocalDateTime.now().toString()
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

    override fun getCachedProfile(): Flow<EmployeeProfile?> = profileFlow

    override suspend fun getCachedProfileSync(): EmployeeProfile? = currentProfile

    override suspend fun clearCache() { /* no-op */ }
}
