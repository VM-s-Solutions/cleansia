package cz.cleansia.customer.features.rewards

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.loyalty.LoyaltyActivityItemDto
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * ViewModel backing [RewardsActivityScreen]. Activity history is *not* cached
 * by [LoyaltyRepository] (it's an infrequent drilldown) — this VM owns the
 * paged list locally for the lifetime of the screen.
 *
 * Pagination follows the same shape as [cz.cleansia.customer.core.disputes.DisputeRepository]
 * and OrderRepository: page-1 fetched in `init`, infinite scroll pulls
 * additional pages via [loadNextPage] when the LazyColumn nears its end.
 */
@HiltViewModel
class RewardsActivityViewModel @Inject constructor(
    private val loyaltyRepository: LoyaltyRepository,
) : ViewModel() {

    private val _items = MutableStateFlow<List<LoyaltyActivityItemDto>>(emptyList())
    val items: StateFlow<List<LoyaltyActivityItemDto>> = _items.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    private val _loadingMore = MutableStateFlow(false)
    val loadingMore: StateFlow<Boolean> = _loadingMore.asStateFlow()

    private val _loaded = MutableStateFlow(false)
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    private val _total = MutableStateFlow(0)
    val total: StateFlow<Int> = _total.asStateFlow()

    private val pageSize = 20

    init {
        refresh()
    }

    /** Pull-to-refresh — always replaces the list with a fresh page-0 fetch. */
    fun refresh() {
        if (_loading.value) return
        viewModelScope.launch {
            _loading.value = true
            try {
                val resp = loyaltyRepository.loadActivity(offset = 0, limit = pageSize)
                if (resp != null) {
                    _items.value = resp.data
                    _total.value = resp.total
                    _loaded.value = true
                }
            } finally {
                _loading.value = false
            }
        }
    }

    /**
     * Fetch the next page if there are more rows to load. Silent on failure —
     * the snackbar from the repo already surfaces the error and the user can
     * scroll up to retry.
     */
    fun loadNextPage() {
        if (_loadingMore.value || _loading.value) return
        if (_items.value.size >= _total.value) return
        viewModelScope.launch {
            _loadingMore.value = true
            try {
                val resp = loyaltyRepository.loadActivity(
                    offset = _items.value.size,
                    limit = pageSize,
                )
                if (resp != null) {
                    _items.value = _items.value + resp.data
                    _total.value = resp.total
                }
            } finally {
                _loadingMore.value = false
            }
        }
    }
}
