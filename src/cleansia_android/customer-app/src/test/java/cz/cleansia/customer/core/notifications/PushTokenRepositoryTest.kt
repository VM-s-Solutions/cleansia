package cz.cleansia.customer.core.notifications

import android.content.Context
import cz.cleansia.core.auth.DeviceIdProvider
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.rules.TestName
import retrofit2.Response

/**
 * Characterization + post-migration tests for [PushTokenRepository]'s two
 * network paths - [PushTokenRepository.ensureRegistered] and
 * [PushTokenRepository.unregisterDevice].
 *
 * Both are deliberately-silent background calls (no snackbar - the consumer is
 * [PushTokenSessionObserver]/logout, not a ViewModel). The observable contract
 * pinned here:
 *  - ensureRegistered writes the "last registered" token ONLY on success, so a
 *    second call with the same token short-circuits without hitting the API; a
 *    failed call leaves the cache untouched so the next call retries.
 *  - unregisterDevice always clears the local token, regardless of API outcome.
 *
 * The `preferencesDataStore` delegate is a process-wide singleton keyed by the
 * store name, so its persisted "last registered token" leaks across tests in
 * one JVM. Each test therefore uses a token unique to its method name so the
 * leaked value never collides with this test's token; the short-circuit under
 * test is driven only by writes made *within* the test. Every other
 * collaborator is a MockK fake.
 */
class PushTokenRepositoryTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    @get:Rule
    val testName = TestName()

    private lateinit var deviceApi: DeviceApi
    private lateinit var deviceIdProvider: DeviceIdProvider
    private lateinit var context: Context

    private val json = Json { ignoreUnknownKeys = true }
    private val deviceId = "device-123"

    // Unique per test so the process-singleton DataStore's leaked token from a
    // prior test never equals this test's token (which would short-circuit).
    private val token: String get() = "token-${testName.methodName}"

    @Before
    fun setUp() {
        deviceApi = mockk()
        deviceIdProvider = mockk()
        every { deviceIdProvider.deviceId } returns deviceId

        // Real DataStore needs a real filesDir; everything else is a fake.
        context = mockk(relaxed = true)
        every { context.applicationContext } returns context
        every { context.filesDir } returns tempFolder.root
    }

    private fun newRepo() = PushTokenRepository(deviceApi, deviceIdProvider, context, json)

    // --- ensureRegistered() ---

    @Test
    fun ensureRegistered_givenSuccess_cachesTokenSoRepeatCallSkipsApi() = kotlinx.coroutines.test.runTest {
        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))

        val repo = newRepo()
        repo.ensureRegistered(token)
        // Second call with the same token must short-circuit on the cached value.
        repo.ensureRegistered(token)

        coVerify(exactly = 1) { deviceApi.register(any()) }
    }

    @Test
    fun ensureRegistered_givenSuccess_sendsExpectedRequest() = kotlinx.coroutines.test.runTest {
        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))

        newRepo().ensureRegistered(token)

        coVerify(exactly = 1) {
            deviceApi.register(
                RegisterDeviceRequest(deviceId = deviceId, deviceToken = token, platform = "android"),
            )
        }
    }

    @Test
    fun ensureRegistered_whenApiThrows_doesNotCacheTokenSoNextCallRetries() = kotlinx.coroutines.test.runTest {
        coEvery { deviceApi.register(any()) } throws java.io.IOException("boom")

        val repo = newRepo()
        repo.ensureRegistered(token)

        // Failure must not persist the token - the next call retries the API.
        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))
        repo.ensureRegistered(token)

        coVerify(exactly = 2) { deviceApi.register(any()) }
    }

    @Test
    fun ensureRegistered_givenHttpError_doesNotCacheTokenSoNextCallRetries() = kotlinx.coroutines.test.runTest {
        val errBody = "{}".toResponseBody("application/json".toMediaType())
        coEvery { deviceApi.register(any()) } returns Response.error(500, errBody)

        val repo = newRepo()
        repo.ensureRegistered(token)

        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))
        repo.ensureRegistered(token)

        coVerify(exactly = 2) { deviceApi.register(any()) }
    }

    // --- unregisterDevice() ---

    @Test
    fun unregisterDevice_givenSuccess_clearsCachedToken() = kotlinx.coroutines.test.runTest {
        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))
        coEvery { deviceApi.unregister(any()) } returns Response.success(UnregisterDeviceResponse(success = true))

        val repo = newRepo()
        repo.ensureRegistered(token)
        repo.unregisterDevice()

        // After unregister the cached token is gone - re-registering the same
        // token hits the API again instead of short-circuiting.
        repo.ensureRegistered(token)

        coVerify(exactly = 1) { deviceApi.unregister(deviceId) }
        coVerify(exactly = 2) { deviceApi.register(any()) }
    }

    @Test
    fun unregisterDevice_whenApiThrows_stillClearsCachedToken() = kotlinx.coroutines.test.runTest {
        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))
        coEvery { deviceApi.unregister(any()) } throws java.io.IOException("boom")

        val repo = newRepo()
        repo.ensureRegistered(token)
        repo.unregisterDevice()

        // Local cleanup happens regardless of the API outcome.
        coEvery { deviceApi.register(any()) } returns Response.success(RegisterDeviceResponse(deviceId = deviceId))
        repo.ensureRegistered(token)

        coVerify(exactly = 2) { deviceApi.register(any()) }
    }
}
