package cz.cleansia.customer.features.booking

import cz.cleansia.core.snackbar.SnackbarInsetScope

import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.AnchoredDraggableState
import androidx.compose.foundation.gestures.DraggableAnchors
import androidx.compose.foundation.gestures.Orientation
import androidx.compose.foundation.gestures.anchoredDraggable
import androidx.compose.foundation.gestures.animateTo
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.customer.core.payments.toConfiguration
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

private enum class SheetAnchor { Hidden, Peek, Half, Full }

/**
 * Which way a step change slides in: 1 from the right, -1 from the left. Forward comes in from the
 * trailing edge and back from the leading one, so a right-to-left layout mirrors both; the step
 * leaving moves the opposite way.
 */
internal fun stepSlideDirection(forward: Boolean, rtl: Boolean): Int = (if (forward) 1 else -1) * (if (rtl) -1 else 1)

/**
 * The draft with its address seeded from [preferred], the saved address Home's top bar chose. A blank
 * street always seeds. A resumed draft re-seeds only when Home's choice has moved on and the address is
 * still the one this seeded, so an address the customer picked in the sheet is never overwritten. The
 * iOS twin is BookingPrefill.hydratedWithPreferred.
 */
internal fun BookingState.hydratedWithPreferred(preferred: UserAddress): BookingState {
    val stillSeeded = savedAddressId != null && savedAddressId == hydratedFromSavedId
    if (street.isNotBlank() && !(stillSeeded && preferred.serverId != savedAddressId)) return this
    return copy(
        street = preferred.street,
        city = preferred.city,
        zipCode = preferred.zipCode,
        countryIsoCode = preferred.countryIsoCode,
        savedAddressId = preferred.serverId,
        hydratedFromSavedId = preferred.serverId,
    )
}

private const val TOTAL_STEPS = BookingViewModel.TOTAL_STEPS

/**
 * Bolt-style draggable booking sheet. Wraps content in a BoxWithConstraints so all
 * size/offset math uses the ACTUAL parent constraint, not physical screen pixels.
 */
@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable
fun BookingBottomSheet(
    visible: Boolean,
    onDismiss: () -> Unit = {},
    onComplete: (confirmationCode: String, orderId: String) -> Unit = { _, _ -> },
    onNavigateToEditProfile: () -> Unit = {},
    // Wave 3 Phase R1 — when non-null AND `visible` flips to true, the inner
    // content fetches the referenced order and seeds BookingState (services,
    // packages, rooms/bathrooms, address). One-shot per id (guarded inside).
    rebookFromOrderId: String? = null,
    // When non-null AND `visible` flips to true, seeds [BookingState.selectedPackageIds]
    // with this single package id. Powers the "popular packages" tap-to-add flow on
    // the home tab — user taps a package card → sheet opens with it already chosen.
    // One-shot per id (guarded inside via lastPrefilledPackage).
    prefillPackageId: String? = null,
    // When non-null AND `visible` flips to true, seeds rooms and bathrooms — the Home carousel's
    // quick-size slide. Through the clamping setters, so it can never exceed the booking caps.
    prefillSize: Pair<Int, Int>? = null,
) {
    // While the sheet is open, lift the snackbar above the sticky CTA (primary
    // button / swipe-to-confirm). Bigger than MainShell's 88dp because the CTA
    // sits ON TOP of the bottom nav (they coexist).
    if (visible) {
        cz.cleansia.core.snackbar.SnackbarInsetScope(120.dp)
    }

    BoxWithConstraints(modifier = Modifier.fillMaxSize()) {
        // Real available height inside the layout (Compose layout space, not physical pixels)
        val parentHeightPx = constraints.maxHeight.toFloat()

        SheetWithAnchors(
            parentHeightPx = parentHeightPx,
            visible = visible,
            onDismiss = onDismiss,
            onComplete = onComplete,
            onNavigateToEditProfile = onNavigateToEditProfile,
            rebookFromOrderId = rebookFromOrderId,
            prefillPackageId = prefillPackageId,
            prefillSize = prefillSize,
        )
    }
}

