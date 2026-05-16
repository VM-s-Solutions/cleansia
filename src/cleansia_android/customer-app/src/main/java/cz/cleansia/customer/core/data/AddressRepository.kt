package cz.cleansia.customer.core.data
import cz.cleansia.core.auth.SessionScopedCache

import android.content.Context
import android.util.Log
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.networkCall
import cz.cleansia.customer.core.user.SavedAddressApi
import cz.cleansia.customer.core.user.SetDefaultSavedAddressCommand
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json

private val Context.addressStore by preferencesDataStore(name = "user_addresses")

/**
 * Persistent store of the user's saved addresses + currently-selected one.
 *
 * Shape in DataStore: two preference keys holding JSON blobs.
 *  - ADDRESSES: JSON array of [UserAddress]
 *  - SELECTED_ID: id of the address currently shown in the home top-bar / used
 *    as the default in booking. Null if user has not picked one yet.
 *
 * Source of truth:
 *  - Signed-in users → the backend; DataStore is a local cache refreshed via
 *    [refreshFromServer] (call on sign-in / app resume).
 *  - Guest users (no token) → DataStore is the only store; no network calls.
 *
 * Mutation methods return `null` on success or a localized error message on
 * failure. Failures also push a snackbar and leave the local cache untouched
 * so the UI stays consistent with the server.
 */
