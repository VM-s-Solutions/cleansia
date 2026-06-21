package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.data.checklist.OrderChecklistRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Tracks which checklist items the cleaner has ticked off for a single
 * order. Backed by [OrderChecklistRepository] (DataStore) so the
 * checked set survives process death, screen rotation, and brief app
 * backgrounding. The set is keyed by `orderId` resolved from the nav
 * SavedStateHandle, so multiple in-flight orders never collide.
 */
@HiltViewModel
class CleaningChecklistViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val repository: OrderChecklistRepository,
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for CleaningChecklistViewModel")

    val checkedIds: StateFlow<Set<String>> = repository.observeChecked(orderId)
        .stateIn(
            scope = viewModelScope,
            // WhileSubscribed so the DataStore Flow stops cold once the
            // detail screen leaves composition; restarts when the cleaner
            // navigates back in. 5s grace window covers config-change
            // recreations without redundant disk reads.
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = emptySet(),
        )

    fun setChecked(itemId: String, checked: Boolean) {
        viewModelScope.launch {
            repository.setChecked(orderId, itemId, checked)
        }
    }
}
