package cz.cleansia.partner.features.profile

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.auth.EmployeeIdResolver
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
 * "Asked once" is three facts, not one: the server knows whether a radius is set, the device knows
 * whether we have already asked — and the ask belongs to a cleaner, not to the handset. Keying the
 * prompt on the null radius alone would re-ask the cleaner who deliberately chose the country-wide
 * board every launch; keying it on the device alone silently never asks the second cleaner to sign
 * in on a shared phone.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class JobRadiusPromptViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var appSettings: AppSettingsRepository
    private lateinit var employeeIdResolver: EmployeeIdResolver

    @Before
    fun setUp() {
        repository = mockk()
        appSettings = mockk(relaxed = true)
        employeeIdResolver = mockk()
        coEvery { employeeIdResolver.resolve() } returns "emp-1"
    }

    private fun viewModel() = JobRadiusPromptViewModel(repository, appSettings, employeeIdResolver)

    @Test
    fun `a cleaner who has never been asked and has no radius sees the prompt`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = null))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Visible, vm.uiState.value)
    }

    @Test
    fun `a cleaner who already set a radius is never asked again`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = 30))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify { appSettings.markJobRadiusPromptAnswered("emp-1") }
    }

    @Test
    fun `the answered flag short-circuits the read entirely`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns true

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify(exactly = 0) { repository.getJobRadius() }
    }

    /**
     * The defect this shape exists to prevent: a cleaning company's phone is shared, and the second
     * cleaner to sign in must still be asked. A question that stops being asked reports nothing.
     */
    @Test
    fun `the second cleaner on a shared device is still asked`() = runTest {
        coEvery { employeeIdResolver.resolve() } returns "emp-2"
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns true
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-2") } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-2", jobRadiusKm = null))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Visible, vm.uiState.value)
    }

    @Test
    fun `the ask is spent against the cleaner who answered and no one else`() = runTest {
        coEvery { employeeIdResolver.resolve() } returns "emp-2"
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-2") } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-2", jobRadiusKm = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.onKeepEveryJob()
        advanceUntilIdle()

        coVerify { appSettings.markJobRadiusPromptAnswered("emp-2") }
        coVerify(exactly = 0) { appSettings.markJobRadiusPromptAnswered("emp-1") }
    }

    /** A prompt is not worth an error state: a failed read stays quiet and asks again next launch. */
    @Test
    fun `a failed read hides the prompt without spending the one ask`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns false
        coEvery { repository.getJobRadius() } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify(exactly = 0) { appSettings.markJobRadiusPromptAnswered(any()) }
    }

    /** Same rule one level up: with no cleaner to ask, there is no one to spend the ask against. */
    @Test
    fun `an unresolved cleaner hides the prompt without spending the one ask`() = runTest {
        coEvery { employeeIdResolver.resolve() } returns null

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify(exactly = 0) { repository.getJobRadius() }
        coVerify(exactly = 0) { appSettings.markJobRadiusPromptAnswered(any()) }
    }

    /**
     * "Keep every job" is an answer, not a deferral — null already IS the country-wide board, so
     * there is nothing to write to the server and nothing to ask again.
     */
    @Test
    fun `keeping every job answers the prompt locally and writes nothing`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.onKeepEveryJob()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify { appSettings.markJobRadiusPromptAnswered("emp-1") }
        coVerify(exactly = 0) { repository.updateJobRadius(any(), any()) }
    }

    @Test
    fun `opening the picker also spends the one ask`() = runTest {
        coEvery { appSettings.hasAnsweredJobRadiusPrompt("emp-1") } returns false
        coEvery { repository.getJobRadius() } returns
            ApiResult.Success(JobRadiusSnapshot(id = "emp-1", jobRadiusKm = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.onChooseRadius()
        advanceUntilIdle()

        assertEquals(JobRadiusPromptUiState.Hidden, vm.uiState.value)
        coVerify { appSettings.markJobRadiusPromptAnswered("emp-1") }
    }
}
