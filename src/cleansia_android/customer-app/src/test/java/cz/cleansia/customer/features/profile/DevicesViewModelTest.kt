package cz.cleansia.customer.features.profile

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.devices.DeviceManagementRepository
import cz.cleansia.customer.core.devices.UserDeviceDto
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
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

    private lateinit var repository: DeviceManagementRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val serverMessage = "Server unavailable."

    private val thisDevice = UserDeviceDto(
        id = "row-1",
        platform = "android",
        deviceId = "device-current",
        lastActiveAt = "2026-06-10T08:00:00+00:00",
        isCurrent = true,
    )
    private val otherDevice = UserDeviceDto(
        id = "row-2",
        platform = "ios",
        deviceId = "device-other",
        lastActiveAt = "2026-06-01T08:00:00+00:00",
        isCurrent = false,
    )

    @Before
    fun setUp() {
        repository = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.devices_revoke_success) } returns "Device removed"
        every { appContext.getString(R.string.devices_revoke_retry_hint) } returns "retry hint"
        every { appContext.getString(R.string.error_generic_server) } returns serverMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns "unknown"
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun viewModel() = DevicesViewModel(repository, snackbar, appContext)

    @Test
    fun `load transitions Loading to Loaded with devices`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))

        val vm = viewModel()
        assertEquals(DevicesUiState.Loading, vm.state.value)

        advanceUntilIdle()
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice, otherDevice)), vm.state.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `load http error transitions to Error and surfaces single snackbar`() = runTest {
        coEvery { repository.getMyDevices() } returns
            ApiResult.Error(ApiError.Server(500, serverMessage))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(DevicesUiState.Error, vm.state.value)
        verify(exactly = 1) { snackbar.showError(serverMessage) }
    }

    @Test
    fun `load infrastructure error transitions to Error silently`() = runTest {
        coEvery { repository.getMyDevices() } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        // NetworkErrorInterceptor already toasted - the VM must not double-toast.
        assertEquals(DevicesUiState.Error, vm.state.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `revoke success removes the device, emits effect, returns to Idle`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        coEvery { repository.revoke("row-2") } returns ApiResult.Success(Unit)

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
    fun `revoke http error keeps the list, surfaces snackbar and ActionState Error`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        coEvery { repository.revoke("row-2") } returns ApiResult.Error(ApiError.Server(500, serverMessage))

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoke("row-2")
        advanceUntilIdle()

        assertTrue(vm.revokeState.value is ActionState.Error)
        assertEquals("retry hint", (vm.revokeState.value as ActionState.Error).message)
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice, otherDevice)), vm.state.value)
        verify(exactly = 1) { snackbar.showError(serverMessage) }
    }

    @Test
    fun `revoke infrastructure error keeps the list, ActionState Error, no snackbar`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        coEvery { repository.revoke("row-2") } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoke("row-2")
        advanceUntilIdle()

        assertTrue(vm.revokeState.value is ActionState.Error)
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice, otherDevice)), vm.state.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `revoke is re-entry guarded while submitting`() = runTest {
        coEvery { repository.getMyDevices() } returns ApiResult.Success(listOf(thisDevice, otherDevice))
        var revokeCalls = 0
        coEvery { repository.revoke("row-2") } coAnswers {
            revokeCalls++
            ApiResult.Success(Unit)
        }

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoke("row-2")
        vm.revoke("row-2")
        advanceUntilIdle()

        assertEquals(1, revokeCalls)
    }
}
