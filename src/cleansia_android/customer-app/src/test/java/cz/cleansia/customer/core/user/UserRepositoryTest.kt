package cz.cleansia.customer.core.user

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.client.UserApi
import cz.cleansia.customer.api.model.MyProfileDto
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.core.auth.ForcedSignOutReason
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.mockkObject
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
    private lateinit var addressRepository: AddressRepository
    private lateinit var orderRepository: OrderRepository
    private lateinit var disputeRepository: DisputeRepository
    private lateinit var loyaltyRepository: LoyaltyRepository
    private lateinit var referralRepository: ReferralRepository
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
        addressRepository = mockk(relaxed = true)
        orderRepository = mockk(relaxed = true)
        disputeRepository = mockk(relaxed = true)
        loyaltyRepository = mockk(relaxed = true)
        referralRepository = mockk(relaxed = true)
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

    private fun newRepo() = UserRepository(
        userApi = userApi,
        gdprApi = gdprApi,
        tokenStore = tokenStore,
        sessionManager = sessionManager,
        addressRepository = addressRepository,
        orderRepository = orderRepository,
        disputeRepository = disputeRepository,
        loyaltyRepository = loyaltyRepository,
        referralRepository = referralRepository,
        appContext = appContext,
    )

    private fun profile() = MyProfileDto(
        email = "a@b.com",
        firstName = "Ann",
        lastName = "Brown",
        phoneNumber = "+420123456789",
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

    // ── deleteAccount() ──

    @Test
    fun deleteAccount_givenSuccess_clearsCachesAndEmitsForcedSignOut() = runTest {
        coEvery { gdprApi.gdprDeleteMyAccount() } returns Response.success(Unit)

        val result = newRepo().deleteAccount()

        assertTrue("expected Success but got: $result", result is ApiResult.Success)
        coVerify { orderRepository.clear() }
        coVerify { addressRepository.clear() }
        coVerify { disputeRepository.clear() }
        coVerify { loyaltyRepository.clear() }
        coVerify { referralRepository.clear() }
        verify { tokenStore.clear() }
        verify { sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated) }
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
