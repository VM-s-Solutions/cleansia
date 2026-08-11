package cz.cleansia.customer.core.user

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.client.UserApi
import cz.cleansia.customer.api.model.BlobFileDto
import cz.cleansia.customer.api.model.MyProfileDto
import cz.cleansia.customer.api.model.UpdateCurrentUserCommand
import cz.cleansia.core.auth.ForcedSignOutReason
import cz.cleansia.core.media.Base64Image
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.mockkObject
import io.mockk.slot
import io.mockk.unmockkObject
import io.mockk.verify
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Characterization + post-migration contract tests for [UserRepository].
 *
 * Pins the observable repo behavior across the T-0197 migration from the legacy
 * `String?`-as-error form to the `ApiResult<Unit>` contract:
 *  - success returns [ApiResult.Success] and caches the user;
 *  - a transport failure (networkCall returns null) is the SILENT channel —
 *    [ApiResult.Error] carrying [ApiError.Network] (NetworkErrorInterceptor owns
 *    the infra toast, so the consuming ViewModel skips it — no double-toast);
 *  - an HTTP error returns [ApiResult.Error] carrying the SAME single message
 *    (built from [cz.cleansia.customer.core.auth.ApiErrorParser]) the VM now
 *    surfaces.
 *
 * The repo never held a SnackbarController — its consuming ViewModels
 * (ProfileViewModel, DeleteAccountViewModel) surface the snackbar. The
 * standalone `snackbar` mock asserts the repo never surfaces one itself.
 */
class UserRepositoryTest {

    private lateinit var userApi: UserApi
    private lateinit var gdprApi: GdprApi
    private lateinit var tokenStore: TokenStore
    private lateinit var sessionManager: SessionManager
    private lateinit var cacheA: SessionScopedCache
    private lateinit var cacheB: SessionScopedCache
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val serverMessage = "server"
    private val userId = "user-1"
    private val accessToken = "header.payload.signature"