@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable
private fun SheetWithAnchors(
    parentHeightPx: Float,
    visible: Boolean,
    onDismiss: () -> Unit,
    onComplete: (confirmationCode: String, orderId: String) -> Unit,
    onNavigateToEditProfile: () -> Unit,
    rebookFromOrderId: String? = null,
    prefillPackageId: String? = null,
    prefillSize: Pair<Int, Int>? = null,
) {
    val density = LocalDensity.current
    val decay = androidx.compose.animation.rememberSplineBasedDecay<Float>()

    val draggableState = remember(parentHeightPx) {
        AnchoredDraggableState(
            initialValue = SheetAnchor.Hidden,
            positionalThreshold = { distance -> distance * 0.4f },
            velocityThreshold = { with(density) { 500.dp.toPx() } },
            snapAnimationSpec = spring(dampingRatio = 0.9f, stiffness = 400f),
            decayAnimationSpec = decay,
        ).apply {
            updateAnchors(
                DraggableAnchors {
                    SheetAnchor.Hidden at parentHeightPx
                    SheetAnchor.Peek at parentHeightPx * 0.70f
                    SheetAnchor.Half at parentHeightPx * 0.45f
                    SheetAnchor.Full at parentHeightPx * 0.08f
                },
            )
        }
    }

    LaunchedEffect(visible) {
        if (!visible) {
            draggableState.animateTo(SheetAnchor.Hidden)
            return@LaunchedEffect
        }
        // animateTo(Full) FIRST, then start watching for dismiss. If we
        // subscribe to snapshotFlow before the animation runs, the very
        // first emission is the cached currentValue from the previous
        // close (Hidden) — which would immediately fire onDismiss() and
        // cancel the reopen. Bug repro: open sheet, slide down hard to
        // fully dismiss, tap Book again → sheet wouldn't reopen.
        draggableState.animateTo(SheetAnchor.Full)
        snapshotFlow { draggableState.currentValue }
            .distinctUntilChanged()
            // Drop the replayed-on-subscribe snapshot so we only react to
            // genuine post-open transitions.
            .drop(1)
            .collect { value ->
                if (value == SheetAnchor.Hidden) onDismiss()
            }
    }

    if (!visible && draggableState.currentValue == SheetAnchor.Hidden) return

    SheetContent(
        draggableState = draggableState,
        parentHeightPx = parentHeightPx,
        visible = visible,
        onDismiss = onDismiss,
        onComplete = onComplete,
        onNavigateToEditProfile = onNavigateToEditProfile,
        rebookFromOrderId = rebookFromOrderId,
        prefillPackageId = prefillPackageId,
        prefillSize = prefillSize,
    )
}

