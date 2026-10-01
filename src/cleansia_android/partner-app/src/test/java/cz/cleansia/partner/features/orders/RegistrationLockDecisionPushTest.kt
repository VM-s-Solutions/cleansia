package cz.cleansia.partner.features.orders

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

/**
 * A cleaner who leaves the lock showing "under review" and gets a decision push must not have to pull
 * to refresh: the push lands while the screen is already resumed, so no ON_RESUME re-reads it, and the
 * 15s stale window would swallow one that did.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class RegistrationLockDecisionPushTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private fun status(contract: ContractStatus) = RegistrationCompletionStatus(
        areDocumentsUploaded = true,
        hasCompletedProfile = true,
        missingFields = emptyList(),
        contractStatus = contract,
        rejectionReason = if (contract == ContractStatus._5) "Unreadable ID" else null,
    )

    @Test
    fun `a decision push re-reads the status inside the stale window`() = runTest {
        val decisions = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
        val profileRepository: ProfileRepository = mockk(relaxed = true)
        every { profileRepository.registrationDecisions } returns decisions
        // Fresh watermark: only the push, never the stale gate, can explain a second read.
        every { profileRepository.getRegistrationStatusStaleness() } returns Staleness().apply { markFresh() }
        coEvery { profileRepository.getLegalDocuments(any()) } returns ApiResult.Success(emptyList())
        coEvery { profileRepository.getRegistrationStatus() } returnsMany listOf(
            ApiResult.Success(status(ContractStatus._1)),
            ApiResult.Success(status(ContractStatus._5)),
        )
        val vm = RegistrationLockViewModel(
            profileRepository = profileRepository,
            authRepository = mockk(relaxed = true),
            errorTranslator = mockk(relaxed = true),
            appSettingsRepository = mockk(relaxed = true),
            languageSync = mockk(relaxed = true),
        )
        advanceUntilIdle()
        vm.onResume()
        advanceUntilIdle()
        assertEquals(ContractStatus._1, vm.uiState.value.status?.contractStatus)

        decisions.emit(Unit)
        advanceUntilIdle()

        assertEquals(ContractStatus._5, vm.uiState.value.status?.contractStatus)
        assertEquals(false, vm.uiState.value.isUserRefreshing)
        coVerify(exactly = 2) { profileRepository.getRegistrationStatus() }
    }
}
