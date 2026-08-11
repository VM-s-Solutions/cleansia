package cz.cleansia.partner.features.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Mirrors `JobProximity.MinRadiusKm` / `MaxRadiusKm`; a drift here 400s every save. */
object JobRadius {
    const val MIN_KM = 1
    const val MAX_KM = 500

    /** Where the slider starts for a cleaner who has never set one — a normal city commute. */
    const val DEFAULT_KM = 25
}

data class JobRadiusForm(
    val employeeId: String = "",
    val limitEnabled: Boolean = false,
    val radiusKm: Int = JobRadius.DEFAULT_KM,
) {
    /**
     * What goes on the wire. `null` is the country-wide choice, and the clamp exists so a seed value
     * from an older row can never be echoed back as something the server refuses.
     */
    val wireRadiusKm: Int?
        get() = if (limitEnabled) radiusKm.coerceIn(JobRadius.MIN_KM, JobRadius.MAX_KM) else null
}

sealed interface JobRadiusUiState {
    data object Loading : JobRadiusUiState
    data object Error : JobRadiusUiState
    data class Loaded(val form: JobRadiusForm) : JobRadiusUiState
}

@HiltViewModel
class JobRadiusViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<JobRadiusUiState>(JobRadiusUiState.Loading)
    val uiState: StateFlow<JobRadiusUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    init { load() }

    fun retry() = load()

    private fun load() {
        viewModelScope.launch {
            _uiState.value = JobRadiusUiState.Loading
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val saved = result.data.jobRadiusKm
                    _uiState.value = JobRadiusUiState.Loaded(
                        JobRadiusForm(
                            employeeId = result.data.id.orEmpty(),
                            limitEnabled = saved != null,
                            radiusKm = saved ?: JobRadius.DEFAULT_KM,
                        ),
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.value = JobRadiusUiState.Error
                }
            }
        }
    }

    /**
     * Off keeps the slider where it was rather than resetting it — turning the limit off is a
     * preference change, not a decision to forget the number they picked.
     */
    fun onLimitEnabledChange(enabled: Boolean) = updateForm { it.copy(limitEnabled = enabled) }

    fun onRadiusChange(radiusKm: Int) = updateForm {
        it.copy(radiusKm = radiusKm.coerceIn(JobRadius.MIN_KM, JobRadius.MAX_KM))
    }

    fun save() {
        val form = (_uiState.value as? JobRadiusUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }
        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updateJobRadius(
                employeeId = form.employeeId,
                radiusKm = form.wireRadiusKm,
            )
            _saveState.value = ActionState.Idle
            when (result) {
                is ApiResult.Success -> _saved.emit(Unit)
                is ApiResult.Error -> snackbar.showError(errorTranslator.translate(result.error))
            }
        }
    }

    private inline fun updateForm(transform: (JobRadiusForm) -> JobRadiusForm) {
        _uiState.update { state ->
            if (state is JobRadiusUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}