@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable
private fun SheetContent(
    draggableState: AnchoredDraggableState<SheetAnchor>,
    parentHeightPx: Float,
    visible: Boolean,
    onDismiss: () -> Unit,
    onComplete: (confirmationCode: String, orderId: String) -> Unit,
    onNavigateToEditProfile: () -> Unit,
    rebookFromOrderId: String? = null,
    prefillPackageId: String? = null,
    prefillSize: Pair<Int, Int>? = null,
    sheetViewModel: BookingSheetViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val repo = sheetViewModel.addressRepository
    val orderRepo = sheetViewModel.orderRepository
    val catalogRepo = sheetViewModel.catalogRepository
    val snackbarController = sheetViewModel.snackbarController
    val addresses by repo.addresses.collectAsState(initial = emptyList())
    val selectedId by repo.selectedId.collectAsState(initial = null)
    val preferred = addresses.firstOrNull { it.id == selectedId }
        ?: addresses.firstOrNull { it.isDefault }
        ?: addresses.firstOrNull()

    val bookingVm: BookingViewModel = androidx.hilt.navigation.compose.hiltViewModel()
    val state by bookingVm.state.collectAsStateWithLifecycle()
    // Wave 4 — `submitting: Boolean` was folded into `submitState: ActionState`.
    // Derive the boolean here so the rest of this composable's enable/loading
    // logic stays untouched.
    val submitState by bookingVm.submitState.collectAsStateWithLifecycle()
    val submitting = submitState is cz.cleansia.customer.ui.state.ActionState.Submitting
    val expressWaiver by bookingVm.expressWaiver.collectAsStateWithLifecycle()
    val currentStep by bookingVm.step.collectAsStateWithLifecycle()
    val canStepBack by bookingVm.canStepBack.collectAsStateWithLifecycle()
    val canPlaceOrder by bookingVm.canPlaceOrder.collectAsStateWithLifecycle()
    val scope = androidx.compose.runtime.rememberCoroutineScope()

    var showAddressManager by remember { mutableStateOf(false) }

    BackHandler(enabled = visible) {
        if (canStepBack) bookingVm.previousStep() else onDismiss()
    }

    // Counter we bump every time submission fails. Passed to the slide-to-confirm
    // button as `resetTrigger` so the thumb snaps back to the start and the user
    // can swipe again — without it the button locks at the end and is stuck.
    var submitFailedCount by remember { mutableIntStateOf(0) }

    // Pending card-payment context. When card flow lands in BookingSubmitOutcome.CardPending
    // we stash the response here, then PaymentSheet's result callback consults it
    // to decide whether to navigate (success) or just snackbar (cancel/fail).
    // Using a State so the launcher closure reads the latest value, not a snapshot.
    var pendingCardOrder by remember {
        mutableStateOf<cz.cleansia.customer.core.booking.CreateOrderResponse?>(null)
    }

    val paymentSheet = com.stripe.android.paymentsheet.rememberPaymentSheet { result ->
        when (result) {
            is com.stripe.android.paymentsheet.PaymentSheetResult.Completed -> {
                val resp = pendingCardOrder
                if (resp != null) {
                    pendingCardOrder = null
                    bookingVm.reset()
                    onComplete(resp.confirmationCode, resp.id)
                }
            }
            is com.stripe.android.paymentsheet.PaymentSheetResult.Canceled -> {
                // User dismissed PaymentSheet without paying. Order stays in
                // Pending — the cleanup function will Cancel it within an hour
                // if the user doesn't retry.
                snackbarController.showError(
                    context.getString(R.string.error_payment_cancelled),
                )
                pendingCardOrder = null
                submitFailedCount++
            }
            is com.stripe.android.paymentsheet.PaymentSheetResult.Failed -> {
                snackbarController.showError(
                    result.error.localizedMessage
                        ?: context.getString(R.string.error_payment_failed),
                )
                pendingCardOrder = null
                submitFailedCount++
            }
        }
    }

    val cardGuaranteeSheet = com.stripe.android.paymentsheet.rememberPaymentSheet { result ->
        when (result) {
            is com.stripe.android.paymentsheet.PaymentSheetResult.Completed -> scope.launch {
                when (val outcome = bookingVm.submitAfterCardGuarantee()) {
                    is BookingSubmitOutcome.Success -> {
                        bookingVm.reset()
                        onComplete(outcome.response.confirmationCode, outcome.response.id)
                    }
                    BookingSubmitOutcome.ProfileIncomplete -> {
                        onDismiss()
                        onNavigateToEditProfile()
                    }
                    is BookingSubmitOutcome.CardPending,
                    is BookingSubmitOutcome.CardGuaranteeNeeded,
                    BookingSubmitOutcome.Failed,
                    -> submitFailedCount++
                }
            }
            is com.stripe.android.paymentsheet.PaymentSheetResult.Canceled -> {
                bookingVm.abandonCardGuarantee()
                snackbarController.showErrorKey(R.string.booking_card_guarantee_cancelled)
                submitFailedCount++
            }
            is com.stripe.android.paymentsheet.PaymentSheetResult.Failed -> {
                bookingVm.abandonCardGuarantee()
                snackbarController.showError(
                    result.error.localizedMessage ?: context.getString(R.string.booking_card_guarantee_cancelled),
                )
                submitFailedCount++
            }
        }
    }

    // Seed the address from Home's choice whenever the sheet becomes visible OR a different preferred
    // address arrives. The content leaves composition while hidden and the saved addresses start empty,
    // so every open sees preferred?.id arrive and runs this: a blank draft takes Home's address, and a
    // resumed one follows a change of Home's address unless the customer picked one in the sheet (see
    // hydratedWithPreferred). Suppressed when rebook is in flight — the rebook effect owns the address
    // and handles saved-address matching itself.
    LaunchedEffect(visible, preferred?.id) {
        val address = preferred ?: return@LaunchedEffect
        if (visible && rebookFromOrderId == null) bookingVm.update { it.hydratedWithPreferred(address) }
    }

    // Wave 3 Phase R1 — pre-fill from a previous order when the sheet opens
    // with `rebookFromOrderId` set. Guarded by `lastRebookedFrom` so a sheet
    // recomposition (or returning from Edit Profile) doesn't re-fetch and
    // clobber edits the user made in between. Catalog lookup drops any service
    // / package id no longer in the active catalog and informs the user via
    // snackbar; we still pass through whatever's left.
    var lastRebookedFrom by remember { mutableStateOf<String?>(null) }

    // The booking outlives the sheet. A plain open — the Book FAB, a "Book now" card — resumes it where
    // the customer swiped it away, on the same step with every choice kept, for as long as this
    // ViewModel lives (the signed-in session). An open that seeds a booking (Order again, a popular
    // package, the quick-size slide) starts that booking afresh in the draft's place: each seed effect
    // below resets before it fills. A booking that goes through resets before navigating. The
    // address hydration above fills a blank street once the saved addresses arrive, which is after
    // these effects run, so a seeded open still gets the default address.
    LaunchedEffect(visible) {
        bookingVm.setSheetVisible(visible)
        // A resumed draft's time may have passed while it was away. Only a plain open re-checks it: a
        // seeded one resets the draft below, and a notice about a time it throws away would be noise.
        if (visible && rebookFromOrderId == null && prefillPackageId == null && prefillSize == null) {
            bookingVm.revalidateResumedTime()
        }
    }
    // A swipe takes the sheet to Hidden before `visible` flips, so this content leaves composition
    // without ever composing closed and the effect above never hears of the close. Leaving composition
    // is the one thing every way out shares, so the close is recorded there too.
    DisposableEffect(bookingVm) { onDispose { bookingVm.setSheetVisible(false) } }
    // A sheet left open in the background comes back to a clock that has moved on, so its time is
    // re-checked on the way back, as a plain open re-checks one. Only a start after a stop is a return:
    // the ON_START a new observer is sent as it enters composition is not one. Nor is a booking being
    // placed or paid re-checked: its order exists or is on its way, and a bank's 3-D Secure hop stops
    // the host, so a slot boundary passing meanwhile would tell a paying customer to pick a time again.
    // The submit state is read live, as the composed one stops following the flow while stopped.
    var stopped by remember { mutableStateOf(false) }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) { stopped = true }
    LifecycleEventEffect(Lifecycle.Event.ON_START) {
        val placing = bookingVm.submitState.value is cz.cleansia.customer.ui.state.ActionState.Submitting
        if (stopped && visible && !placing && pendingCardOrder == null) bookingVm.revalidateResumedTime()
        stopped = false
    }

    LaunchedEffect(visible, rebookFromOrderId) {
        if (!visible) return@LaunchedEffect
        val target = rebookFromOrderId ?: return@LaunchedEffect
        if (target == lastRebookedFrom) return@LaunchedEffect
        lastRebookedFrom = target
        bookingVm.reset()

        val order = orderRepo.getById(target)
            .onError { error -> if (error !is ApiError.Network) snackbarController.showError(error) }
            .getOrNull()
            ?: return@LaunchedEffect

        // Match the inline address against any saved address by street + city + zip
        // (case-insensitive) so the saved-address UI stays in selected state.
        val savedAddresses = addresses
        val matchedSavedAddress = order.address?.let { addr ->
            savedAddresses.firstOrNull {
                it.street.equals(addr.street, ignoreCase = true) &&
                    it.city.equals(addr.city, ignoreCase = true) &&
                    it.zipCode.equals(addr.zipCode, ignoreCase = true)
            }
        }

        // Cross-check against the active catalog so we drop any service /
        // package that's been retired since the original booking. If the
        // catalog hasn't loaded yet (cold start), trust the original ids and
        // let the create call fail loudly later.
        val activeServiceIds = catalogRepo.services.value.map { it.id }.toSet()
        val activePackageIds = catalogRepo.packages.value.map { it.id }.toSet()
        val catalogReady = activeServiceIds.isNotEmpty() || activePackageIds.isNotEmpty()

        val originalServiceIds = order.selectedServices?.mapNotNull { it.id }.orEmpty()
        val originalPackageIds = order.selectedPackages?.mapNotNull { it.id }.orEmpty()

        val keptServiceIds = if (catalogReady) {
            originalServiceIds.filter { it in activeServiceIds }
        } else {
            originalServiceIds
        }
        val keptPackageIds = if (catalogReady) {
            originalPackageIds.filter { it in activePackageIds }
        } else {
            originalPackageIds
        }
        val droppedAny = catalogReady &&
            (keptServiceIds.size != originalServiceIds.size ||
                keptPackageIds.size != originalPackageIds.size)

        bookingVm.update { current ->
            current.copy(
                selectedServiceIds = keptServiceIds.toSet(),
                selectedPackageIds = keptPackageIds.toSet(),
                street = order.address?.street.orEmpty(),
                city = order.address?.city.orEmpty(),
                zipCode = order.address?.zipCode.orEmpty(),
                // Rebook re-uses the original address. If we matched a saved
                // address we know its ISO code; otherwise fall back to whatever
                // was already in state (typically the auto-defaulted served
                // country code).
                countryIsoCode = matchedSavedAddress?.countryIsoCode
                    ?: current.countryIsoCode,
                savedAddressId = matchedSavedAddress?.serverId,
            )
        }
        if (order.rooms > 0) bookingVm.setRooms(order.rooms)
        if (order.bathrooms > 0) bookingVm.setBathrooms(order.bathrooms)

        if (droppedAny) {
            snackbarController.showInfoKey(R.string.order_rebook_unavailable_items)
        }
    }

    // Reset the dedupe guard when the rebook target clears so a future open
    // with the same id (rare) re-fills cleanly. Keeping it set across
    // visible-toggles is intentional: when the user closes & reopens with the
    // same id, the cached fill from the first open is what they want.
    LaunchedEffect(rebookFromOrderId) {
        if (rebookFromOrderId == null) lastRebookedFrom = null
    }

    // Pop-package prefill — separate from rebook because the source is a
    // tap-on-popular-package, not a past-order replay. A fresh booking with
    // the package chosen, the first time the sheet becomes visible with the
    // arg set; the rest of the wizard keeps its defaults so the user only
    // chooses what they actually want to change. Guarded so re-composition
    // or sheet-reopen doesn't re-toggle.
    var lastPrefilledPackage by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(visible, prefillPackageId) {
        if (!visible) return@LaunchedEffect
        val target = prefillPackageId ?: return@LaunchedEffect
        if (target == lastPrefilledPackage) return@LaunchedEffect
        lastPrefilledPackage = target
        bookingVm.reset()
        bookingVm.update { current ->
            current.copy(selectedPackageIds = current.selectedPackageIds + target)
        }
    }
    LaunchedEffect(prefillPackageId) {
        if (prefillPackageId == null) lastPrefilledPackage = null
    }

    // Quick-size prefill — the same one-shot shape as the package one above.
    var lastPrefilledSize by remember { mutableStateOf<Pair<Int, Int>?>(null) }
    LaunchedEffect(visible, prefillSize) {
        if (!visible) return@LaunchedEffect
        val target = prefillSize ?: return@LaunchedEffect
        if (target == lastPrefilledSize) return@LaunchedEffect
        lastPrefilledSize = target
        bookingVm.reset()
        bookingVm.setRooms(target.first)
        bookingVm.setBathrooms(target.second)
    }
    LaunchedEffect(prefillSize) {
        if (prefillSize == null) lastPrefilledSize = null
    }

    val stepTitle = when (currentStep) {
        1 -> stringResource(R.string.booking_step1_title)
        2 -> stringResource(R.string.dirtiness_title)
        3 -> stringResource(R.string.booking_step2_title)
        4 -> stringResource(R.string.booking_step3_title)
        else -> ""
    }

    val canContinue = when (currentStep) {
        1 -> (state.selectedServiceIds.isNotEmpty() || state.selectedPackageIds.isNotEmpty()) && state.rooms >= 1
        2 -> state.dirtinessLevel != null
        3 -> state.street.isNotBlank() &&
            state.selectedLocalDate != null &&
            state.selectedTime.isNotBlank()
        4 -> canPlaceOrder
        else -> false
    }

    val offsetPx = if (draggableState.offset.isNaN()) parentHeightPx else draggableState.offset
    val fullAnchor = parentHeightPx * 0.08f
    val peekAnchor = parentHeightPx * 0.70f
    val expansionProgress = ((offsetPx - fullAnchor) / (peekAnchor - fullAnchor)).coerceIn(0f, 1f)
    val cornerRadius = (expansionProgress * 20f).dp

    // Sheet height = parent - top offset. Computed in dp from the parent constraint.
    val sheetHeightDp = with(LocalDensity.current) { (parentHeightPx - fullAnchor).toDp() }

    Box(modifier = Modifier.fillMaxSize()) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .height(sheetHeightDp)
            .offset { IntOffset(0, offsetPx.roundToInt()) }
            .anchoredDraggable(
                state = draggableState,
                orientation = Orientation.Vertical,
            )
            .shadow(
                elevation = 28.dp,
                shape = RoundedCornerShape(topStart = cornerRadius, topEnd = cornerRadius),
                clip = false,
                ambientColor = Color.Black,
                spotColor = Color.Black,
            )
            .background(
                color = MaterialTheme.colorScheme.background,
                shape = RoundedCornerShape(topStart = cornerRadius, topEnd = cornerRadius),
            )
            .imePadding(),
    ) {
        // Drag handle pill
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp, bottom = 4.dp),
            contentAlignment = Alignment.TopCenter,
        ) {
            Box(
                modifier = Modifier
                    .size(width = 32.dp, height = 4.dp)
                    .background(
                        MaterialTheme.colorScheme.onSurface.copy(alpha = 0.12f),
                        RoundedCornerShape(2.dp),
                    ),
            )
        }

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 4.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = {
                if (canStepBack) bookingVm.previousStep() else onDismiss()
            }) {
                Icon(
                    if (canStepBack) Icons.AutoMirrored.Outlined.ArrowBack else Icons.Outlined.Close,
                    contentDescription = null,
                )
            }
            Text(
                stepTitle,
                style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold),
                modifier = Modifier.weight(1f),
            )
            Text(
                stringResource(R.string.booking_step_indicator, currentStep, TOTAL_STEPS),
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(end = 16.dp),
            )
        }

        LinearProgressIndicator(
            progress = { currentStep.toFloat() / TOTAL_STEPS },
            modifier = Modifier.fillMaxWidth().height(3.dp),
            color = MaterialTheme.colorScheme.primary,
            trackColor = MaterialTheme.colorScheme.surfaceVariant,
        )

        // With animations removed in the system settings Compose runs these at zero duration, so the
        // step simply changes.
        val rtl = androidx.compose.ui.platform.LocalLayoutDirection.current == androidx.compose.ui.unit.LayoutDirection.Rtl
        Box(modifier = Modifier.weight(1f)) {
            androidx.compose.animation.AnimatedContent(
                targetState = currentStep,
                transitionSpec = {
                    val slideDistance = stepSlideDirection(forward = targetState > initialState, rtl = rtl)
                    (androidx.compose.animation.slideInHorizontally(
                        animationSpec = androidx.compose.animation.core.tween(280),
                    ) { it * slideDistance } + androidx.compose.animation.fadeIn(
                        animationSpec = androidx.compose.animation.core.tween(280),
                    )) togetherWith (androidx.compose.animation.slideOutHorizontally(
                        animationSpec = androidx.compose.animation.core.tween(280),
                    ) { -it * slideDistance } + androidx.compose.animation.fadeOut(
                        animationSpec = androidx.compose.animation.core.tween(280),
                    ))
                },
                label = "booking-step-transition",
            ) { step ->
                when (step) {
                    1 -> ServicesStep(
                        state = state,
                        onUpdate = { next -> bookingVm.update { next } },
                        onRoomsChange = bookingVm::setRooms,
                        onBathroomsChange = bookingVm::setBathrooms,
                    )
                    2 -> DirtinessStep(
                        selected = state.dirtinessLevel,
                        onSelect = { level -> bookingVm.update { it.copy(dirtinessLevel = level) } },
                    )
                    3 -> WhenWhereStep(
                        state = state,
                        onUpdate = { next -> bookingVm.update { next } },
                        onPickAddressOnMap = { showAddressManager = true },
                        expressWaiver = expressWaiver,
                    )
                    4 -> ConfirmStep(state = state, onUpdate = { next -> bookingVm.update { next } })
                }
            }
        }

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.surface)
                .navigationBarsPadding()
                .padding(horizontal = 20.dp, vertical = 12.dp),
        ) {
            // Wave 4 — `quote: QuoteOrderResponse?` was folded into a sealed
            // `quoteState: QuoteState`. Pull out the response once via the
            // Quoted variant so the existing `quote?.let { … }` shape below
            // remains 1:1 with the prior implementation.
            val quoteState by bookingVm.quoteState.collectAsStateWithLifecycle()
            val quote = (quoteState as? QuoteState.Quoted)?.response
            val effectiveDiscount by bookingVm.effectiveDiscount.collectAsStateWithLifecycle()
            // The slide-button label and the receipt above it are the same number by construction:
            // one resolver, one discount from the view model. This bar used to redo the math with the
            // server discounts left out, so a Plus member read two different totals on one screen.
            // On a card booking it is what the card is asked for — the figure the Stripe sheet then
            // shows — so credit that applies is already off it.
            val totalDisplay = quote?.let { q ->
                val payByCard = state.paymentMethod == BookingViewModel.PAYMENT_CARD
                formatOrderPrice(BookingPriceSummary.resolve(q, effectiveDiscount, payByCard).dueOnCard, q.currencyCode)
            }
            if (currentStep == TOTAL_STEPS) {
                // Slide to confirm — Wolt-style, prevents accidental taps on the final step.
                val confirmText = if (totalDisplay != null) {
                    stringResource(R.string.booking_slide_to_confirm_price, totalDisplay)
                } else {
                    stringResource(R.string.booking_slide_to_confirm)
                }
                cz.cleansia.customer.ui.components.SwipeToConfirmButton(
                    text = confirmText,
                    onConfirmed = {
                        scope.launch {
                            when (val outcome = bookingVm.submit()) {
                                is BookingSubmitOutcome.Success -> {
                                    // Cash flow — order is already created, navigate
                                    // straight to success. Clear wizard state first so
                                    // a future Book Now tap opens to a clean slate.
                                    bookingVm.reset()
                                    onComplete(
                                        outcome.response.confirmationCode,
                                        outcome.response.id,
                                    )
                                }
                                is BookingSubmitOutcome.CardPending -> {
                                    // Card flow — order is created in Pending. Open
                                    // PaymentSheet to confirm payment. Navigation
                                    // happens in the PaymentSheetResult callback above
                                    // when the user successfully completes payment.
                                    pendingCardOrder = outcome.response
                                    paymentSheet.presentWithPaymentIntent(
                                        paymentIntentClientSecret = outcome.paymentSheet.clientSecret,
                                        configuration = outcome.paymentSheet.toConfiguration(),
                                    )
                                }
                                is BookingSubmitOutcome.CardGuaranteeNeeded -> {
                                    cardGuaranteeSheet.presentWithSetupIntent(
                                        setupIntentClientSecret = outcome.setup.setupIntentClientSecret,
                                        configuration = com.stripe.android.paymentsheet.PaymentSheet.Configuration(
                                            merchantDisplayName = "Cleansia",
                                            customer = com.stripe.android.paymentsheet.PaymentSheet.CustomerConfiguration(
                                                id = outcome.setup.customerId,
                                                ephemeralKeySecret = outcome.setup.ephemeralKey,
                                            ),
                                            googlePay = com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration(
                                                environment = if (cz.cleansia.customer.BuildConfig.GOOGLE_PAY_PRODUCTION) {
                                                    com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration.Environment.Production
                                                } else {
                                                    com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration.Environment.Test
                                                },
                                                countryCode = "CZ",
                                                currencyCode = outcome.setup.currencyCode,
                                            ),
                                            allowsDelayedPaymentMethods = false,
                                        ),
                                    )
                                }
                                BookingSubmitOutcome.ProfileIncomplete -> {
                                    onDismiss()
                                    onNavigateToEditProfile()
                                }
                                BookingSubmitOutcome.Failed -> {
                                    // Bump the reset counter so the slide button
                                    // snaps back to the start. The user sees the
                                    // error in the snackbar and can re-swipe.
                                    submitFailedCount++
                                }
                            }
                        }
                    },
                    enabled = canContinue && !submitting,
                    resetTrigger = submitFailedCount,
                )
            } else {
                val buttonText = if (totalDisplay != null) {
                    stringResource(R.string.booking_continue_price, totalDisplay)
                } else {
                    stringResource(R.string.booking_continue)
                }
                CleansiaPrimaryButton(
                    text = buttonText,
                    onClick = bookingVm::nextStep,
                    enabled = canContinue,
                )
            }
        }
    }

        AnimatedVisibility(
            visible = showAddressManager,
            enter = fadeIn(animationSpec = tween(250)) +
                scaleIn(
                    initialScale = 0.96f,
                    animationSpec = spring(dampingRatio = 0.85f, stiffness = 300f),
                ),
            exit = fadeOut(animationSpec = tween(200)) +
                scaleOut(targetScale = 0.97f, animationSpec = tween(200)),
        ) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background),
            ) {
                BackHandler { showAddressManager = false }
                cz.cleansia.customer.features.addresses.AddressManagerScreen(
                    onBack = { showAddressManager = false },
                    onAddressSelected = { picked ->
                        bookingVm.update {
                            it.copy(
                                street = picked.street,
                                city = picked.city,
                                zipCode = picked.zipCode,
                                countryIsoCode = picked.countryIsoCode,
                                savedAddressId = picked.serverId,
                            )
                        }
                        showAddressManager = false
                    },
                )
            }
        }

        // Wave 4 — busy overlay over the whole sheet while submission is in
        // flight. Swallows touches so the user can't double-tap the slide
        // button or open the address manager mid-submit.
        cz.cleansia.customer.ui.components.BusyMascotOverlay(
            visible = submitting,
            message = androidx.compose.ui.res.stringResource(cz.cleansia.customer.R.string.busy_booking),
        )
    }
}
