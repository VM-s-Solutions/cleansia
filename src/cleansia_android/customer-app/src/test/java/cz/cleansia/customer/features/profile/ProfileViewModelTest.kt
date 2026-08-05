package cz.cleansia.customer.features.profile

import android.content.ContentResolver
import android.content.Context
import android.net.Uri
import cz.cleansia.core.media.Base64Image
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.mockkObject
import io.mockk.unmockkObject
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.After
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
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var settings: AppSettingsRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context
    private val currentUser = MutableStateFlow<CurrentUser?>(null)
    private val membership = MutableStateFlow<GetMyMembershipResponse?>(null)

    private val pickedUri = mockk<Uri>()
    private val encoded = Base64Image(base64 = "encoded", contentType = "image/jpeg", fileName = "me.jpg")

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
        membershipRepository = mockk(relaxed = true)
        settings = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { userRepository.currentUser } returns currentUser
        every { membershipRepository.current } returns membership
        every { appContext.contentResolver } returns mockk<ContentResolver>(relaxed = true)
        every { appContext.getString(R.string.profile_avatar_encode_failed) } returns "encode failed"

        // The bitmap pipeline needs a real Android runtime, so the shared compressor
        // is mocked and these tests cover what the VM does with its two outcomes.
        // `ImageCompressorMathTest` (:core) covers the pure sizing/orientation maths.
        mockkObject(ImageCompressor)
        coEvery { ImageCompressor.compressToBase64(any(), any(), any(), any()) } returns encoded
    }

    @After
    fun tearDown() {
        unmockkObject(ImageCompressor)
    }

    private fun viewModel() =
        ProfileViewModel(userRepository, membershipRepository, settings, snackbar, appContext)

    @Test
    fun `save and refresh start Idle`() = runTest {
        val vm = viewModel()
        assertEquals(ActionState.Idle, vm.saveState.value)
        assertEquals(ActionState.Idle, vm.refreshState.value)
    }

    @Test
    fun `refresh toggles refreshState Submitting then Idle`() = runTest {
        coEvery { userRepository.refreshCurrentUser() } returns ApiResult.Success(Unit)

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
            ApiResult.Success(Unit)
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
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

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
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Server(statusCode = 500, message = "save failed"))

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
    fun `saveProfile network failure sets ActionState Error but stays silent`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        assertTrue(vm.saveState.value is ActionState.Error)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `saveProfile is re-entry guarded`() = runTest {
        var calls = 0
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } coAnswers {
            calls++
            ApiResult.Success(Unit)
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
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        var completed = false
        val vm = viewModel()
        vm.completeOnboarding("+420123456789", null) { completed = true }
        advanceUntilIdle()

        assertTrue(completed)
        assertEquals(ActionState.Idle, vm.saveState.value)
        coVerify { settings.markOnboardingSeen("user-1") }
    }

    @Test
    fun `isPlus is false while the membership state is unknown`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertFalse(vm.isPlus.value)
    }

    @Test
    fun `isPlus stays false for a non-Plus user`() = runTest {
        membership.value = GetMyMembershipResponse(hasMembership = false)

        val vm = viewModel()
        advanceUntilIdle()

        assertFalse(vm.isPlus.value)
    }

    @Test
    fun `isPlus turns true when the membership cache reports an active Plus`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertFalse(vm.isPlus.value)

        membership.value = GetMyMembershipResponse(hasMembership = true)
        advanceUntilIdle()

        assertTrue(vm.isPlus.value)
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

    // ── avatar ──

    @Test
    fun `the avatar draft starts Unchanged`() = runTest {
        val vm = viewModel()

        assertEquals(AvatarDraft.Unchanged, vm.avatarDraft.value)
        assertEquals(ActionState.Idle, vm.avatarState.value)
    }

    @Test
    fun `pickAvatar compresses and holds the result as a preview draft`() = runTest {
        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        assertEquals(ActionState.Submitting, vm.avatarState.value)

        advanceUntilIdle()

        val draft = vm.avatarDraft.value as AvatarDraft.Picked
        assertEquals(pickedUri, draft.previewUri)
        assertEquals("encoded", draft.image.base64)
        assertEquals(ActionState.Idle, vm.avatarState.value)
    }

    @Test
    fun `pickAvatar uploads nothing on its own`() = runTest {
        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()

        coVerify(exactly = 0) {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        }
    }

    /**
     * An undecodable pick (corrupt file, HEIC below API 28) has to say something:
     * the pill has been spinning since the tap, so a silent reset reads as the app
     * ignoring the user.
     */
    @Test
    fun `pickAvatar on an undecodable image snackbars and leaves the draft alone`() = runTest {
        coEvery { ImageCompressor.compressToBase64(any(), any(), any(), any()) } returns null

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()

        verify { snackbar.showError("encode failed") }
        assertEquals(AvatarDraft.Unchanged, vm.avatarDraft.value)
        assertEquals(ActionState.Idle, vm.avatarState.value)
    }

    @Test
    fun `a second pick while one is compressing is dropped`() = runTest {
        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()

        coVerify(exactly = 1) { ImageCompressor.compressToBase64(any(), any(), any(), any()) }
    }

    @Test
    fun `saveProfile sends the picked image and does not ask for removal`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Ann",
                lastName = "Brown",
                phoneNumber = null,
                birthDate = null,
                languageCode = "en",
                photo = encoded,
                removePhoto = false,
            )
        }
    }

    @Test
    fun `removeAvatar makes the next save ask for removal without an image`() = runTest {
        currentUser.value = userWithPhoto
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.removeAvatar()
        assertEquals(AvatarDraft.Removed, vm.avatarDraft.value)

        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Ann",
                lastName = "Brown",
                phoneNumber = null,
                birthDate = null,
                languageCode = "en",
                photo = null,
                removePhoto = true,
            )
        }
    }

    /**
     * The `fe0c985b` regression, VM side: an ordinary name/phone save must say
     * nothing about the avatar. Goes red the moment `removePhoto` stops being
     * derived from the draft. `UserRepositoryTest` pins the same thing on the wire.
     */
    @Test
    fun `saveProfile with no photo action sends no image and does not ask for removal`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Ann",
                lastName = "Brown",
                phoneNumber = null,
                birthDate = null,
                languageCode = "en",
                photo = null,
                removePhoto = false,
            )
        }
    }

    @Test
    fun `completeOnboarding never touches the avatar`() = runTest {
        currentUser.value = sampleUser
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.completeOnboarding("+420123456789", null) {}
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = any(),
                lastName = any(),
                phoneNumber = any(),
                birthDate = any(),
                languageCode = any(),
                photo = null,
                removePhoto = false,
            )
        }
    }

    @Test
    fun `a successful save clears the draft so the next save is a no-op for the avatar`() = runTest {
        currentUser.value = userWithPhoto
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.removeAvatar()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        assertEquals(AvatarDraft.Unchanged, vm.avatarDraft.value)
    }

    @Test
    fun `a failed save keeps the draft so the user can retry`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        assertTrue(vm.avatarDraft.value is AvatarDraft.Picked)
    }

    @Test
    fun `a successful save of a picked photo confirms the upload and nothing else`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showSuccessKey(R.string.profile_avatar_upload_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_avatar_remove_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_save_success) }
    }

    @Test
    fun `a successful save of a removal confirms the removal and nothing else`() = runTest {
        currentUser.value = userWithPhoto
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.removeAvatar()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showSuccessKey(R.string.profile_avatar_remove_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_avatar_upload_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_save_success) }
    }

    @Test
    fun `a save that leaves the avatar alone confirms the profile save`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showSuccessKey(R.string.profile_save_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_avatar_upload_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_avatar_remove_success) }
    }

    /** A rejected save changed nothing, so there is nothing to confirm. */
    @Test
    fun `a failed save confirms nothing`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Server(statusCode = 500, message = "save failed"))

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showSuccessKey(any()) }
    }

    /** Nothing has reached the server until the user saves, so neither may confirm. */
    @Test
    fun `picking and removing confirm nothing on their own`() = runTest {
        currentUser.value = userWithPhoto

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()
        vm.removeAvatar()
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showSuccessKey(any()) }
    }

    /**
     * Remove is reachable over a pick that has never been uploaded — the options
     * sheet opens on the local preview. The server holds nothing to delete there,
     * so the tap can only mean "drop what I just chose".
     */
    @Test
    fun `removing with no saved photo discards the pick instead of recording a removal`() = runTest {
        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()

        vm.removeAvatar()

        assertEquals(AvatarDraft.Unchanged, vm.avatarDraft.value)
    }

    @Test
    fun `a save after discarding an unsaved pick asks for no removal and claims none`() = runTest {
        coEvery {
            userRepository.updateCurrentUser(any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()
        vm.removeAvatar()
        vm.saveProfile("Ann", "Brown", null, null, "en") {}
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Ann",
                lastName = "Brown",
                phoneNumber = null,
                birthDate = null,
                languageCode = "en",
                photo = null,
                removePhoto = false,
            )
        }
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.profile_save_success) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.profile_avatar_remove_success) }
    }

    @Test
    fun `discardAvatarDraft resets a pending pick`() = runTest {
        val vm = viewModel()
        vm.pickAvatar(pickedUri)
        advanceUntilIdle()

        vm.discardAvatarDraft()

        assertEquals(AvatarDraft.Unchanged, vm.avatarDraft.value)
    }

    // ── avatar load failure: refetch once, then let it fall back to initials ──

    private val userWithPhoto = sampleUser.copy(
        avatarFileName = "9f1c-guid",
        avatarUrl = "https://blob.example/9f1c-guid?sig=abc",
    )

    @Test
    fun `an avatar load failure refetches the profile for a fresh SAS`() = runTest {
        currentUser.value = userWithPhoto
        coEvery { userRepository.refreshCurrentUser() } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()

        coVerify(exactly = 1) { userRepository.refreshCurrentUser() }
    }

    @Test
    fun `repeated failures on the same photo refetch only once`() = runTest {
        currentUser.value = userWithPhoto
        coEvery { userRepository.refreshCurrentUser() } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()
        vm.onAvatarLoadFailed()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()

        coVerify(exactly = 1) { userRepository.refreshCurrentUser() }
    }

    /** A replacement upload mints a new blob name, so the new image gets its own retry. */
    @Test
    fun `a different photo gets its own retry budget`() = runTest {
        currentUser.value = userWithPhoto
        coEvery { userRepository.refreshCurrentUser() } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()

        currentUser.value = userWithPhoto.copy(avatarFileName = "a2b4-guid")
        vm.onAvatarLoadFailed()
        advanceUntilIdle()

        coVerify(exactly = 2) { userRepository.refreshCurrentUser() }
    }

    /** A long session can outlive more than one SAS, so a good load hands the budget back. */
    @Test
    fun `a successful load restores the retry budget`() = runTest {
        currentUser.value = userWithPhoto
        coEvery { userRepository.refreshCurrentUser() } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()
        vm.onAvatarLoadSucceeded()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()

        coVerify(exactly = 2) { userRepository.refreshCurrentUser() }
    }

    @Test
    fun `a load failure with no photo on the profile refetches nothing`() = runTest {
        currentUser.value = sampleUser

        val vm = viewModel()
        vm.onAvatarLoadFailed()
        advanceUntilIdle()

        coVerify(exactly = 0) { userRepository.refreshCurrentUser() }
    }
}
