package cz.cleansia.customer.features.profile

import android.content.Context
import app.cash.turbine.test
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
    }

    private fun viewModel() = DevicesViewModel(repository, snackbar, appContext)

    @Test
    fun `load transitions Loading to Loaded with devices`() = runTest {
        coEvery { repository.getMyDevices() } returns listOf(thisDevice, otherDevice)

        val vm = viewModel()
        assertEquals(DevicesUiState.Loading, vm.state.value)

        advanceUntilIdle()
        assertEquals(DevicesUiState.Loaded(listOf(thisDevice, otherDevice)), vm.state.value)
    }

    @Test
    fun `load failure transitions to Error (repo already snackbarred)`() = runTest {
        coEvery { repository.getMyDevices() } returns null

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(DevicesUiState.Error, vm.state.value)
    }

    @Test
    fun `revoke success removes the device, emits effect, returns to Idle`() = runTest {
        coEvery { repository.getMyDevices() } returns listOf(thisDevice, otherDevice)
        coEvery { repository.revoke("row-2") } returns true

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
        coEvery { repository.getMyDevices() } returns listOf(thisDevice, otherDevice)
        coEvery { repository.revoke("row-2") } returns false

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
        coEvery { repository.getMyDevices() } returns listOf(thisDevice, otherDevice)
        var revokeCalls = 0
        coEvery { repository.revoke("row-2") } coAnswers {
            revokeCalls++
            true
        }

        val vm = viewModel()
        advanceUntilIdle()

        vm.revoke("row-2")
        vm.revoke("row-2")
        advanceUntilIdle()

        assertEquals(1, revokeCalls)
    }
}
