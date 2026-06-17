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
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.customer.core.user.SavedAddressApi
import cz.cleansia.customer.core.user.SetDefaultSavedAddressCommand
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
 * Mutation methods return [ApiResult.Success] once the local cache reflects the
 * change and [ApiResult.Error] carrying the parsed message on failure (the
 * cache is left untouched so the UI stays consistent with the server). The
 * consuming ViewModel surfaces the snackbar; an [ApiError.Network] failure
 * stays silent (NetworkErrorInterceptor owns the infra toast).
 */
@Singleton
class AddressRepository @Inject constructor(
    private val api: SavedAddressApi,
    private val tokenStore: TokenStore,
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
     * the local cache. No-op ([ApiResult.Success]) when the user is unauthenticated.
     */
    suspend fun refreshFromServer(): ApiResult<Unit> {
        if (tokenStore.current() == null) return ApiResult.Success(Unit)

        val response = networkCall { api.getMine() } ?: return networkError()

        if (!response.isSuccessful) {
            return httpError(response.errorBody(), response.code())
        }

        val mapped = response.body().orEmpty().map { it.toUserAddress() }
        writeCache(mapped)
        return ApiResult.Success(Unit)
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
    suspend fun upsert(address: UserAddress, setAsDefault: Boolean = address.isDefault): ApiResult<Unit> {
        val isGuest = tokenStore.current() == null

        if (isGuest) {
            writeLocalUpsert(address)
            return ApiResult.Success(Unit)
        }

        return if (address.serverId == null) {
            createOnServer(address, setAsDefault)
        } else {
            updateOnServer(address)
        }
    }

    private suspend fun createOnServer(address: UserAddress, setAsDefault: Boolean): ApiResult<Unit> {
        // Wave 1 Finding 2 — refuse to submit without coordinates. The backend
        // expects non-nullable lat/lng and would otherwise bind nulls to 0.0.
        // Surface the same "move pin" copy the picker uses; mirrors how the
        // booking flow validates required fields.
        val command = address.toAddCommand(setAsDefault) ?: return movePinError()
        val response = networkCall { api.add(command) } ?: return networkError()

        if (!response.isSuccessful) {
            return httpError(response.errorBody(), response.code())
        }

        val dto = response.body() ?: return refreshFromServer()

        return if (setAsDefault) {
            // Server demoted the peers — refetch so the cache reflects it.
            refreshFromServer()
        } else {
            val mapped = dto.toUserAddress()
            mergeIntoCache(localIdToReplace = address.id, replacement = mapped)
            ApiResult.Success(Unit)
        }
    }

    private suspend fun updateOnServer(address: UserAddress): ApiResult<Unit> {
        val serverId = address.serverId ?: return ApiResult.Success(Unit)
        // Same coordinate guard as createOnServer — see Wave 1 Finding 2.
        val command = address.toUpdateCommand(serverId) ?: return movePinError()
        val response = networkCall { api.update(command) } ?: return networkError()

        if (!response.isSuccessful) {
            return httpError(response.errorBody(), response.code())
        }

        val mapped = response.body()?.toUserAddress() ?: address
        mergeIntoCache(localIdToReplace = address.id, replacement = mapped)
        return ApiResult.Success(Unit)
    }

    suspend fun delete(id: String): ApiResult<Unit> {
        val cached = currentList().firstOrNull { it.id == id }
        val isGuest = tokenStore.current() == null
        val serverId = cached?.serverId

        if (isGuest || serverId == null) {
            writeLocalDelete(id)
            return ApiResult.Success(Unit)
        }

        val response = networkCall { api.delete(serverId) } ?: return networkError()

        if (!response.isSuccessful) {
            return httpError(response.errorBody(), response.code())
        }

        writeLocalDelete(id)
        return ApiResult.Success(Unit)
    }

    suspend fun setDefault(id: String): ApiResult<Unit> {
        val cached = currentList().firstOrNull { it.id == id }
        val isGuest = tokenStore.current() == null
        val serverId = cached?.serverId

        if (isGuest || serverId == null) {
            writeLocalSetDefault(id)
            return ApiResult.Success(Unit)
        }

        val response = networkCall { api.setDefault(SetDefaultSavedAddressCommand(savedAddressId = serverId)) }
            ?: return networkError()

        if (!response.isSuccessful) {
            return httpError(response.errorBody(), response.code())
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

    suspend fun rename(id: String, newLabel: String): ApiResult<Unit> {
        val cached = currentList().firstOrNull { it.id == id } ?: return ApiResult.Success(Unit)
        val isGuest = tokenStore.current() == null
        val serverId = cached.serverId

        if (isGuest || serverId == null) {
            writeLocalRename(id, newLabel)
            return ApiResult.Success(Unit)
        }

        val renamed = cached.copy(label = newLabel)
        // Coordinate guard — server rename round-trips the full update DTO.
        val command = renamed.toUpdateCommand(serverId) ?: return movePinError()
        val response = networkCall { api.update(command) } ?: return networkError()

        if (!response.isSuccessful) {
            return httpError(response.errorBody(), response.code())
        }

        val mapped = response.body()?.toUserAddress() ?: renamed
        mergeIntoCache(localIdToReplace = id, replacement = mapped)
        return ApiResult.Success(Unit)
    }

    /** Wipe the local cache — called on sign-out so the next user doesn't inherit this one's addresses. */
    override suspend fun clear() {
        context.addressStore.edit { prefs ->
            prefs.remove(Keys.ADDRESSES)
            prefs.remove(Keys.SELECTED_ID)
        }
    }

    /* ───────── error builders ───────── */

    private fun networkError(): ApiResult<Unit> =
        ApiResult.Error(ApiError.Network(context.getString(R.string.error_generic_network)))

    private fun movePinError(): ApiResult<Unit> =
        ApiResult.Error(ApiError.BadRequest(context.getString(R.string.address_picker_move_pin)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Unit> {
        // Carry the message [ApiErrorParser] already resolved from the body so
        // the surfacing ViewModel shows the identical string. The 401 case folds
        // into the message-carrying [ApiError.Unknown] alongside the generic
        // fallback rather than the messageless Unauthorized object.
        val message = ApiErrorParser.parseToUserMessage(context, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
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
