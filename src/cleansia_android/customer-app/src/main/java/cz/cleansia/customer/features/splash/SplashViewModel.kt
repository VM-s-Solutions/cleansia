package cz.cleansia.customer.features.splash

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.orders.OrderRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@HiltViewModel
class SplashViewModel @Inject constructor(
    private val tokenStore: TokenStore,
    orderRepository: OrderRepository,
    membershipRepository: MembershipRepository,
) : ViewModel() {

    init {
        // Home's first paint waits for both, so they load during the brand hold rather than after it. The splash is
        // popped when the hold ends, and an answer still on its way has to land then: MainShell skips an orders
        // refresh already in flight, so a cancelled one would leave Home without orders.
        if (hasValidSession()) {
            viewModelScope.launch { withContext(NonCancellable) { orderRepository.refresh() } }
            viewModelScope.launch { withContext(NonCancellable) { membershipRepository.refresh() } }
        }
    }

    /** The access token may have expired — the 401 Authenticator refreshes it — so only the refresh token gates. */
    fun hasValidSession(): Boolean = tokenStore.current()?.let { !it.isRefreshExpired() } == true
}
