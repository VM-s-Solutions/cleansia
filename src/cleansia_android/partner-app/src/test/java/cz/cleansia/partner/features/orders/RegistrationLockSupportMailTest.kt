package cz.cleansia.partner.features.orders

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test

/** The rejected-approval row copied the support address because no mail app took it; the cleaner is told so. */
@OptIn(ExperimentalCoroutinesApi::class)
class RegistrationLockSupportMailTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    @Test
    fun `a support tap no mail app takes says the address was copied`() = runTest {
        val snackbar = mockk<SnackbarController>(relaxed = true)
        val profileRepository: ProfileRepository = mockk(relaxed = true)
        every { profileRepository.registrationDecisions } returns MutableSharedFlow()
        every { profileRepository.getRegistrationStatusStaleness() } returns Staleness().apply { markFresh() }
        coEvery { profileRepository.getLegalDocuments(any()) } returns ApiResult.Success(emptyList())
        coEvery { profileRepository.getRegistrationStatus() } returns ApiResult.Success(
            RegistrationCompletionStatus(
                areDocumentsUploaded = true,
                hasCompletedProfile = true,
                missingFields = emptyList(),
                contractStatus = ContractStatus._5,
                rejectionReason = "Unreadable ID",
            ),
        )
        val vm = RegistrationLockViewModel(
            profileRepository = profileRepository,
            authRepository = mockk(relaxed = true),
            errorTranslator = mockk(relaxed = true),
            appSettingsRepository = mockk(relaxed = true),
            languageSync = mockk(relaxed = true),
            snackbar = snackbar,
        )
        advanceUntilIdle()

        vm.onSupportEmailUnavailable()

        verify(exactly = 1) { snackbar.showInfoKey(R.string.help_email_unavailable) }
    }
}
