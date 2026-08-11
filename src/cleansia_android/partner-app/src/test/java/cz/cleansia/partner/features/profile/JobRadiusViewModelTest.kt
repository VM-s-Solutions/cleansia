package cz.cleansia.partner.features.profile

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The radius narrows the "new jobs near you" digest and nothing else, so the only thing this VM can
 * get materially wrong is the wire value: a cleared preference has to reach the server as "no
 * radius", never as a zero-kilometre one, which would be a radius that matches nothing.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class JobRadiusViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    @Before
    fun setUp() {
        repository = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_profile_not_loaded) } returns "profile not loaded"
    }

    private fun viewModel() = JobRadiusViewModel(repository, errorTranslator, snackbar, appContext)

    private fun loaded(radiusKm: Int?) {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(EmployeeItem(id = "emp-1", jobRadiusKm = radiusKm))
    }

    @Test
    fun `load transitions Loading to Loaded with the limit off when no radius is set`() = runTest {
        loaded(null)

        val vm = viewModel()
        assertEquals(JobRadiusUiState.Loading, vm.uiState.value)
        advanceUntilIdle()

        val form = (vm.uiState.value as JobRadiusUiState.Loaded).form
        assertEquals("emp-1", form.employeeId)
        assertEquals(false, form.limitEnabled)
        assertEquals(JobRadius.DEFAULT_KM, form.radiusKm)
    }

    @Test
    fun `load seeds the slider from the saved radius`() = runTest {
        loaded(40)

        val vm = viewModel()
        advanceUntilIdle()

        val form = (vm.uiState.value as JobRadiusUiState.Loaded).form
        assertEquals(true, form.limitEnabled)
        assertEquals(40, form.radiusKm)
    }

    @Test
    fun `a failed load lands on Error and tells the cleaner`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(JobRadiusUiState.Error, vm.uiState.value)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `retry re-runs the load`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Error(ApiError.Network("offline"))
        val vm = viewModel()
        advanceUntilIdle()

        loaded(10)
        vm.retry()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is JobRadiusUiState.Loaded)
    }

    @Test
    fun `saving a radius sends the chosen kilometres and reports success once`() = runTest {
        loaded(null)
        coEvery { repository.updateJobRadius(any(), any()) } returns ApiResult.Success(Unit)
        val vm = viewModel()
        advanceUntilIdle()

        vm.onLimitEnabledChange(true)
        vm.onRadiusChange(35)
        vm.save()
        advanceUntilIdle()

        coVerify { repository.updateJobRadius(employeeId = "emp-1", radiusKm = 35) }
        assertEquals(ActionState.Idle, vm.saveState.value)
    }

    /** The whole point of the toggle: "anywhere" is a null radius, not a zero-kilometre one. */
    @Test
    fun `turning the limit off clears the radius with null rather than zero`() = runTest {
        loaded(40)
        coEvery { repository.updateJobRadius(any(), any()) } returns ApiResult.Success(Unit)
        val vm = viewModel()
        advanceUntilIdle()

        vm.onLimitEnabledChange(false)
        vm.save()
        advanceUntilIdle()

        coVerify { repository.updateJobRadius(employeeId = "emp-1", radiusKm = null) }
    }

    @Test
    fun `turning the limit off keeps the slider position for when it is turned back on`() = runTest {
        loaded(40)
        val vm = viewModel()
        advanceUntilIdle()

        vm.onLimitEnabledChange(false)
        vm.onLimitEnabledChange(true)

        assertEquals(40, (vm.uiState.value as JobRadiusUiState.Loaded).form.radiusKm)
    }

    @Test
    fun `a rejected save surfaces the backend key and leaves the action idle`() = runTest {
        loaded(null)
        coEvery { repository.updateJobRadius(any(), any()) } returns ApiResult.Error(
            ApiError.BadRequest(
                message = "A validation problem occurred.",
                code = null,
                validationErrors = mapOf("RadiusKm" to listOf("employee.job_radius_out_of_range")),
                errorKey = "employee.job_radius_out_of_range",
            ),
        )
        every { errorTranslator.translate(any()) } returns "Choose between 1 and 500 km."
        val vm = viewModel()
        advanceUntilIdle()

        vm.onLimitEnabledChange(true)
        vm.save()
        advanceUntilIdle()

        verify { snackbar.showError("Choose between 1 and 500 km.") }
        assertEquals(ActionState.Idle, vm.saveState.value)
    }

    /**
     * E1a — the id survives a failed reload, so a save from a non-Loaded state would write the stale
     * form over a profile we could not read.
     */
    @Test
    fun `save refuses to run when the load failed`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Error(ApiError.Network("offline"))
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        coVerify(exactly = 0) { repository.updateJobRadius(any(), any()) }
    }

    @Test
    fun `save refuses to run when the employee id never arrived`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(EmployeeItem(id = null, jobRadiusKm = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        coVerify(exactly = 0) { repository.updateJobRadius(any(), any()) }
        verify { snackbar.showError("profile not loaded") }
    }

    @Test
    fun `the form never sends a value outside the bounds the server accepts`() {
        assertEquals(
            JobRadius.MAX_KM,
            JobRadiusForm(limitEnabled = true, radiusKm = JobRadius.MAX_KM + 250).wireRadiusKm,
        )
        assertEquals(
            JobRadius.MIN_KM,
            JobRadiusForm(limitEnabled = true, radiusKm = 0).wireRadiusKm,
        )
        assertNull(JobRadiusForm(limitEnabled = false, radiusKm = 40).wireRadiusKm)
    }

    /** Mirrors `JobProximity.MinRadiusKm` / `MaxRadiusKm`; a drift here silently 400s every save. */
    @Test
    fun `the bounds match the server's`() {
        assertEquals(1, JobRadius.MIN_KM)
        assertEquals(500, JobRadius.MAX_KM)
    }
}
