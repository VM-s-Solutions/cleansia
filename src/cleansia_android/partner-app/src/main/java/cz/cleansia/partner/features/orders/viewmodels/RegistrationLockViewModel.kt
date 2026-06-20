package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.auth.AuthRepository
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
 * Mirrors partner-web's `RegistrationCompletionService.checkRegistrationCompletion()`
 * decision tree: a cleaner can take orders only when profile + documents
 * are filled AND admin has Approved / Active'd the contract. (Availability
 * used to be part of this gate but no longer is.)
 *
 * The categories list drives the rows on the lock screen so the cleaner sees
 * exactly what's outstanding and where to go fix it.
 */
data class RegistrationLockUiState(
    /**
     * `true` only while a USER-initiated refresh is in flight (pull-to-
     * refresh gesture or explicit Retry tap). Drives the suds rosette in
     * [PullToRefreshBox] — see invariant #1: the pull indicator NEVER
     * subscribes to [isBackgroundRefreshing] or any generic isLoading.
     */
    val isUserRefreshing: Boolean = false,
    /**
     * `true` while a silent stale-driven refresh is in flight (ON_RESUME
     * after the 15s window, init-time first fetch). Intentionally NOT
     * wired to the pull-to-refresh indicator — these refreshes happen
     * invisibly behind the existing cached data so the screen stays
     * calm. Useful for diagnostics / future inline spinners if needed.
     */
    val isBackgroundRefreshing: Boolean = false,
    val status: RegistrationCompletionStatus? = null,
    /**
     * Translated error message from the most recent failed refresh, or
     * null when the last refresh succeeded. Surfaced inline on the lock
     * screen as a banner with a Retry button — pull-to-refresh is also
     * available, but a banner makes the error state legible even when
     * the cleaner doesn't try to pull.
     *
     * Note: errors do NOT clear the cached [status] — the defensive
     * "stay locked on fetch error" behavior is preserved by simply not
     * touching [status] in the Error branch. Last-known categories
     * (or all-Missing if nothing's been fetched yet) stay rendered.
     */
    val errorMessage: String? = null,
    /**
     * Flips to true the first time any refresh completes (Success OR Error)
     * and stays true forever after. The lock screen uses this to draw a
     * hard line between "initial paint, show the centered spinner" and
     * "subsequent refresh, show the pull-to-refresh suds indicator".
     *
     * Without this flag, the screen previously gated initial-load on
     * `status == null`, which raced with the coroutine completion: if
     * a loading flag stayed true while status transitioned away from
     * null, the pull-to-refresh indicator could get stuck spinning.
     * Mirrors the working pattern in InvoicesListViewModel.
     */
    val hasLoadedOnce: Boolean = false,
)

enum class StepStatus { Done, Pending, Missing }

enum class StepCategory(val key: String) {
    Profile("profile"),
    Documents("documents"),
    Approval("approval"),
}

data class StepRow(
    val category: StepCategory,
    val status: StepStatus,
    /**
     * Translation keys (or backend-emitted i18n keys for the Profile row's
     * missing-fields list). Rendered as bullet points under the category
     * header on the lock screen.
     */
    val detailKeys: List<String>,
    /**
     * Destination the "Fix" CTA should route to. `null` when the row has no
     * actionable fix (e.g. the Approval row when admin is reviewing or has
     * rejected — cleaner can't unblock that themselves).
     */
    val fixDestination: NavRoute?,
)

/**
 * Returns true when the cleaner can use the Orders tab. Matches the web's
 * `isComplete` clause exactly.
 */
fun RegistrationCompletionStatus.isRegistrationComplete(): Boolean =
    (hasCompletedProfile == true) &&
        (areDocumentsUploaded == true) &&
        // Availability is no longer a gate — backend always reports
        // hasSetAvailability=true now; we don't read it here either.
        (contractStatus == ContractStatus._4 /* Approved */ ||
            contractStatus == ContractStatus._2 /* Active */)

