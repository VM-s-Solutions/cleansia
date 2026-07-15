package cz.cleansia.customer.core.user
import cz.cleansia.core.auth.ForcedSignOutReason

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.client.UserApi
import cz.cleansia.customer.api.model.UpdateCurrentUserCommand
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Provider
import javax.inject.Singleton
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.datetime.LocalDate

@Singleton
class UserRepository @Inject constructor(
    private val userApi: UserApi,
    private val gdprApi: GdprApi,
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    // Late-bound via Provider: this repo is itself a SessionScopedCache member,
    // so eagerly injecting the Set it belongs to would be a self-referential
    // Dagger cycle. .get() resolves the fully-built set at delete time.
    private val sessionScopedCaches: Provider<Set<@JvmSuppressWildcards SessionScopedCache>>,
    @ApplicationContext private val appContext: Context,
) : SessionScopedCache {
    /**
     * Cached current-user snapshot. Screens observe this; call [refreshCurrentUser]
     * to trigger a fetch. Emits null while the first fetch is in flight and
     * after [deleteAccount]/sign-out.
     *
     * Foreground operations return [ApiResult.Success] on success and
     * [ApiResult.Error] carrying the parsed message on failure. The consuming
     * ViewModel surfaces the snackbar; an [ApiError.Network] failure stays silent
     * (NetworkErrorInterceptor owns the infra toast).
     */
    private val _currentUser = MutableStateFlow<CurrentUser?>(null)
    val currentUser: StateFlow<CurrentUser?> = _currentUser.asStateFlow()

    // Lightweight scope for the derived flow. Repository is @Singleton so this lives
    // for the app lifetime; SupervisorJob keeps a downstream cancellation from killing it.
    private val derivedScope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    /**
     * True when [currentUser] has every field the booking submit (and other gated
     * actions) needs: first name, last name, email, phone. Single source of truth so
     * the booking VM and the post-signin onboarding agree on what "complete" means.
     */
    val isProfileComplete: StateFlow<Boolean> = _currentUser
        .map { user ->
            user != null &&
                !user.firstName.isBlank() &&
                !user.lastName.isBlank() &&
                !user.email.isBlank() &&
                !user.phoneNumber.isNullOrBlank()
        }
        .stateIn(derivedScope, SharingStarted.Eagerly, initialValue = false)

    override suspend fun clear() {
        _currentUser.value = null
    }

    /** Fetch the authenticated user's profile and update the cached [currentUser]. */
    suspend fun refreshCurrentUser(): ApiResult<Unit> {
        // User id isn't part of the profile response — it's in the JWT sub
        // claim. Pull it once at request time so downstream code can keep
        // using `currentUser.value.id`.
        val accessToken = tokenStore.current()?.accessToken ?: return networkError()
        val userId = JwtDecoder.extractUserId(accessToken) ?: return networkError()

        // Generated method takes an optional `query` parameter because the
        // backend declares `[FromQuery] GetCurrentUser.Query query` (an empty
        // record). Pass null — backend defaults are fine.
        val resp = networkCall { userApi.userGetCurrentUser(query = null) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        val body = resp.body() ?: return networkError()
        _currentUser.value = body.toCurrentUser(userId)
        return ApiResult.Success(Unit)
    }

    /**
     * Update the authenticated user's profile. On success, re-fetches so the
     * cached snapshot reflects server-side normalisations.
     */
    suspend fun updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String?,
        birthDate: String?,
        languageCode: String?,
    ): ApiResult<Unit> {
        val userId = _currentUser.value?.id ?: return networkError()

        // Generated command takes `kotlinx.datetime.LocalDate?` for birthDate.
        // UI passes "yyyy-MM-dd" (or blank). Parse defensively — a malformed
        // value silently drops to null so we don't blow up the call.
        val parsedBirthDate = birthDate?.trim()?.ifBlank { null }?.let { raw ->
            runCatching { LocalDate.parse(raw) }.getOrNull()
        }

        val resp = networkCall {
            userApi.userUpdateCurrentUser(
                UpdateCurrentUserCommand(
                    id = userId,
                    firstName = firstName,
                    lastName = lastName,
                    phoneNumber = phoneNumber?.ifBlank { null },
                    birthDate = parsedBirthDate,
                    languageCode = languageCode,
                ),
            )
        } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }

        // Re-fetch so the cache reflects the persisted row (trimmed whitespace,
        // phone normalisation, language-code canonicalisation, etc).
        return refreshCurrentUser()
    }

    /**
     * Permanently delete the signed-in user's account. On success, wipes local
     * tokens and emits a forced sign-out so the app returns to the login screen.
     */
    suspend fun deleteAccount(): ApiResult<Unit> {
        val resp = networkCall { gdprApi.gdprDeleteMyAccount() } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }

        // Wipe every session-scoped cache through the same multibinding sign-out
        // uses — the hand-list previously missed Membership/Recurring/PushToken,
        // leaving the deleted account's data for the next user on this device.
        // The set includes this repo, so its own snapshot is nulled here too.
        sessionScopedCaches.get().forEach { it.clear() }
        tokenStore.clear()
        sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated)
        return ApiResult.Success(Unit)
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Nothing> {
        // Carry the message [ApiErrorParser] already resolved from the body so
        // the surfacing ViewModel shows the identical string. The 401 object
        // would drop that message, so it folds into the message-carrying
        // [ApiError.Unknown] alongside the generic fallback.
        val message = ApiErrorParser.parseToUserMessage(appContext, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
    }
}