@Singleton
class AddressRepository @Inject constructor(
    private val api: SavedAddressApi,
    private val tokenStore: TokenStore,
    private val snackbar: SnackbarController,
    @ApplicationContext private val context: Context,
) : cz.cleansia.core.auth.SessionScopedCache {

    private object Keys {
        val ADDRESSES = stringPreferencesKey("addresses_json")
        val SELECTED_ID = stringPreferencesKey("selected_id")
    }

    private val json = Json { ignoreUnknownKeys = true }

    val addresses: Flow<List<UserAddress>> = context.addressStore.data.map { prefs ->
        val raw = prefs[Keys.ADDRESSES]
        if (raw.isNullOrBlank()) emptyList() else runCatching {
            json.decodeFromString<List<UserAddress>>(raw)
        }.onFailure { Log.w(TAG, "Failed to parse stored addresses", it) }
            .getOrDefault(emptyList())
    }

    val selectedId: Flow<String?> = context.addressStore.data.map { it[Keys.SELECTED_ID] }

    /**
     * Pulls the signed-in user's saved addresses from the backend and overwrites
     * the local cache. No-op (returns null) when the user is unauthenticated.
     *
     * @return null on success, localized error string on failure.
     */
    suspend fun refreshFromServer(): String? {
        if (tokenStore.current() == null) return null

        val response = networkCall { api.getMine() }
            ?: return context.getString(R.string.error_generic_network)

        if (!response.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
            snackbar.showError(msg)
            return msg
        }

        val mapped = response.body().orEmpty().map { it.toUserAddress() }
        writeCache(mapped)
        return null
    }

    /**
     * Create (when [UserAddress.serverId] is null) or update an address.
     *
     * Guests take a purely-local path and write straight to DataStore. Signed-in
     * users hit the backend; the local cache is only updated after a 2xx so a
     * failed mutation never leaves stale state on disk.
     *
     * When [setAsDefault] is true on a create, the server demotes every other
     * address belonging to the user — we trigger a full [refreshFromServer] to
     * pick that up rather than mirroring the invariant in two places.
     */
    suspend fun upsert(address: UserAddress, setAsDefault: Boolean = address.isDefault): String? {
        val isGuest = tokenStore.current() == null

        if (isGuest) {
            writeLocalUpsert(address)
            return null
        }

        return if (address.serverId == null) {
            createOnServer(address, setAsDefault)
        } else {
            updateOnServer(address)
        }
    }

    private suspend fun createOnServer(address: UserAddress, setAsDefault: Boolean): String? {
        // Wave 1 Finding 2 — refuse to submit without coordinates. The backend
        // expects non-nullable lat/lng and would otherwise bind nulls to 0.0.
        // Surface the same "move pin" copy the picker uses; mirrors how the
        // booking flow validates required fields.
        val command = address.toAddCommand(setAsDefault) ?: run {
            val msg = context.getString(R.string.address_picker_move_pin)
            snackbar.showError(msg)
            return msg
        }
        val response = networkCall { api.add(command) }
            ?: return context.getString(R.string.error_generic_network)

        if (!response.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
            snackbar.showError(msg)
            return msg
        }

        val dto = response.body() ?: return refreshFromServer()

        return if (setAsDefault) {
            // Server demoted the peers — refetch so the cache reflects it.
            refreshFromServer()
        } else {
            val mapped = dto.toUserAddress()
            mergeIntoCache(localIdToReplace = address.id, replacement = mapped)
            null
        }
    }

    private suspend fun updateOnServer(address: UserAddress): String? {
        val serverId = address.serverId ?: return null
        // Same coordinate guard as createOnServer — see Wave 1 Finding 2.
        val command = address.toUpdateCommand(serverId) ?: run {
            val msg = context.getString(R.string.address_picker_move_pin)
            snackbar.showError(msg)
            return msg
        }
        val response = networkCall { api.update(command) }
            ?: return context.getString(R.string.error_generic_network)

        if (!response.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
            snackbar.showError(msg)
            return msg
        }

        val mapped = response.body()?.toUserAddress() ?: address
        mergeIntoCache(localIdToReplace = address.id, replacement = mapped)
        return null
    }

    suspend fun delete(id: String): String? {
        val cached = currentList().firstOrNull { it.id == id }
        val isGuest = tokenStore.current() == null
        val serverId = cached?.serverId

        if (isGuest || serverId == null) {
            writeLocalDelete(id)
            return null
        }

        val response = networkCall { api.delete(serverId) }
            ?: return context.getString(R.string.error_generic_network)

        if (!response.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
            snackbar.showError(msg)
            return msg
        }

        writeLocalDelete(id)
        return null
    }

    suspend fun setDefault(id: String): String? {
        val cached = currentList().firstOrNull { it.id == id }
        val isGuest = tokenStore.current() == null
        val serverId = cached?.serverId

        if (isGuest || serverId == null) {
            writeLocalSetDefault(id)
            return null
        }

        val response = networkCall { api.setDefault(SetDefaultSavedAddressCommand(savedAddressId = serverId)) }
            ?: return context.getString(R.string.error_generic_network)

        if (!response.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
            snackbar.showError(msg)
            return msg
        }

        // The server demoted every other peer — refetch so the cache picks that
        // up in one go instead of duplicating the invariant here.
        return refreshFromServer()
    }

    suspend fun setSelected(id: String?) {
        context.addressStore.edit { prefs ->
            if (id == null) prefs.remove(Keys.SELECTED_ID) else prefs[Keys.SELECTED_ID] = id
        }
    }

    suspend fun rename(id: String, newLabel: String): String? {
        val cached = currentList().firstOrNull { it.id == id } ?: return null
        val isGuest = tokenStore.current() == null
        val serverId = cached.serverId

        if (isGuest || serverId == null) {
            writeLocalRename(id, newLabel)
            return null
        }

        val renamed = cached.copy(label = newLabel)
        // Coordinate guard — server rename round-trips the full update DTO.
        val command = renamed.toUpdateCommand(serverId) ?: run {
            val msg = context.getString(R.string.address_picker_move_pin)
            snackbar.showError(msg)
            return msg
        }
        val response = networkCall { api.update(command) }
            ?: return context.getString(R.string.error_generic_network)

        if (!response.isSuccessful) {
            val msg = ApiErrorParser.parseToUserMessage(context, response.errorBody(), response.code())
            snackbar.showError(msg)
            return msg
        }

        val mapped = response.body()?.toUserAddress() ?: renamed
        mergeIntoCache(localIdToReplace = id, replacement = mapped)
        return null
    }

    /** Wipe the local cache — called on sign-out so the next user doesn't inherit this one's addresses. */
    override suspend fun clear() {
        context.addressStore.edit { prefs ->
            prefs.remove(Keys.ADDRESSES)
            prefs.remove(Keys.SELECTED_ID)
        }
    }

    /* ───────── private cache helpers ───────── */

    private suspend fun currentList(): List<UserAddress> {
        var snapshot: List<UserAddress> = emptyList()
        context.addressStore.edit { p -> snapshot = p.readList() }
        return snapshot
    }

    private suspend fun writeCache(list: List<UserAddress>) {
        context.addressStore.edit { prefs ->
            prefs[Keys.ADDRESSES] = json.encodeToString(list)
        }
    }

    private suspend fun writeLocalUpsert(address: UserAddress) {
        context.addressStore.edit { prefs ->
            val current = prefs.readList()
            val filtered = current.filterNot { it.id == address.id }
            val demoted = if (address.isDefault) filtered.map { it.copy(isDefault = false) } else filtered
            val updated = demoted + address
            prefs[Keys.ADDRESSES] = json.encodeToString(updated)
        }
    }

    private suspend fun mergeIntoCache(localIdToReplace: String, replacement: UserAddress) {
        context.addressStore.edit { prefs ->
            val current = prefs.readList()
            val withoutOld = current.filterNot { it.id == localIdToReplace || it.id == replacement.id }
            val updated = withoutOld + replacement
            prefs[Keys.ADDRESSES] = json.encodeToString(updated)
        }
    }

    private suspend fun writeLocalDelete(id: String) {
        context.addressStore.edit { prefs ->
            val current = prefs.readList()
            val removed = current.firstOrNull { it.id == id }
            val updated = current.filterNot { it.id == id }

            // Promote the first remaining address if we just deleted the default.
            val finalList = if (removed?.isDefault == true && updated.isNotEmpty()) {
                updated.mapIndexed { idx, addr -> addr.copy(isDefault = idx == 0) }
            } else {
                updated
            }

            prefs[Keys.ADDRESSES] = json.encodeToString(finalList)
            if (prefs[Keys.SELECTED_ID] == id) prefs.remove(Keys.SELECTED_ID)
        }
    }

    private suspend fun writeLocalSetDefault(id: String) {
        context.addressStore.edit { prefs ->
            val current = prefs.readList()
            val updated = current.map { it.copy(isDefault = it.id == id) }
            prefs[Keys.ADDRESSES] = json.encodeToString(updated)
        }
    }

    private suspend fun writeLocalRename(id: String, newLabel: String) {
        context.addressStore.edit { prefs ->
            val current = prefs.readList()
            val updated = current.map {
                if (it.id == id) it.copy(label = newLabel) else it
            }
            prefs[Keys.ADDRESSES] = json.encodeToString(updated)
        }
    }

    private fun androidx.datastore.preferences.core.Preferences.readList(): List<UserAddress> {
        val raw = this[Keys.ADDRESSES] ?: return emptyList()
        return runCatching { json.decodeFromString<List<UserAddress>>(raw) }.getOrDefault(emptyList())
    }

    private companion object {
        const val TAG = "AddressRepository"
    }
}
