package cz.cleansia.core.notifications

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import cz.cleansia.core.auth.DeviceIdProvider
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.rules.TestName

/**
 * Characterization tests for [PushTokenRepository]'s two network paths —
 * [PushTokenRepository.ensureRegistered] and
 * [PushTokenRepository.unregisterDevice].
 *
 * Both are deliberately-silent background calls (the consumer is
 * [PushTokenSessionObserver]/logout, not a ViewModel). The observable contract
 * pinned here:
 *  - ensureRegistered writes the "last registered" token ONLY on success, so a
 *    second call with the same token short-circuits without hitting the client;
 *    a failed call leaves the cache untouched so the next call retries.
 *  - unregisterDevice always clears the local token, regardless of outcome.
 *
 * Each test uses a fresh on-disk DataStore in a per-test temp folder, plus a
 * token unique to its method name, so nothing leaks across tests. Every other
 * collaborator is a MockK fake.
 */
class PushTokenRepositoryTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    @get:Rule
    val testName = TestName()

    private lateinit var client: DeviceRegistrationClient
    private lateinit var deviceIdProvider: DeviceIdProvider
    private lateinit var dataStore: DataStore<Preferences>

    private val deviceId = "device-123"

    private val token: String get() = "token-${testName.methodName}"

    @Before
    fun setUp() {
        client = mockk()
        deviceIdProvider = mockk()
        every { deviceIdProvider.deviceId } returns deviceId

        dataStore = PreferenceDataStoreFactory.create(
            produceFile = { tempFolder.newFile("push_token_${testName.methodName}.preferences_pb") },
        )
    }

    private fun newRepo() = PushTokenRepository(client, dataStore, deviceIdProvider)

    // --- ensureRegistered() ---

    @Test
    fun ensureRegistered_givenSuccess_cachesTokenSoRepeatCallSkipsClient() = runTest {
        coEvery { client.register(any()) } returns true

        val repo = newRepo()
        repo.ensureRegistered(token)
        repo.ensureRegistered(token)

        coVerify(exactly = 1) { client.register(any()) }
    }

    @Test
    fun ensureRegistered_givenSuccess_sendsExpectedRequest() = runTest {
        coEvery { client.register(any()) } returns true

        newRepo().ensureRegistered(token)

        coVerify(exactly = 1) {
            client.register(
                RegisterDeviceRequest(deviceId = deviceId, deviceToken = token, platform = "android"),
            )
        }
    }

    @Test
    fun ensureRegistered_whenClientFails_doesNotCacheTokenSoNextCallRetries() = runTest {
        coEvery { client.register(any()) } returns false

        val repo = newRepo()
        repo.ensureRegistered(token)

        coEvery { client.register(any()) } returns true
        repo.ensureRegistered(token)

        coVerify(exactly = 2) { client.register(any()) }
    }

    @Test
    fun ensureRegistered_whenClientThrows_doesNotCacheTokenSoNextCallRetries() = runTest {
        coEvery { client.register(any()) } throws java.io.IOException("boom")

        val repo = newRepo()
        runCatching { repo.ensureRegistered(token) }

        coEvery { client.register(any()) } returns true
        repo.ensureRegistered(token)

        coVerify(exactly = 2) { client.register(any()) }
    }

    // --- unregisterDevice() ---

    @Test
    fun unregisterDevice_givenSuccess_clearsCachedToken() = runTest {
        coEvery { client.register(any()) } returns true
        coEvery { client.unregister(any()) } returns true

        val repo = newRepo()
        repo.ensureRegistered(token)
        repo.unregisterDevice()
        repo.ensureRegistered(token)

        coVerify(exactly = 1) { client.unregister(deviceId) }
        coVerify(exactly = 2) { client.register(any()) }
    }

    @Test
    fun unregisterDevice_whenClientFails_stillClearsCachedToken() = runTest {
        coEvery { client.register(any()) } returns true
        coEvery { client.unregister(any()) } returns false

        val repo = newRepo()
        repo.ensureRegistered(token)
        repo.unregisterDevice()
        repo.ensureRegistered(token)

        coVerify(exactly = 2) { client.register(any()) }
    }
}
