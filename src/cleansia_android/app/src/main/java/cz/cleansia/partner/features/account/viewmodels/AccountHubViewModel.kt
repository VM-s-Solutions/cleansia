package cz.cleansia.partner.features.account.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.repositories.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class AccountHubUiState(
    val recentOrders: List<Order> = emptyList(),
    val isLoadingOrders: Boolean = false
)

@HiltViewModel
class AccountHubViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(AccountHubUiState())
    val uiState: StateFlow<AccountHubUiState> = _uiState.asStateFlow()

    init {
        loadRecentOrders()
    }

    private fun loadRecentOrders() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoadingOrders = true) }
            when (val result = ordersRepository.getMyOrders(
                page = 1,
                statuses = listOf(
                    OrderStatus.COMPLETED,
                    OrderStatus.IN_PROGRESS,
                    OrderStatus.CONFIRMED,
                    OrderStatus.PENDING
                )
            )) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            recentOrders = result.data.orders.take(3),
                            isLoadingOrders = false
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isLoadingOrders = false) }
                }
            }
        }
    }
}
