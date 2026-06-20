package cz.cleansia.partner.features.devices

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.core.devices.DevicesRepository
import cz.cleansia.partner.core.devices.RevokeDeviceResponse
import cz.cleansia.partner.core.devices.UserDeviceDto
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class DevicesViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: DevicesRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: cz.cleansia.partner.core.network.ApiErrorTranslator
    private lateinit var appContext: Context

    private val thisDevice = UserDeviceDto(
        id = "row-1",
        platform = "android",
        deviceId = "device-current",
        lastActiveAt = "2026-06-10T08:00:00+00:00",
        isCurrent = true,
    )
    private val otherDevice = UserDeviceDto(
        id = "row-2",
        platform = "android",
        deviceId = "device-other",
        lastActiveAt = "2026-06-01T08:00:00+00:00",
        isCurrent = false,
    )

    @Before
    fun setUp() {
        repository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.devices_revoke_success) } returns "Device removed"
        every { appContext.getString(R.string.devices_revoke_retry_hint) } returns "retry hint"
    }

    private fun viewModel() = DevicesViewModel(repository, errorTranslator, snackbar, appContext)

    @Test
    fun `load transitions Loading to Loaded with devices`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))

        val vm = viewModel()
        assertEquals(DevicesUiState.Loading, vm.state.value)

        advanceUntilIdle()
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice, otherDevice)), vm.state.value)
    }

    @Test
    fun `load failure transitions to Error and snackbars`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(DevicesUiState.Error, vm.state.value)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `revoke success removes the device, emits effect, returns to Idle`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        coEvery { repository.revoke("row-2") } returns ApiResult.Success(RevokeDeviceResponse(success = true))

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoked.test {
            vm.revoke("row-2")
            advanceUntilIdle()
            assertEquals("row-2", awaitItem())
        }

        assertEquals(ActionState.Idle, vm.revokeState.value)
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice)), vm.state.value)
        verify { snackbar.showSuccess("Device removed") }
    }

    @Test
    fun `revoke failure keeps the list and surfaces ActionState Error`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        coEvery { repository.revoke("row-2") } returns
            ApiResult.Error(ApiError.BadRequest("nope", code = null, validationErrors = null, errorKey = "device.not_found"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoke("row-2")
        advanceUntilIdle()

        assertTrue(vm.revokeState.value is ActionState.Error)
        assertEquals("retry hint", (vm.revokeState.value as ActionState.Error).message)
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice, otherDevice)), vm.state.value)
    }

    @Test
    fun `revoke is re-entry guarded while submitting`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        var revokeCalls = 0
        coEvery { repository.revoke("row-2") } coAnswers {
            revokeCalls++
            ApiResult.Success(RevokeDeviceResponse(success = true))
        }

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoke("row-2")
        vm.revoke("row-2")
        advanceUntilIdle()

        assertEquals(1, revokeCalls)
    }
}
