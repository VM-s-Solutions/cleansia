package cz.cleansia.partner.features.profile

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.profile.JobRadiusSnapshot
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * "Asked once" is two facts, not one: the server knows whether a radius is set, and only the device
 * knows whether we have already asked. Keying the prompt on the null radius alone would re-ask the
 * cleaner who deliberately chose the country-wide board every single time they open the app.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class JobRadiusPromptViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var appSettings: AppSettingsRepository

    @Before
    fun setUp() {
        repository = mockk()
        appSettings = mockk(relaxed = true)
    }

    private fun viewModel() = JobRadiusPromptViewModel(repository, appSettings)

    @Test
    fun `a cleaner who has never been asked and has no radius sees the prompt`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt() } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = null))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Visible, vm.uiState.value)
    }

    @Test
    fun `a cleaner who already set a radius is never asked again`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt() } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = 30))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify { appSettings.markJobRadiusPromptAnswered() }
    }

    @Test
    fun `the answered flag short-circuits the read entirely`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt() } returns true

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify(exactly = 0) { repository.getJobRadius() }
    }

    /** A prompt is not worth an error state: a failed read stays quiet and asks again next launch. */
    @Test
    fun `a failed read hides the prompt without spending the one ask`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt() } returns false
        coEvery { repository.getJobRadius() } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify(exactly = 0) { appSettings.markJobRadiusPromptAnswered() }
    }

    /**
     * "Keep every job" is an answer, not a deferral — null already IS the country-wide board, so
     * there is nothing to write to the server and nothing to ask again.
     */
    @Test
    fun `keeping every job answers the prompt locally and writes nothing`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt() } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.onKeepEveryJob()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify { appSettings.markJobRadiusPromptAnswered() }
        coVerify(exactly = 0) { repository.updateJobRadius(any(), any()) }
    }

    @Test
    fun `opening the picker also spends the one ask`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt() } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.onChooseRadius()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify { appSettings.markJobRadiusPromptAnswered() }
    }
}
