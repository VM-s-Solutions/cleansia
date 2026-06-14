package cz.cleansia.customer.features.profile

import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ProfileViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var userRepository: UserRepository
    private lateinit var settings: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private val currentUser = MutableStateFlow<CurrentUser?>(null)

    private val sampleUser = CurrentUser(
        id = "user-1",
        email = "a@b.com",
        firstName = "Ann",
        lastName = "Brown",
        phoneNumber = null,
        birthDate = null,
        preferredLanguageCode = "en",
    )

    @Before
    fun setUp() {
        userRepository = mockk(relaxed = true)
        settings = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        every { userRepository.currentUser } returns currentUser
    }

    private fun viewModel() = ProfileViewModel(userRepository, settings, snackbar)

    @Test
    fun `save and refresh start Idle`() = runTest {
        val vm = viewModel()
        assertEquals(ActionState.Idle, vm.saveState.value)
        assertEquals(ActionState.Idle, vm.refreshState.value)
    }

    @Test
    fun `refresh toggles refreshState Submitting then Idle`() = runTest {
        coEvery { userRepository.refreshCurrentUser() } returns null

        val vm = viewModel()
        vm.refresh()
        assertEquals(ActionState.Submitting, vm.refreshState.value)

        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.refreshState.value)
    }

    @Test
    fun `refresh is re-entry guarded`() = runTest {
        var calls = 0
        coEvery { userRepository.refreshCurrentUser() } coAnswers {
            calls++
            null
        }

        val vm = viewModel()
        vm.refresh()
        vm.refresh()
        advanceUntilIdle()

        assertEquals(1, calls)
    }

    @Test
    fun `saveProfile success returns to Idle and calls onSaved`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any())
        } returns null

        var saved = false
        val vm = viewModel()
        vm.saveProfile("Ann", "Brown", null, null, "en") { saved = true }
        advanceUntilIdle()

        assertTrue(saved)
        assertEquals(ActionState.Idle, vm.saveState.value)
    }

    @Test
    fun `saveProfile failure surfaces ActionState Error, snackbars, no onSaved`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any())
        } returns "save failed"

        var saved = false
        val vm = viewModel()
        vm.saveProfile("Ann", "Brown", null, null, "en") { saved = true }
        advanceUntilIdle()

        assertFalse(saved)
        assertTrue(vm.saveState.value is ActionState.Error)
        assertEquals("save failed", (vm.saveState.value as ActionState.Error).message)
        verify { snackbar.showError("save failed") }
    }

    @Test
    fun `saveProfile is re-entry guarded`() = runTest {
        var calls = 0
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any())
        } coAnswers {
            calls++
            null
        }

        val vm = viewModel()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        assertEquals(1, calls)
    }

    @Test
    fun `completeOnboarding success marks seen and returns to Idle`() = runTest {
        currentUser.value = sampleUser
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any())
        } returns null

        var completed = false
        val vm = viewModel()
        vm.completeOnboarding("+420123456789", null) { completed = true }
        advanceUntilIdle()

        assertTrue(completed)
        assertEquals(ActionState.Idle, vm.saveState.value)
        coVerify { settings.markOnboardingSeen("user-1") }
    }

    @Test
    fun `skipOnboarding marks seen and runs callback`() = runTest {
        currentUser.value = sampleUser

        var skipped = false
        val vm = viewModel()
        vm.skipOnboarding { skipped = true }
        advanceUntilIdle()

        assertTrue(skipped)
        coVerify { settings.markOnboardingSeen("user-1") }
    }
}