    @Before
    fun setUp() {
        userApi = mockk()
        gdprApi = mockk()
        tokenStore = mockk(relaxed = true)
        sessionManager = mockk(relaxed = true)
        cacheA = mockk(relaxed = true)
        cacheB = mockk(relaxed = true)
        // Standalone mock the repo never receives — asserts the repo never
        // surfaces a snackbar (the snackbar lives in the consuming ViewModel).
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_generic_server) } returns serverMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns "unknown"
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0

        every { tokenStore.current() } returns TokenStore.Tokens(
            accessToken = accessToken,
            accessTokenExpiresAt = Long.MAX_VALUE,
            refreshToken = "r",
            refreshTokenExpiresAt = Long.MAX_VALUE,
        )

        mockkObject(JwtDecoder)
        every { JwtDecoder.extractUserId(accessToken) } returns userId
    }

    @After
    fun tearDown() {
        unmockkObject(JwtDecoder)
    }

    // The real multibinding includes UserRepository itself; model that by
    // seeding the provided set with the built repo alongside the two mock
    // caches, mirroring the deleteAccount path iterating the whole set.
    private fun newRepo(): UserRepository {
        lateinit var repo: UserRepository
        repo = UserRepository(
            userApi = userApi,
            gdprApi = gdprApi,
            tokenStore = tokenStore,
            sessionManager = sessionManager,
            sessionScopedCaches = { setOf(repo, cacheA, cacheB) },
            appContext = appContext,
        )
        return repo
    }

    private fun profile() = MyProfileDto(
        email = "a@b.com",
        firstName = "Ann",
        lastName = "Brown",
        phoneNumber = "+420123456789",
        totalBookings = 7,
        totalSavings = 4820.50,
        savingsCurrencyCode = "CZK",
    )

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    // ── refreshCurrentUser() ──

    @Test
    fun refreshCurrentUser_givenSuccess_returnsSuccessAndCachesUser() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())

        val repo = newRepo()
        val result = repo.refreshCurrentUser()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        assertEquals(userId, repo.currentUser.value?.id)
        assertEquals("a@b.com", repo.currentUser.value?.email)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun refreshCurrentUser_mapsProfileStatsOntoCurrentUser() = runTest {
        val memberSince = kotlinx.datetime.Instant.parse("2025-02-14T10:00:00Z")
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(
            profile().copy(
                memberSince = memberSince,
                totalBookings = 7,
                totalSavings = 320.0,
                savingsCurrencyCode = "CZK",
            ),
        )

        val repo = newRepo()
        repo.refreshCurrentUser()

        val user = repo.currentUser.value
        assertEquals(memberSince, user?.memberSince)
        assertEquals(7, user?.totalBookings)
        assertEquals(320.0, user?.totalSavings)
        assertEquals("CZK", user?.savingsCurrencyCode)
    }

    /**
     * This used to assert the defect: a profile with no stats cached as "0 bookings, 0 saved". Both
     * are `nullable: false`, so an absent one is a broken wire field and telling a customer they have
     * saved nothing is a claim about their money. `savingsCurrencyCode` is nullable by design and
     * still survives as null.
     */
    @Test
    fun refreshCurrentUser_givenNoStats_refusesTheProfileRatherThanCachingZeroSavings() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns
            Response.success(profile().copy(totalBookings = null, totalSavings = null))

        val repo = newRepo()
        val result = repo.refreshCurrentUser()

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        assertNull(repo.currentUser.value)
    }

    @Test
    fun refreshCurrentUser_keepsANullSavingsCurrencyWithoutRefusingTheProfile() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns
            Response.success(profile().copy(savingsCurrencyCode = null))

        val repo = newRepo()
        repo.refreshCurrentUser()

        val user = repo.currentUser.value
        assertNull(user?.savingsCurrencyCode)
        assertEquals(4820.50, user?.totalSavings)
    }

    @Test
    fun refreshCurrentUser_givenNoToken_returnsSilentNetworkError() = runTest {
        every { tokenStore.current() } returns null

        val result = newRepo().refreshCurrentUser()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Network)
        assertEquals(networkMessage, error.getUserMessage())
    }

    @Test
    fun refreshCurrentUser_whenApiThrows_returnsSilentNetworkError() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } throws java.io.IOException("boom")

        val repo = newRepo()
        val result = repo.refreshCurrentUser()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Network)
        assertEquals(networkMessage, error.getUserMessage())
        assertNull(repo.currentUser.value)
    }

    @Test
    fun refreshCurrentUser_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.error(500, errorBody())

        val result = newRepo().refreshCurrentUser()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── updateCurrentUser() ──

    @Test
    fun updateCurrentUser_givenSuccess_reFetchesAndReturnsSuccess() = runTest {
        // Prime the cache so updateCurrentUser has a user id to send.
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())
        val repo = newRepo()
        repo.refreshCurrentUser()

        coEvery { userApi.userUpdateCurrentUser(any()) } returns Response.success(
            cz.cleansia.customer.api.model.UpdateCurrentUserResponse(),
        )

        val result = repo.updateCurrentUser(
            firstName = "Ann",
            lastName = "Brown",
            phoneNumber = "+420123456789",
            birthDate = null,
            languageCode = "en",
        )

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
    }

    @Test
    fun updateCurrentUser_givenNoCachedUser_returnsSilentNetworkError() = runTest {
        val result = newRepo().updateCurrentUser(
            firstName = "Ann",
            lastName = "Brown",
            phoneNumber = null,
            birthDate = null,
            languageCode = null,
        )

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }

    @Test
    fun updateCurrentUser_givenHttpError_carriesParsedServerMessage() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())
        val repo = newRepo()
        repo.refreshCurrentUser()

        coEvery { userApi.userUpdateCurrentUser(any()) } returns Response.error(500, errorBody())

        val result = repo.updateCurrentUser(
            firstName = "Ann",
            lastName = "Brown",
            phoneNumber = null,
            birthDate = null,
            languageCode = null,
        )

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    // ── updateCurrentUser(): the avatar reaches the GENERATED command ──
    //
    // These assert the wire, not the repo signature. Every field on the generated
    // command defaults to null, so dropping a mapper line still compiles and every
    // test that stops at the repo boundary stays green — the value simply never
    // leaves the phone.

    /** Primes the cache (updateCurrentUser needs a user id) and captures the sent command. */
    private suspend fun captureUpdateCommand(
        photo: Base64Image? = null,
        removePhoto: Boolean = false,
    ): UpdateCurrentUserCommand {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())
        val repo = newRepo()
        repo.refreshCurrentUser()

        val sent = slot<UpdateCurrentUserCommand>()
        coEvery { userApi.userUpdateCurrentUser(capture(sent)) } returns Response.success(
            cz.cleansia.customer.api.model.UpdateCurrentUserResponse(),
        )

        repo.updateCurrentUser(
            firstName = "Ann",
            lastName = "Brown",
            phoneNumber = null,
            birthDate = null,
            languageCode = null,
            photo = photo,
            removePhoto = removePhoto,
        )
        return sent.captured
    }

    @Test
    fun updateCurrentUser_givenAPickedPhoto_putsTheEncodedImageOnTheGeneratedCommand() = runTest {
        val command = captureUpdateCommand(
            photo = Base64Image(base64 = "encoded", contentType = "image/jpeg", fileName = "me.jpg"),
        )

        assertEquals("encoded", command.photo?.base64Content)
        assertEquals("image/jpeg", command.photo?.contentType)
        assertEquals("me.jpg", command.photo?.fileName)
        assertEquals(false, command.removePhoto)
    }

    @Test
    fun updateCurrentUser_givenRemoval_putsRemovePhotoTrueAndNoImageOnTheGeneratedCommand() = runTest {
        val command = captureUpdateCommand(removePhoto = true)

        assertEquals(true, command.removePhoto)
        assertNull(command.photo)
    }

    /**
     * The `fe0c985b` regression: an ordinary name/phone save must say nothing about
     * the avatar. A null photo alone is not enough — `removePhoto` riding true (or
     * arriving null, which is what deleting the mapper line produces) is what
     * destroyed the image the user still had.
     */
    @Test
    fun updateCurrentUser_givenNoPhotoAction_sendsNoImageAndRemovePhotoFalse() = runTest {
        val command = captureUpdateCommand()

        assertNull(command.photo)
        assertEquals(false, command.removePhoto)
    }

    @Test
    fun refreshCurrentUser_mapsTheProfilePhotoNameAndSasOntoCurrentUser() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(
            profile().copy(
                profilePhoto = BlobFileDto(
                    fileName = "9f1c-guid",
                    blobUrl = "https://blob.example/9f1c-guid?sig=abc",
                ),
            ),
        )

        val repo = newRepo()
        repo.refreshCurrentUser()

        assertEquals("9f1c-guid", repo.currentUser.value?.avatarFileName)
        assertEquals("https://blob.example/9f1c-guid?sig=abc", repo.currentUser.value?.avatarUrl)
    }

    @Test
    fun refreshCurrentUser_givenNoProfilePhoto_leavesTheAvatarFieldsNull() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())

        val repo = newRepo()
        repo.refreshCurrentUser()

        assertNull(repo.currentUser.value?.avatarFileName)
        assertNull(repo.currentUser.value?.avatarUrl)
    }

    // ── clear() (SessionScopedCache) ──

    @Test
    fun clear_viaSessionScopedCache_nullsTheCachedUserSnapshot() = runTest {
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())
        val repo = newRepo()
        repo.refreshCurrentUser()

        val cache: SessionScopedCache = repo
        cache.clear()

        assertNull(repo.currentUser.value)
    }

    // ── deleteAccount() ──

    @Test
    fun deleteAccount_givenSuccess_clearsEverySessionScopedCacheAndEmitsForcedSignOut() = runTest {
        coEvery { gdprApi.gdprDeleteMyAccount() } returns Response.success(Unit)
        // Prime the snapshot so we can assert the repo's own clear() ran via
        // the multibinding iteration (it's a member of the injected set).
        coEvery { userApi.userGetCurrentUser(query = null) } returns Response.success(profile())
        val repo = newRepo()
        repo.refreshCurrentUser()
        assertEquals(userId, repo.currentUser.value?.id)

        val result = repo.deleteAccount()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        // Every member of the multibinding is wiped — the hand-list previously
        // missed Membership/Recurring/PushToken. cacheA/cacheB stand in for the
        // full set; the repo's own snapshot proves it cleared itself too.
        coVerify { cacheA.clear() }
        coVerify { cacheB.clear() }
        assertNull(repo.currentUser.value)
        verify { tokenStore.clear() }
        verify { sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated) }
    }

    @Test
    fun deleteAccount_whenApiThrows_doesNotClearSessionScopedCaches() = runTest {
        coEvery { gdprApi.gdprDeleteMyAccount() } throws java.io.IOException("boom")

        newRepo().deleteAccount()

        coVerify(exactly = 0) { cacheA.clear() }
        coVerify(exactly = 0) { cacheB.clear() }
    }

    @Test
    fun deleteAccount_whenApiThrows_returnsSilentNetworkErrorAndDoesNotSignOut() = runTest {
        coEvery { gdprApi.gdprDeleteMyAccount() } throws java.io.IOException("boom")

        val result = newRepo().deleteAccount()

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }

    @Test
    fun deleteAccount_givenHttpError_carriesParsedServerMessageAndDoesNotSignOut() = runTest {
        coEvery { gdprApi.gdprDeleteMyAccount() } returns Response.error(500, errorBody())

        val result = newRepo().deleteAccount()

        val error = (result as ApiResult.Error).error
        assertTrue(error is ApiError.Server)
        assertEquals(serverMessage, error.getUserMessage())
        verify(exactly = 0) { sessionManager.emitForcedSignOut(any()) }
    }
}
