package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.NavHostController
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.navigation.NavRoute
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Per-section completion snapshot used by the chain header. Mirrors
 * `RegistrationCompletionStatus.missingFields` resolved through the
 * section-ownership map, so a section is "done" the moment it has no
 * remaining missing fields.
 */
data class OnboardingChainState(
    val isLoading: Boolean = true,
    val completionByCategory: Map<ProfileSection, Boolean> = ProfileSection.values()
        .associateWith { false },
) {
    val totalSteps: Int get() = ProfileSection.values().size
    val completedSteps: Int get() = completionByCategory.values.count { it }
}

/**
 * Owns the "what's the next missing profile section?" decision for the
 * onboarding chain triggered from the registration lock. When a section
 * in onboarding mode finishes saving it calls [advanceOrFinish] with the
 * nav controller; the VM re-fetches `RegistrationCompletionStatus` and
 * navigates to either the next missing section or back to the lock.
 *
 * Also exposes [state] so each section screen can render a chain header
 * ("Step 2 of 4 · Address → Identification → Bank") and the cleaner
 * sees they're in a multi-step flow rather than just one isolated page.
 */
@HiltViewModel
class OnboardingChainViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(OnboardingChainState())
    val state: StateFlow<OnboardingChainState> = _state.asStateFlow()

    init {
        // Eagerly load so the chain header can render the moment the
        // section screen mounts — without this the first section would
        // briefly show "Step ? of 4" while waiting for the network.
        refresh()
    }

    /**
     * Re-fetches the registration status. Section screens call this from
     * a `LifecycleEventEffect(ON_RESUME)` so the chain header reflects
     * the latest server state whenever the cleaner returns to a screen
     * (e.g. after a save → chain navigates → next screen mounts).
     */
    fun refresh() {
        viewModelScope.launch {
            _state.update { it.copy(isLoading = true) }
            val status = (profileRepository.getRegistrationStatus()
                as? ApiResult.Success)?.data
            _state.update {
                it.copy(
                    isLoading = false,
                    completionByCategory = perSectionCompletion(status),
                )
            }
        }
    }

    /**
     * Re-fetches status and navigates forward in the chain. If the
     * profile is now complete, pops back to the registration lock so
     * the cleaner sees their row turn green. On API error we also pop
     * back — the lock will re-fetch via its own ON_RESUME effect and
     * surface any error there.
     */
    fun advanceOrFinish(navController: NavHostController) {
        viewModelScope.launch {
            val result = profileRepository.getRegistrationStatus()
            val next = when (result) {
                is ApiResult.Success ->
                    RegistrationLockViewModel.nextOnboardingDestination(result.data)
                is ApiResult.Error -> null
            }
            // Update local state too so the next mounted section sees
            // accurate per-section completion immediately without a
            // second round-trip.
            val status = (result as? ApiResult.Success)?.data
            _state.update {
                it.copy(
                    isLoading = false,
                    completionByCategory = perSectionCompletion(status),
                )
            }
            if (next == null) {
                // Chain finished (or status fetch failed) — pop the
                // current section off, falling back to whatever is
                // underneath (the registration lock in the onboarding
                // case; the profile menu in maintenance edits — which
                // never call this method anyway).
                navController.popBackStack()
            } else {
                // Replace the current section with the next one so the
                // back stack doesn't grow Personal → Address →
                // Identification → Bank → Lock; users tapping system
                // back should go to the lock, not the previous section.
                navController.navigate(next) {
                    popUpTo(NavRoute.RegistrationLock) { inclusive = false }
                }
            }
        }
    }

    private fun perSectionCompletion(
        status: RegistrationCompletionStatus?,
    ): Map<ProfileSection, Boolean> {
        if (status == null) return ProfileSection.values().associateWith { false }
        // If the whole profile is complete, every section is done.
        if (status.hasCompletedProfile == true) {
            return ProfileSection.values().associateWith { true }
        }
        val missing = status.missingFields.orEmpty().toSet()
        return ProfileSection.values().associateWith { section ->
            section.ownedFields().none { it in missing }
        }
    }
}
