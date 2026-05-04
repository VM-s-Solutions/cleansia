package cz.cleansia.customer.core.user

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.auth.ForcedSignOutReason
import cz.cleansia.customer.core.auth.SessionManager
import cz.cleansia.customer.core.auth.TokenStore
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
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

@Singleton
class UserRepository @Inject constructor(
    private val api: UserApi,
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    private val addressRepository: AddressRepository,
    private val orderRepository: OrderRepository,
    private val disputeRepository: DisputeRepository,
    private val loyaltyRepository: LoyaltyRepository,
    private val referralRepository: ReferralRepository,
    @ApplicationContext private val appContext: Context,
) {
    /**
     * Cached current-user snapshot. Screens observe this; call [refreshCurrentUser]
     * to trigger a fetch. Emits null while the first fetch is in flight and
     * after [deleteAccount]/sign-out.
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

    /**
     * Fetch the authenticated user's profile and update the cached [currentUser].
     *
     * @return null on success, translated user-facing error message on failure.
     */
    suspend fun refreshCurrentUser(): String? {
        val response = try {
            api.getCurrent()
        } catch (t: Throwable) {
            return appContext.getString(R.string.error_generic_network)
        }

        if (!response.isSuccessful) {
            return ApiErrorParser.parseToUserMessage(appContext, response.errorBody(), response.code())
        }

        val body = response.body() ?: return appContext.getString(R.string.error_generic_network)
        _currentUser.value = body.toCurrentUser()
        return null
    }

    /**
     * Update the authenticated user's profile. On success, re-fetches so the
     * cached snapshot reflects server-side normalisations.
     *
     * @return null on success, translated user-facing error message on failure.
     */
    suspend fun updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String?,
        birthDate: String?,
        languageCode: String?,
    ): String? {
        val userId = _currentUser.value?.id
            ?: return appContext.getString(R.string.error_generic_network)
        val response = try {
            api.updateCurrentUser(
                UpdateCurrentUserCommand(
                    id = userId,
                    firstName = firstName,
                    lastName = lastName,
                    phoneNumber = phoneNumber?.ifBlank { null },
                    birthDate = birthDate?.ifBlank { null },
                    languageCode = languageCode,
                ),
            )
        } catch (t: Throwable) {
            return appContext.getString(R.string.error_generic_network)
        }

        if (!response.isSuccessful) {
            return ApiErrorParser.parseToUserMessage(appContext, response.errorBody(), response.code())
        }

        // Re-fetch so the cache reflects the persisted row (trimmed whitespace,
        // phone normalisation, language-code canonicalisation, etc).
        return refreshCurrentUser()
    }

    /**
     * Permanently delete the signed-in user's account. On success, wipes local
     * tokens and emits a forced sign-out so the app returns to the login screen.
     *
     * Returns a user-facing translated error message on failure, or null on success.
     */
    suspend fun deleteAccount(): String? {
        val response = try {
            api.deleteAccount()
        } catch (t: Throwable) {
            return appContext.getString(R.string.error_generic_network)
        }

        if (!response.isSuccessful) {
            return ApiErrorParser.parseToUserMessage(appContext, response.errorBody(), response.code())
        }

        _currentUser.value = null
        orderRepository.clear()
        addressRepository.clear()
        disputeRepository.clear()
        loyaltyRepository.clear()
        referralRepository.clear()
        tokenStore.clear()
        sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated)
        return null
    }
}