@HiltViewModel
class RegistrationLockViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
) : ViewModel() {

    private val _uiState = MutableStateFlow(RegistrationLockUiState())
    val uiState: StateFlow<RegistrationLockUiState> = _uiState.asStateFlow()

    init {
        // Invariant #2: init {} MUST use the cached/stale-checking path,
        // never the user-pull path. Otherwise the first paint would flash
        // a spurious suds spinner before we've even shown anything.
        ensureFreshOrCachedAsync()
    }

    /**
     * Called by the screen on Lifecycle.Event.ON_RESUME. Routes through
     * the staleness-gated path so a quick "save section → come back" loop
     * inside the 15s window skips the network entirely and shows the
     * cached categories instantly. (Invariant #3.)
     */
    fun onResume() {
        ensureFreshOrCachedAsync()
    }

    /**
     * Pull-to-refresh + Retry button entry point. Always fetches — the
     * user's intent is the source of truth, not the cache age. Drives
     * the suds rosette via [RegistrationLockUiState.isUserRefreshing].
     */
    fun userRefresh() {
        viewModelScope.launch { fetchRegistrationStatus(userInitiated = true) }
    }

    /**
     * Silent-stale path: only hits the network if the cached watermark
     * is older than [STALE_WINDOW_MS] (15s — shorter than the default
     * 30s because the cleaner on this screen is actively waiting for
     * admin approval and a stale gate that feels broken would push them
     * to spam the pull gesture).
     *
     * When the cache is fresh enough this is a true no-op — no flag
     * toggle, no coroutine spend, no UI churn. When it is stale, the
     * refresh runs silently behind the existing cached categories
     * (isBackgroundRefreshing=true, isUserRefreshing stays false, so
     * the pull indicator does NOT appear — see invariant #1).
     *
     * First-ever entry (lastFetchedAt == null) is treated as stale by
     * [Staleness.isStale], so the initial load still happens here and
     * the screen's centered spinner kicks in while hasLoadedOnce=false.
     */
    private fun ensureFreshOrCachedAsync() {
        val staleness = profileRepository.getRegistrationStatusStaleness()
        if (!staleness.isStale(STALE_WINDOW_MS)) return
        viewModelScope.launch { fetchRegistrationStatus(userInitiated = false) }
    }

    /**
     * Single fetch implementation shared by [userRefresh] and
     * [ensureFreshOrCachedAsync]. The [userInitiated] flag picks which
     * loading flag to toggle so the pull indicator stays bound to user
     * intent only. Both branches reset errorMessage on success and
     * preserve cached `status` on error (defensive: never accidentally
     * unlock a half-onboarded cleaner on a transient network blip).
     */
    private suspend fun fetchRegistrationStatus(userInitiated: Boolean) {
        _uiState.update {
            if (userInitiated) it.copy(isUserRefreshing = true)
            else it.copy(isBackgroundRefreshing = true)
        }
        when (val result = profileRepository.getRegistrationStatus()) {
            is ApiResult.Success -> _uiState.update {
                // Clear both loading flags + any prior error on success.
                // hasLoadedOnce flips here AND in the Error branch so
                // the screen can distinguish "first paint" from
                // "subsequent refresh" reliably.
                it.copy(
                    isUserRefreshing = false,
                    isBackgroundRefreshing = false,
                    status = result.data,
                    errorMessage = null,
                    hasLoadedOnce = true,
                )
            }
            is ApiResult.Error -> _uiState.update {
                // Stay locked on error: don't touch `status` so the
                // last-known categories (or all-Missing on first-attempt
                // failure) stay rendered. Surface errorMessage so the
                // banner + Retry path is visible. hasLoadedOnce flips
                // here too — a failed initial load still counts as
                // "attempted once" so the centered spinner gives way
                // to the error banner.
                it.copy(
                    isUserRefreshing = false,
                    isBackgroundRefreshing = false,
                    errorMessage = errorTranslator.translate(result.error),
                    hasLoadedOnce = true,
                )
            }
        }
    }

    fun signOut(onSignedOut: () -> Unit) {
        viewModelScope.launch {
            authRepository.logout()
            onSignedOut()
        }
    }

    companion object {
        /**
         * Stale window for the registration-status cache. Deliberately
         * shorter than [Staleness.DEFAULT_MAX_AGE_MS] (30s) because the
         * cleaner on this screen is actively waiting for admin approval
         * and a longer gate feels broken — they'd alt-tab back, see no
         * change, and reach for the pull gesture. Trade-off documented
         * in the spec's Risks section: monitor telemetry to validate.
         */
        const val STALE_WINDOW_MS: Long = 15_000L

        /** Builds the 3 category rows the lock screen renders. */
        fun buildSteps(status: RegistrationCompletionStatus?): List<StepRow> {
            val profileMissing = status?.missingFields.orEmpty()
            val profileDone = status?.hasCompletedProfile == true
            val docsDone = status?.areDocumentsUploaded == true
            val contract = status?.contractStatus

            val approvalStatus: StepStatus
            val approvalDetails: List<String>
            val approvalFixDestination: NavRoute?
            when {
                contract == ContractStatus._4 || contract == ContractStatus._2 -> {
                    approvalStatus = StepStatus.Done
                    approvalDetails = emptyList()
                    approvalFixDestination = null
                }
                contract == ContractStatus._5 -> {
                    approvalStatus = StepStatus.Missing
                    approvalDetails = listOf("registration_lock.approval_rejected")
                    // Rejected cleaners need to talk to support; render the
                    // mailto link directly in the row, not a NavRoute.
                    approvalFixDestination = null
                }
                profileDone && docsDone &&
                    contract == ContractStatus._1 -> {
                    approvalStatus = StepStatus.Pending
                    approvalDetails = listOf("registration_lock.approval_awaiting_review")
                    approvalFixDestination = null
                }
                else -> {
                    approvalStatus = StepStatus.Missing
                    approvalDetails = listOf("registration_lock.approval_complete_profile_first")
                    approvalFixDestination = null
                }
            }

            return listOf(
                StepRow(
                    category = StepCategory.Profile,
                    status = if (profileDone) StepStatus.Done else StepStatus.Missing,
                    detailKeys = if (profileDone) emptyList() else profileMissing,
                    // Profile row launches the multi-step onboarding chain
                    // (Personal → Address → Identification → Bank) so the
                    // cleaner doesn't have to bounce through the lock
                    // between sections. `onboarding=true` tells the saved
                    // section to navigate forward instead of popping back.
                    fixDestination = if (profileDone) null
                    else firstMissingProfileSection(profileMissing, forOnboarding = true),
                ),
                StepRow(
                    category = StepCategory.Documents,
                    status = if (docsDone) StepStatus.Done else StepStatus.Missing,
                    detailKeys = if (docsDone) emptyList()
                    else listOf("registration_lock.documents_required"),
                    fixDestination = if (docsDone) null else NavRoute.ProfileDocuments,
                ),
                StepRow(
                    category = StepCategory.Approval,
                    status = approvalStatus,
                    detailKeys = approvalDetails,
                    fixDestination = approvalFixDestination,
                ),
            )
        }

        /** Priority order for resolving "which section owns this field". */
        private val sectionFieldOwnership: List<Pair<ProfileSection, Set<String>>>
            get() = ProfileSection.values().map { it to it.ownedFields() }

        /**
         * Picks the first profile section that has unfilled fields, so the
         * cleaner taps "Complete profile" and lands exactly where they need
         * to be — not on the Profile hub. Priority order matches the visual
         * order on the Profile screen.
         *
         * Maps the backend's `profile.fields.*` keys to section destinations.
         * Falls back to Personal when nothing matches (defensive — server
         * said hasCompletedProfile=false but didn't list a field).
         *
         * [forOnboarding] propagates the onboarding flag into the returned
         * NavRoute so the receiving section knows to chain forward on save
         * instead of popping back to the lock.
         */
        fun firstMissingProfileSection(
            missingFields: List<String>,
            forOnboarding: Boolean = false,
        ): NavRoute {
            for ((section, fields) in sectionFieldOwnership) {
                if (missingFields.any { it in fields }) {
                    return section.toRoute(forOnboarding)
                }
            }
            return ProfileSection.Personal.toRoute(forOnboarding)
        }

        /**
         * Given the freshly-fetched status after a section saved, returns
         * the NEXT section the onboarding chain should land on. Returns
         * null when nothing else is missing — caller pops the chain back
         * to the registration lock so the unlock moment is visible.
         *
         * Skips sections whose fields are now Done so a partial fix
         * (e.g. cleaner only added missing fields on Personal but Address
         * was already complete) jumps straight to Identification.
         */
        fun nextOnboardingDestination(status: RegistrationCompletionStatus): NavRoute? {
            if (status.hasCompletedProfile == true) return null
            val missing = status.missingFields.orEmpty()
            for ((section, fields) in sectionFieldOwnership) {
                if (missing.any { it in fields }) return section.toRoute(forOnboarding = true)
            }
            // Server says profile incomplete but didn't list a field —
            // safest default is to drop back to the lock so the cleaner
            // sees the current state instead of looping on the last section.
            return null
        }
    }
}

