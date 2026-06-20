package cz.cleansia.customer.core.devices

import cz.cleansia.core.auth.DeviceIdProvider
import cz.cleansia.core.network.ApiError
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Contract test for [DeviceManagementRepository] after the ApiResult migration.
 *
 * Pins the behaviour the legacy `T?`/sentinel + in-repo-snackbar form had,
 * now expressed through [ApiResult]:
 *  - success returns the body (no snackbar - the repo no longer touches UI);
 *  - an HTTP error response yields [ApiResult.Error] carrying the typed error
 *    (the ViewModel localizes it via ApiErrorParser + surfaces the single snackbar);
 *  - an infrastructure failure (IOException) yields a [ApiError.Network] the VM
 *    no-ops (NetworkErrorInterceptor already toasted it);
 *  - a `200 { success = false }` revoke is the same silent soft-failure the
 *    legacy `return false` (no snackbar) produced.
 */
class DeviceManagementRepositoryTest {

    private lateinit var api: DeviceManagementApi
    private lateinit var deviceIdProvider: DeviceIdProvider

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
    }

    @Before
    fun setUp() {
        api = mockk()
        deviceIdProvider = mockk()
        every { deviceIdProvider.deviceId } returns "device-current"
    }

    private fun newRepo() = DeviceManagementRepository(api, deviceIdProvider, json)

    private fun jsonBody(raw: String) = raw.toResponseBody("application/json".toMediaType())

    // -- getMyDevices() --

    @Test
    fun getMyDevices_givenSuccess_returnsBody() = runTest {
        val devices = listOf(UserDeviceDto(id = "row-1", platform = "android"))
        coEvery { api.mine("device-current") } returns Response.success(devices)

        val result = newRepo().getMyDevices()

        assertTrue(result.isSuccess)
        assertEquals(devices, result.getOrNull())
    }

    @Test
    fun getMyDevices_givenHttpError_carriesTypedErrorWithServerKey() = runTest {
        // A validation-shaped 400 - the error key is carried for the VM to localize,
        // exactly the key the legacy ApiErrorParser resolved from the same body.
        val body = """{"errors":{"Device":"device.not_found"}}"""
        coEvery { api.mine("device-current") } returns Response.error(400, jsonBody(body))

        val result = newRepo().getMyDevices()

        assertTrue(result.isError)
        val error = result.errorOrNull()
        assertTrue(error is ApiError.BadRequest)
        assertEquals("device.not_found", (error as ApiError.BadRequest).errorKey)
    }

    @Test
    fun getMyDevices_whenApiThrows_isSilentNetworkError() = runTest {
        coEvery { api.mine("device-current") } throws java.io.IOException("boom")

        val result = newRepo().getMyDevices()

        assertTrue(result.isError)
        assertTrue(result.errorOrNull() is ApiError.Network)
    }

    @Test
    fun getMyDevices_given200NullBody_returnsEmptySuccess() = runTest {
        // Legacy `resp.body().orEmpty()` rendered an absent/null 200 body as the
        // empty 'no devices' success state, not an error.
        coEvery { api.mine("device-current") } returns Response.success(null)

        val result = newRepo().getMyDevices()

        assertTrue(result.isSuccess)
        assertEquals(emptyList<UserDeviceDto>(), result.getOrNull())
    }

    // -- revoke() --

    @Test
    fun revoke_givenSuccessfulBody_returnsSuccessUnit() = runTest {
        coEvery { api.revoke("row-2") } returns Response.success(RevokeDeviceResponse(success = true))

        val result = newRepo().revoke("row-2")

        assertTrue(result.isSuccess)
        assertEquals(Unit, result.getOrNull())
    }

    @Test
    fun revoke_givenSuccessFalseBody_isSilentNetworkError() = runTest {
        coEvery { api.revoke("row-2") } returns Response.success(RevokeDeviceResponse(success = false))

        val result = newRepo().revoke("row-2")

        // Legacy returned false with no snackbar - preserved as the silent channel.
        assertTrue(result.isError)
        assertTrue(result.errorOrNull() is ApiError.Network)
    }

    @Test
    fun revoke_given200NullBody_returnsSuccessUnit() = runTest {
        // Legacy `resp.body()?.success ?: true` treated an absent/null 200 body
        // as success (no snackbar), so the migrated form must yield Success(Unit).
        coEvery { api.revoke("row-2") } returns Response.success(null)

        val result = newRepo().revoke("row-2")

        assertTrue(result.isSuccess)
        assertEquals(Unit, result.getOrNull())
    }

    @Test
    fun revoke_givenHttpError_carriesTypedError() = runTest {
        coEvery { api.revoke("row-2") } returns Response.error(400, jsonBody("{}"))

        val result = newRepo().revoke("row-2")

        assertTrue(result.isError)
        assertTrue(result.errorOrNull() is ApiError.BadRequest)
    }

    @Test
    fun revoke_whenApiThrows_isSilentNetworkError() = runTest {
        coEvery { api.revoke("row-2") } throws java.io.IOException("boom")

        val result = newRepo().revoke("row-2")

        assertTrue(result.isError)
        assertTrue(result.errorOrNull() is ApiError.Network)
    }
}
