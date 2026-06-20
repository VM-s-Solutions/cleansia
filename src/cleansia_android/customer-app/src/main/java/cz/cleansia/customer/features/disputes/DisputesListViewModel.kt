package cz.cleansia.customer.features.disputes

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.disputes.DisputeListItemDto
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch

/**
 * ViewModel backing the DisputesListScreen. Thin passthrough over the
 * singleton [DisputeRepository] — the repo owns the cache, this VM just
 * exposes the flows plus kicks off the first load if the user reached the
 * screen without a prefetch (disputes aren't prefetched in MainShell by
 * design — infrequent feature, lazy load is cheaper).
 *
 * Mirrors [cz.cleansia.customer.features.orders.OrdersTab]'s direct repo
 * access pattern but via a dedicated VM so the screen can collect the flows
 * through `hiltViewModel()`; no extra state lives here.
 */
@HiltViewModel
class DisputesListViewModel @Inject constructor(
    private val disputeRepository: DisputeRepository,
    val snackbar: SnackbarController,
) : ViewModel() {

    val disputes: StateFlow<List<DisputeListItemDto>> = disputeRepository.disputes
    val loading: StateFlow<Boolean> = disputeRepository.loading
    val loadingMore: StateFlow<Boolean> = disputeRepository.loadingMore
    val loaded: StateFlow<Boolean> = disputeRepository.loaded
    val total: StateFlow<Int> = disputeRepository.totalRecords

    init {
        // Only kick off a fetch if the singleton cache hasn't already been
        // populated (e.g. user opened the list, went back, then re-opened).
        viewModelScope.launch {
            if (!disputeRepository.loaded.value) {
                disputeRepository.refresh().onError(::surfaceError)
            }
        }
    }

    /** Pull-to-refresh — always hits the network regardless of cache state. */
    fun refresh() {
        viewModelScope.launch { disputeRepository.refresh().onError(::surfaceError) }
    }

    /** Infinite scroll — additive page fetch, silent on failure. */
    fun loadNextPage() {
        viewModelScope.launch { disputeRepository.loadNextPage() }
    }

    private fun surfaceError(error: ApiError) {
        if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
    }
}