/**
 * Section identifier used by the registration-lock routing helpers. Keeps
 * the `forOnboarding` flag construction in one place — every caller goes
 * through `.toRoute(...)` so we can't accidentally hand-build a section
 * route without the flag.
 *
 * [ownedFields] returns the backend `profile.fields.*` keys that belong
 * to this section. The lock + chain reuse this single source of truth
 * to decide "which section owns this missing field?" — declaring it
 * twice (once in the lock VM map, once on the section screen) is how
 * the Personal section drifted from the lock's "missing fields" list
 * in the first place.
 */
enum class ProfileSection {
    Personal, Address, Identification, Bank;

    fun toRoute(forOnboarding: Boolean): NavRoute = when (this) {
        Personal -> NavRoute.ProfilePersonal(onboarding = forOnboarding)
        Address -> NavRoute.ProfileAddress(onboarding = forOnboarding)
        Identification -> NavRoute.ProfileIdentification(onboarding = forOnboarding)
        Bank -> NavRoute.ProfileBank(onboarding = forOnboarding)
    }

    fun ownedFields(): Set<String> = when (this) {
        Personal -> setOf(
            "profile.fields.firstName",
            "profile.fields.lastName",
            "profile.fields.email",
            "profile.fields.phoneNumber",
            "profile.fields.birthDate",
        )
        Address -> setOf(
            "profile.fields.street",
            "profile.fields.city",
            "profile.fields.zipCode",
            "profile.fields.country",
        )
        Identification -> setOf(
            "profile.fields.passportId",
            "profile.fields.nationality",
            "profile.fields.registrationNumber",
            "profile.fields.legalEntityName",
        )
        Bank -> setOf("profile.fields.iban")
    }
}
