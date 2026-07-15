package cz.cleansia.partner.core.auth

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import cz.cleansia.core.auth.SessionScopedCache
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.rules.TestName

/**
 * Pins the [SessionScopedCache] contract of [UserProfileStore]: the persisted
 * identity (including the employeeId later reused as a login seed) must not
 * survive a `clear()` issued through the multibinding — the path both the
 * voluntary sign-out and the authenticator-forced sign-out use.
 */
class UserProfileStoreTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    @get:Rule
    val testName = TestName()

    private lateinit var dataStore: DataStore<Preferences>

    private val profile = UserProfileData(
        userId = "user-1",
        email = "jana@example.com",
        employeeId = "emp-1",
        isEmailConfirmed = true,
        hasAdminAccess = false,
        firstName = "Jana",
        lastName = "Nováková",
        role = "Employee",
    )

    @Before
    fun setUp() {
        dataStore = PreferenceDataStoreFactory.create(
            produceFile = { tempFolder.newFile("user_profile_${testName.methodName}.preferences_pb") },
        )
    }

    @Test
    fun save_thenCurrent_roundTripsTheProfile() = runTest {
        val store = UserProfileStore(dataStore)

        store.save(profile)

        assertEquals(profile, store.current())
    }

    @Test
    fun clear_viaSessionScopedCache_wipesThePersistedProfile() = runTest {
        val store = UserProfileStore(dataStore)
        store.save(profile)

        val cache: SessionScopedCache = store
        cache.clear()

        assertNull(store.current())
    }
}
