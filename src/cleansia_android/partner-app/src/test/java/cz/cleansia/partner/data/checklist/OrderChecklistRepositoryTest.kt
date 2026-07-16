package cz.cleansia.partner.data.checklist

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import cz.cleansia.core.auth.SessionScopedCache
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.rules.TestName

/**
 * Pins the [SessionScopedCache] contract of [OrderChecklistRepository]: the
 * per-order checklist is a persistent DataStore that previously only had a
 * per-order clear(orderId). Without the clear-all, a shared device carried the
 * prior account's checked items into the next account's order screens. clear()
 * must wipe every order's set.
 */
class OrderChecklistRepositoryTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    @get:Rule
    val testName = TestName()

    private lateinit var dataStore: DataStore<Preferences>

    @Before
    fun setUp() {
        dataStore = PreferenceDataStoreFactory.create(
            produceFile = { tempFolder.newFile("checklists_${testName.methodName}.preferences_pb") },
        )
    }

    @Test
    fun setChecked_thenObserve_roundTripsPerOrder() = runTest {
        val repo = OrderChecklistRepository(dataStore)

        repo.setChecked("order-1", "svc-a", checked = true)
        repo.setChecked("order-2", "svc-b", checked = true)

        assertEquals(setOf("svc-a"), repo.observeChecked("order-1").first())
        assertEquals(setOf("svc-b"), repo.observeChecked("order-2").first())
    }

    @Test
    fun clear_viaSessionScopedCache_wipesEveryOrdersCheckedState() = runTest {
        val repo = OrderChecklistRepository(dataStore)
        repo.setChecked("order-1", "svc-a", checked = true)
        repo.setChecked("order-2", "svc-b", checked = true)

        val cache: SessionScopedCache = repo
        cache.clear()

        assertEquals(emptySet<String>(), repo.observeChecked("order-1").first())
        assertEquals(emptySet<String>(), repo.observeChecked("order-2").first())
    }
}
