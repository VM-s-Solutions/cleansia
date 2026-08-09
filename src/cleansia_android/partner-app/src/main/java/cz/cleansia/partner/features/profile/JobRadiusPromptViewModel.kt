package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Whether to ask this cleaner, once, how far they want to hear about work. There is no loading or
 * error case: a prompt that cannot answer its own question simply does not appear, and the ask is
 * not spent, so the next launch tries again.
 */
sealed interface JobRadiusPromptUiState {
    data object Hidden : JobRadiusPromptUiState
    data object Visible : JobRadiusPromptUiState
}

/**
 * "Never set a radius" is not the same question as "never been asked". A null radius is a legitimate
 * standing choice — the country-wide board — so asking on the null alone would re-ask the cleaner who
 * chose it, every launch, forever. The device remembers the ask; the server owns the preference.
 *
 * The ask belongs to the cleaner, not to the handset: on a shared company phone a device-global
 * memory asks whoever signs in first and never asks anyone after them.
 */
@HiltViewModel
class JobRadiusPromptViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val appSettingsRepository: AppSettingsRepository,
    private val employeeIdResolver: EmployeeIdResolver,
) : ViewModel() {

    private val _uiState = MutableStateFlow<JobRadiusPromptUiState>(JobRadiusPromptUiState.Hidden)
    val uiState: StateFlow<JobRadiusPromptUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            val employeeId = employeeIdResolver.resolve() ?: return@launch
            if (appSettingsRepository.hasAnsweredJobRadiusPrompt(employeeId)) return@launch
            val radius = (profileRepository.getJobRadius() as? ApiResult.Success)?.data ?: return@launch
            if (radius.jobRadiusKm == null) {
                _uiState.value = JobRadiusPromptUiState.Visible
            } else {
                appSettingsRepository.markJobRadiusPromptAnswered(employeeId)
            }
        }
    }

    /** The country-wide board, which is already the stored state — nothing to send. */
    fun onKeepEveryJob() = answer()

    fun onChooseRadius() = answer()

    private fun answer() {
        _uiState.value = JobRadiusPromptUiState.Hidden
        viewModelScope.launch {
            val employeeId = employeeIdResolver.resolve() ?: return@launch
            appSettingsRepository.markJobRadiusPromptAnswered(employeeId)
        }
    }
}
