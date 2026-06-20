package cz.cleansia.partner.features.invoices.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.EmployeeInvoiceDto
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.invoices.InvoicesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class InvoicesListUiState(
    /**
     * True while a USER-initiated pull-to-refresh is in flight. Drives
     * the chunky [PullToRefreshBox] indicator at the top of the list.
     * Background refreshes (init + ON_RESUME) MUST NOT flip this — the
     * pull spinner is the user's reward for their gesture and showing
     * it on automatic refreshes feels like a glitch.
     */
    val isUserRefreshing: Boolean = false,
    /**
     * True while a SILENT background refresh is in flight (first load,
     * ON_RESUME on stale cache). Kept separate from [isUserRefreshing]
     * so the pull indicator never fires from auto-refresh paths. Used
     * by [isInitialLoad] in the screen to gate the full-page spinner.
     */
    val isBackgroundRefreshing: Boolean = false,
    val invoices: List<EmployeeInvoiceDto> = emptyList(),
    val error: String? = null,
    /**
     * True once the first load (success or error) has completed.
     * Lets the screen distinguish "we're still showing the initial full-page
     * spinner" from "the user pulled to refresh on an empty list" — the
     * latter must keep the empty mascot visible so PullToRefreshBox can
     * detect the gesture and show its indicator.
     */
    val hasLoadedOnce: Boolean = false,
)

@HiltViewModel
class InvoicesListViewModel @Inject constructor(
    private val invoicesRepository: InvoicesRepository,
    private val userProfileStore: UserProfileStore,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(InvoicesListUiState())
    val uiState: StateFlow<InvoicesListUiState> = _uiState.asStateFlow()

    init { ensureFreshOrCachedAsync() }

    /**
     * USER pull-to-refresh entry point. Always hits the network — the
     * user's gesture is the source of truth, not the cache age — and
     * drives the chunky [PullToRefreshBox] spinner via
     * [InvoicesListUiState.isUserRefreshing].
     */
    fun refresh() = userRefresh()

    /**
     * Lifecycle ON_RESUME hook. Routes through the silent-stale path so
     * tab-switching / returning from a detail screen on a fresh cache
     * is a no-op (no spinner flicker, no network round-trip).
     */
    fun onResume() = ensureFreshOrCachedAsync()

    /**
     * Pulls the latest invoices unconditionally and flips
     * [InvoicesListUiState.isUserRefreshing] so the pull indicator
     * shows. Only called from the user's pull gesture.
     */
    private fun userRefresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isUserRefreshing = true, error = null) }
            fetchAndUpdate(
                clearFlags = { it.copy(isUserRefreshing = false) },
            )
        }
    }

    /**
     * Silent-stale refresh. No-ops when the repo's freshness watermark
     * is still inside the window (default 30s) — the user already has
     * fresh data on screen, no reason to round-trip. Flips
     * [InvoicesListUiState.isBackgroundRefreshing] (NOT the user flag)
     * so the pull indicator stays quiet.
     */
    private fun ensureFreshOrCachedAsync() {
        viewModelScope.launch {
            val staleness = invoicesRepository.getMyInvoicesStaleness()
            if (!staleness.isStale()) {
                // Cache is fresh — make sure hasLoadedOnce is true so
                // the screen doesn't get stuck on the initial spinner
                // when the repo cache was warmed by another screen.
                _uiState.update {
                    if (it.hasLoadedOnce) it else it.copy(hasLoadedOnce = true)
                }
                return@launch
            }
            _uiState.update { it.copy(isBackgroundRefreshing = true, error = null) }
            fetchAndUpdate(
                clearFlags = { it.copy(isBackgroundRefreshing = false) },
            )
        }
    }

    /**
     * Shared fetch + state-merge path used by both [userRefresh] and
     * [ensureFreshOrCachedAsync]. The caller owns which "in flight"
     * flag was raised; [clearFlags] is the transform that lowers it
     * (and only it) when the request finishes.
     */
    private suspend fun fetchAndUpdate(
        clearFlags: (InvoicesListUiState) -> InvoicesListUiState,
    ) {
        val employeeId = userProfileStore.current()?.employeeId
        if (employeeId.isNullOrBlank()) {
            _uiState.update {
                clearFlags(it).copy(invoices = emptyList(), hasLoadedOnce = true)
            }
            return
        }
        when (val result = invoicesRepository.getMyInvoices(employeeId)) {
            is ApiResult.Success -> _uiState.update {
                clearFlags(it).copy(invoices = result.data, hasLoadedOnce = true)
            }
            is ApiResult.Error -> {
                snackbar.showError(errorTranslator.translate(result.error))
                _uiState.update { clearFlags(it).copy(hasLoadedOnce = true) }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }
}
