package cz.cleansia.partner.data.checklist

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringSetPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import cz.cleansia.core.auth.SessionScopedCache
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Qualifier
import javax.inject.Singleton

private val Context.checklistDataStore by preferencesDataStore(
    name = "partner_order_checklists",
)

@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class OrderChecklistDataStore

@Module
@InstallIn(SingletonComponent::class)
object OrderChecklistDataStoreModule {

    @Provides
    @Singleton
    @OrderChecklistDataStore
    fun provideChecklistDataStore(
        @ApplicationContext context: Context,
    ): DataStore<Preferences> = context.checklistDataStore
}

/**
 * Local-only persistent state for the per-order cleaning checklist.
 *
 * The backend doesn't currently track per-task completion (there's no
 * OrderTaskCompletion table), so for v2 we keep the checked set
 * client-side. Trade-offs we're accepting:
 *
 *  - Lost on app uninstall / data wipe.
 *  - Not shared between two cleaners assigned to the same order (rare).
 *  - Not visible to admin / customer.
 *
 * If/when those become real problems we promote this to a backend
 * endpoint; the [Flow]-based API here will read straight through.
 *
 * Keyed by `orderId`. Values are an unordered `Set<String>` of "item
 * ids" — for services / packages we use the DTO id; for extras we use
 * the slug (since extras don't have a separate id on the DTO).
 */
@Singleton
class OrderChecklistRepository @Inject constructor(
    @OrderChecklistDataStore private val dataStore: DataStore<Preferences>,
) : SessionScopedCache {
    /** Reactive read of the checked ids for a single order. Emits `emptySet()` until the first edit. */
    fun observeChecked(orderId: String): Flow<Set<String>> {
        val key = stringSetPreferencesKey(orderId)
        return dataStore.data.map { it[key] ?: emptySet() }
    }

    suspend fun setChecked(orderId: String, itemId: String, checked: Boolean) {
        val key = stringSetPreferencesKey(orderId)
        dataStore.edit { prefs ->
            val current = prefs[key] ?: emptySet()
            prefs[key] = if (checked) current + itemId else current - itemId
        }
    }

    /** Clears all checked state for a single order — call this on Complete. */
    suspend fun clear(orderId: String) {
        val key = stringSetPreferencesKey(orderId)
        dataStore.edit { it.remove(key) }
    }

    /**
     * SessionScopedCache contract — wipes every order's checked set so a shared
     * device doesn't carry the prior account's per-order progress into the next
     * one. The checklist is local-only (no server row keyed by user), so a
     * sign-out is the only signal we get to drop it.
     */
    override suspend fun clear() {
        dataStore.edit { it.clear() }
    }
}
