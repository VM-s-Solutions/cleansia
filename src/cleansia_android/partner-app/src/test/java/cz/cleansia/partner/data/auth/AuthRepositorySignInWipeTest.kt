package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import io.mockk.coEvery
import io.mockk.coVerifyOrder
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * A read still in flight at sign-out can refill a cache after its wipe — a pending-offers answer landing
 * after the authenticator's clear(), say — and the partner app wiped nothing at sign-in, so the next
 * cleaner on the phone could be shown the last one's data. As in the customer app, both paths that mint
 * a session wipe every session-scoped cache before they save its tokens.
 */
class AuthRepositorySignInWipeTest {

    private val authApi = mockk<AuthApi>()
    private val authenticatedAuthApi = mockk<AuthApi>()
    private val employeeApi = mockk<EmployeeApi>()
    private val tokenStore = mockk<TokenStore>(relaxed = true)
    private val pushTokenRepository = mockk<PushTokenRepository>(relaxed = true)
    private val cache = mockk<SessionScopedCache>(relaxed = true)

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private var storedProfile: UserProfileData? = null
    private val userProfileStore = mockk<UserProfileStore>().also { store ->
        coEvery { store.current() } answers { storedProfile }
        coEvery { store.save(any()) } answers { storedProfile = firstArg() }
    }

    /** The profile store is one of the partner app's session caches; this stands in for its binding. */
    private val profileCache = object : SessionScopedCache {
        override suspend fun clear() {
            storedProfile = null
        }
    }

    private fun newRepository(caches: Set<SessionScopedCache> = setOf(cache)) = AuthRepositoryImpl(
        authApi = authApi,
        authenticatedAuthApi = { authenticatedAuthApi },
        employeeApi = employeeApi,
        tokenStore = tokenStore,
        userProfileStore = userProfileStore,
        json = json,
        pushTokenRepository = pushTokenRepository,
        sessionScopedCaches = { caches },
    )

    private fun session() = Response.success(
        JwtTokenResponse(
            token = "header.eyJleHAiOjQxMDI0NDQ4MDB9.sig",
            isEmailConfirmed = true,
            hasAdminAccess = false,
            userId = "user-new",
            email = "next@cleansia.cz",
            refreshToken = "refresh-new",
            role = "Employee",
        ),
    )

    private fun employeeReadFails() {
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns Response.error(
            500,
            "boom".toResponseBody("text/plain".toMediaType()),
        )
    }

    @Test
    fun login_wipesEverySessionCacheBeforeSavingTheNewTokens() = runTest {
        coEvery { authApi.authLogin(any()) } returns session()
        employeeReadFails()

        val outcome = newRepository().login("next@cleansia.cz", "password")

        assertTrue(outcome is cz.cleansia.core.network.ApiResult.Success)
        coVerifyOrder {
            cache.clear()
            tokenStore.save(any())
        }
    }

    @Test
    fun confirmEmail_wipesEverySessionCacheBeforeSavingTheNewTokens() = runTest {
        coEvery { authApi.authConfirmUserEmail(any()) } returns session()
        employeeReadFails()

        val outcome = newRepository().confirmEmail("next@cleansia.cz", "123456", "Heslo2026")

        assertTrue(outcome is cz.cleansia.core.network.ApiResult.Success)
        coVerifyOrder {
            cache.clear()
            tokenStore.save(any())
        }
    }

    /** The profile store merges into what it holds, so an unwiped one hands the last cleaner's id on. */
    @Test
    fun login_doesNotCarryThePreviousCleanersEmployeeIdIntoTheNewSession() = runTest {
        storedProfile = UserProfileData(
            userId = "user-old",
            email = "previous@cleansia.cz",
            employeeId = "emp-old",
            isEmailConfirmed = true,
            hasAdminAccess = false,
            firstName = "Petra",
            lastName = "Dvořáková",
            role = "Employee",
        )
        coEvery { authApi.authLogin(any()) } returns session()
        employeeReadFails()

        newRepository(caches = setOf(cache, profileCache)).login("next@cleansia.cz", "password")

        assertEquals("user-new", storedProfile?.userId)
        assertNull("the previous cleaner's employee id reached the new session", storedProfile?.employeeId)
        assertNull("the previous cleaner's name reached the new session", storedProfile?.firstName)
    }
}
