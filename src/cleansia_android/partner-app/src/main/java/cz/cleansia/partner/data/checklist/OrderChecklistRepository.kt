package cz.cleansia.partner.data.checklist

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringSetPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.checklistDataStore by preferencesDataStore(
    name = "partner_order_checklists",
)

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
    @ApplicationContext private val context: Context,
) {
    /** Reactive read of the checked ids for a single order. Emits `emptySet()` until the first edit. */
    fun observeChecked(orderId: String): Flow<Set<String>> {
        val key = stringSetPreferencesKey(orderId)
        return context.checklistDataStore.data.map { it[key] ?: emptySet() }
    }

    suspend fun setChecked(orderId: String, itemId: String, checked: Boolean) {
        val key = stringSetPreferencesKey(orderId)
        context.checklistDataStore.edit { prefs ->
            val current = prefs[key] ?: emptySet()
            prefs[key] = if (checked) current + itemId else current - itemId
        }
    }

    /** Clears all checked state for a single order — call this on Complete. */
    suspend fun clear(orderId: String) {
        val key = stringSetPreferencesKey(orderId)
        context.checklistDataStore.edit { it.remove(key) }
    }
}
