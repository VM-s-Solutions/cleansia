package cz.cleansia.partner.core.auth

import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.api.client.EmployeeApi
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Single source of truth for "which employee am I?".
 *
 * The JWT carries a *user* id; every partner surface that has to be scoped to
 * the signed-in cleaner (invoices, dashboard, the My-* order tabs, period pay)
 * needs the *employee* id, which only `GetCurrentEmployee` can supply. Login
 * hydrates it, and so does confirm-email since this fix — but installs created
 * before that shipped are already sitting on a persisted `null`, and a session
 * lasts up to 30 days, so they may never run either path again. Every read site
 * therefore goes through here: the id is fetched once, lazily, on first use and
 * persisted for the rest of the session.
 *
 * Deliberately holds NO id field of its own. [UserProfileStore] is a
 * [cz.cleansia.core.auth.SessionScopedCache] and is wiped on both sign-out
 * paths; a cache field on this singleton would survive that wipe and hand the
 * previous cleaner's employee id to the next user of a shared device. Reading
 * through the store on every call is what makes the sign-out wipe authoritative.
 *
 * The [Mutex] is concurrency control, not a cache: the dashboard, the orders
 * list and the invoices list all resolve within a few ms of app start, and
 * without it a null-id install would fire three identical `GetCurrentEmployee`
 * requests. The re-read inside the lock is what collapses them to one.
 */
@Singleton
class EmployeeIdResolver @Inject constructor(
    private val userProfileStore: UserProfileStore,
    private val employeeApi: EmployeeApi,
    private val json: Json,
) {

    private val fetchLock = Mutex()

    /**
     * The signed-in cleaner's employee id, fetching and persisting it if the
     * local profile does not have one yet.
     *
     * Returns null when there is no session, or when the fetch fails — callers
     * keep their existing "no employee id" branch (empty list / unscoped view /
     * error state) and simply get another chance on the next refresh. Nothing
     * is persisted on failure, so a transient outage cannot poison the profile.
     */
    suspend fun resolve(): String? {
        storedEmployeeId()?.let { return it }

        return fetchLock.withLock {
            // Re-read under the lock: a caller that queued behind an in-flight
            // fetch must see the id that fetch persisted, not fetch it again.
            storedEmployeeId() ?: fetchAndPersist()
        }
    }

    /**
     * Null both when there is no session (no profile row at all) and when the
     * session simply has not been hydrated yet — the two are told apart by
     * [fetchAndPersist], which refuses to call the API in the first case.
     */
    private suspend fun storedEmployeeId(): String? =
        userProfileStore.current()?.employeeId?.takeIf { it.isNotBlank() }

    private suspend fun fetchAndPersist(): String? {
        // No profile row means no session — the sign-out wipe cleared it, or we
        // never had one. Calling GetCurrentEmployee here would be a guaranteed
        // 401 on every scoped screen of the signed-out shell.
        userProfileStore.current() ?: return null

        val employee = safeApiCall(json) { employeeApi.employeeGetCurrentEmployee() }.getOrNull()
        val employeeId = employee?.id?.takeIf { it.isNotBlank() } ?: return null
        userProfileStore.updateEmployeeId(employeeId)
        return employeeId
    }
}
