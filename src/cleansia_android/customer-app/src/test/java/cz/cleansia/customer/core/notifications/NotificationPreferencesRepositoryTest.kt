package cz.cleansia.customer.core.notifications

import cz.cleansia.core.network.ApiResult
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Behavior contract for NotificationPreferencesRepository.
 *
 * This repo is a silent failure path: it never surfaces a snackbar (it has no
 * SnackbarController). The migrated form returns ApiResult - success carries the
 * body and commits the snapshot; failure is ApiResult.Error which the VM keeps
 * silent. refresh() leaves the snapshot untouched on failure; update() applies an
 * optimistic snapshot and reverts on failure.
 *
 * Characterization -> migrate -> green: the same success/failure outcomes are
 * preserved across the T?/Boolean -> ApiResult migration; only the carrier changed.
 */
class NotificationPreferencesRepositoryTest {

    private lateinit var api: NotificationPreferencesApi

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        api = mockk()
    }

    private fun newRepo() = NotificationPreferencesRepository(api, json)

    private fun payload(promo: Boolean = false) = NotificationPreferencesPayload(promo = promo)

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    // refresh()

    @Test
    fun refresh_givenSuccess_returnsBodyAndUpdatesSnapshot() = runTest {
        val body = payload(promo = true)
        coEvery { api.getMine() } returns Response.success(body)

        val repo = newRepo()
        val result = repo.refresh()

        assertEquals(ApiResult.Success(body), result)
        assertEquals(body, repo.preferences.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_whenApiThrows_returnsErrorSilentlyAndKeepsSnapshot() = runTest {
        coEvery { api.getMine() } returns Response.success(payload(promo = true))
        val repo = newRepo()
        repo.refresh()
        assertNotNull(repo.preferences.value)

        coEvery { api.getMine() } throws java.io.IOException("boom")
        val result = repo.refresh()

        assertTrue("network failure must be ApiResult.Error", result is ApiResult.Error)
        assertEquals("snapshot must be preserved on refresh failure", payload(promo = true), repo.preferences.value)
        assertEquals(false, repo.loading.value)
    }

    @Test
    fun refresh_givenHttpError_returnsErrorAndKeepsSnapshot() = runTest {
        coEvery { api.getMine() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue(result is ApiResult.Error)
        assertEquals(null, repo.preferences.value)
        assertEquals(false, repo.loading.value)
    }

    // update()

    @Test
    fun update_givenSuccess_returnsBodyAndCommitsSnapshot() = runTest {
        val sent = payload(promo = true)
        coEvery { api.update(sent) } returns Response.success(sent)

        val repo = newRepo()
        val result = repo.update(sent)

        assertEquals(ApiResult.Success(sent), result)
        assertEquals(sent, repo.preferences.value)
    }

    @Test
    fun update_givenHttpError_returnsErrorAndRevertsSnapshot() = runTest {
        val previous = payload(promo = false)
        coEvery { api.getMine() } returns Response.success(previous)
        val repo = newRepo()
        repo.refresh()

        val attempted = payload(promo = true)
        coEvery { api.update(attempted) } returns Response.error(400, errorBody())
        val result = repo.update(attempted)

        assertTrue(result is ApiResult.Error)
        assertEquals("snapshot must revert to previous on update failure", previous, repo.preferences.value)
    }

    @Test
    fun update_whenApiThrows_returnsErrorAndRevertsSnapshot() = runTest {
        val previous = payload(promo = false)
        coEvery { api.getMine() } returns Response.success(previous)
        val repo = newRepo()
        repo.refresh()

        val attempted = payload(promo = true)
        coEvery { api.update(attempted) } throws java.io.IOException("boom")
        val result = repo.update(attempted)

        assertTrue(result is ApiResult.Error)
        assertEquals(previous, repo.preferences.value)
    }

    // clear() (SessionScopedCache)

    @Test
    fun clear_viaSessionScopedCache_wipesTheSnapshot() = runTest {
        coEvery { api.getMine() } returns Response.success(payload(promo = true))
        val repo = newRepo()
        repo.refresh()
        assertNotNull(repo.preferences.value)

        val cache: cz.cleansia.core.auth.SessionScopedCache = repo
        cache.clear()

        assertNull(repo.preferences.value)
        assertEquals(false, repo.loading.value)
    }
}
