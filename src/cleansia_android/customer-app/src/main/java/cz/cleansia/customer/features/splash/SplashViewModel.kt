package cz.cleansia.customer.features.splash

import androidx.lifecycle.ViewModel
import cz.cleansia.core.auth.TokenStore
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

@HiltViewModel
class SplashViewModel @Inject constructor(
    private val tokenStore: TokenStore,
) : ViewModel() {

    /** The access token may have expired — the 401 Authenticator refreshes it — so only the refresh token gates. */
    fun hasValidSession(): Boolean = tokenStore.current()?.let { !it.isRefreshExpired() } == true
}
