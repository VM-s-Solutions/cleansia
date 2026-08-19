package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.core.gdpr.GdprDeletionClient
import cz.cleansia.partner.core.network.ApiErrorTranslator
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Submits the cleaner's account-deletion REQUEST. → /decisions/adr-0052
 *
 * Deliberately unlike the customer app's equivalent in two ways, and both are the point rather than
 * an omission:
 *
 *  - **No session teardown.** The customer flow wipes caches, clears the token store and emits a
 *    forced sign-out, because the account really is gone. Here nothing has happened yet — an admin
 *    fulfils the request after the paperwork — so signing the cleaner out would lock them out of an
 *    account that still exists with jobs still assigned to them.
 *  - **[requested] is terminal for this screen.** There is no local pending state to reload, because
 *    the server already refuses a second request with `gdpr.deletion_already_pending`. Tracking it
 *    here would be a second copy of a fact the backend owns.
 */
@HiltViewModel
class DeleteAccountViewModel @Inject constructor(
    private val deletionClient: GdprDeletionClient,
    private val errors: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _state = MutableStateFlow<ActionState>(ActionState.Idle)
    val state: StateFlow<ActionState> = _state.asStateFlow()

    private val _requested = MutableStateFlow(false)

    /** True once the request is filed — the screen swaps the CTA for a confirmation. */
    val requested: StateFlow<Boolean> = _requested.asStateFlow()

    fun submit() {
        if (_state.value is ActionState.Submitting || _requested.value) return
        _state.value = ActionState.Submitting

        viewModelScope.launch {
            deletionClient.requestDeletion()
                .onSuccess {
                    _state.value = ActionState.Idle
                    _requested.value = true
                    snackbar.showSuccessKey(R.string.delete_account_requested)
                }
                .onError { error ->
                    // The interceptor already surfaces a network drop; doubling it reads as two
                    // separate failures. Everything else is a refusal the cleaner needs the words
                    // for — an assigned job, unsettled pay, a request already pending.
                    if (error !is ApiError.Network) snackbar.showError(errors.translate(error))
                    _state.value = ActionState.Error(errors.translate(error))
                }
        }
    }
}
