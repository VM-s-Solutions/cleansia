package cz.cleansia.customer.features.orders

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.ListAlt
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.Cancel
import androidx.compose.material.icons.outlined.Map
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.ReportProblem
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.input.nestedscroll.NestedScrollConnection
import androidx.compose.ui.input.nestedscroll.NestedScrollSource
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.snackbar.SnackbarInsetScope
import cz.cleansia.core.ui.components.CleansiaErrorState
import cz.cleansia.core.ui.components.SnapAnchor
import cz.cleansia.core.ui.components.SnapSheet
import cz.cleansia.core.ui.components.SnapSheetState
import cz.cleansia.core.ui.components.rememberSnapSheetState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderCurrencyDetailDto
import cz.cleansia.customer.core.orders.OrderAddressDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderStatusTrackDto
import cz.cleansia.customer.core.orders.ReceiptOpenResult
import cz.cleansia.customer.core.orders.openReceiptPdf
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.features.recurring.RecurringAuthoringGate
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.customer.ui.theme.CleansiaTheme
import kotlinx.coroutines.launch

/**
 * Order detail — a full-bleed map of the cleaning address with a three-anchor
 * [SnapSheet] of the order over it, the same layout the partner app uses for a
 * job. Dragging the sheet down to [SnapAnchor.MapFocus] hands the screen to the
 * map; dragging up to [SnapAnchor.Expanded] hands it to the order.
 *
 * The sheet keeps its action footer pinned at every anchor, so cancelling or
 * reporting an issue never depends on how far the panel has been dragged.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderDetailScreen(
    onBack: () -> Unit = {},
    // Wave 3 Phase R1 — wired to the booking sheet via NavHost. Tapping the
    // footer's "Book again" button stashes this order id in NavHost-scoped
    // state, pops back to MainShell, and the booking sheet opens pre-filled.
    onRebook: () -> Unit = {},
    // Wave 2 Phase 6 — wired to the CreateDispute nav route. Caller passes
    // the order id as a query arg; the screen pre-fills the form.
    onReportIssue: () -> Unit = {},
    /**
     * PA14 Path B — "Make this recurring". Routes to the create form with
     * the order id pre-filling services/packages/rooms/bathrooms/payment/
     * timeOfDay. Shown on a Completed order unless membership has resolved
     * to "not a member" — see [RecurringAuthoringGate].
     */
    onMakeRecurring: (orderId: String) -> Unit = {},
    @Suppress("UNUSED_PARAMETER") onDownloadReceipt: () -> Unit = {},
    onViewPhotos: () -> Unit = {},
    /**
     * Raise the review sheet as soon as the order resolves — the completion prompt's landing.
     *
     * The prompt routes HERE rather than hosting its own sheet in the shell: this screen already owns
     * the review state, the submit path and the success/close wiring, and a second host would be a
     * second copy of all three.
     */
    openReviewOnLoad: Boolean = false,
    viewModel: OrderDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    // Wave 4 — single ActionState replaces (cancelling, cancelError) etc.
    // The screen still derives the same boolean / message values from the
    // sealed variant; sheets receive those derived bits via their existing
    // params, keeping their composables unchanged.
    val cancelState by viewModel.cancelState.collectAsStateWithLifecycle()
    val cancellationPreview by viewModel.cancellationPreview.collectAsStateWithLifecycle()
    val reviewState by viewModel.reviewState.collectAsStateWithLifecycle()
    val receiptDownloadState by viewModel.receiptDownloadState.collectAsStateWithLifecycle()
    val photosState by viewModel.photos.collectAsStateWithLifecycle()
    // Wave 3.3 — recurring-confirm flow state. Submitting → CTA hides + spinner.
    val confirmRecurringState by viewModel.confirmRecurringState.collectAsStateWithLifecycle()

    val cancelling = cancelState is ActionState.Submitting
    val cancelError = (cancelState as? ActionState.Error)?.message
    val submittingReview = reviewState is ActionState.Submitting
    val reviewError = (reviewState as? ActionState.Error)?.message
    val downloadingReceipt = receiptDownloadState is ActionState.Submitting
    val confirmingRecurring = confirmRecurringState is ActionState.Submitting

    // Local sheet visibility — lifted above the layout so the footer button
    // and the observed success flow can both drive it.
    var showCancelSheet by remember { mutableStateOf(false) }
    var showReviewSheet by remember { mutableStateOf(false) }

    // Deep-linked / prompted arrival. Waits for the order to actually load — opening over a spinner
    // would show a sheet with no cleaner name and no date. Fires once: `remember` survives the
    // recomposition the load itself causes, so dismissing does not immediately re-open.
    var reviewAutoOpened by remember { mutableStateOf(false) }
    val autoOpenOrder = (state as? OrderDetailUiState.Loaded)?.order
    LaunchedEffect(openReviewOnLoad, autoOpenOrder?.id) {
        if (!openReviewOnLoad || reviewAutoOpened || autoOpenOrder == null) return@LaunchedEffect
        // Server truth wins: a review left on another device between the prompt and this screen
        // means there is nothing to ask for.
        if (autoOpenOrder.review != null) {
            reviewAutoOpened = true
            return@LaunchedEffect
        }
        reviewAutoOpened = true
        showReviewSheet = true
    }

    // Close the sheet when the VM confirms the cancel succeeded. The VM itself
    // pushes the success snackbar (it has the currency + refund numbers in
    // reach), so the screen only needs to flip the sheet visibility.
    LaunchedEffect(viewModel) {
        viewModel.cancelResult.collect {
            showCancelSheet = false
        }
    }

    // Same pattern for the review: VM pushes the success snackbar + triggers
    // a re-fetch, screen only flips the sheet closed.
    LaunchedEffect(viewModel) {
        viewModel.reviewResult.collect {
            showReviewSheet = false
        }
    }

    // Wave 3.3 — Stripe PaymentSheet for the card-path confirm. Cash responses
    // come through the same VM channel but with a null clientSecret; we skip
    // PaymentSheet for those (the VM already moved the order to Confirmed).
    val paymentSheet = com.stripe.android.paymentsheet.rememberPaymentSheet { result ->
        when (result) {
            is com.stripe.android.paymentsheet.PaymentSheetResult.Completed ->
                viewModel.notifyCardPaymentResult(success = true)
            is com.stripe.android.paymentsheet.PaymentSheetResult.Canceled ->
                Unit // silent — the user backed out; no snackbar noise
            is com.stripe.android.paymentsheet.PaymentSheetResult.Failed ->
                viewModel.notifyCardPaymentResult(
                    success = false,
                    errorMessage = result.error.localizedMessage,
                )
        }
    }
    LaunchedEffect(viewModel) {
        viewModel.confirmResult.collect { resp ->
            val clientSecret = resp.clientSecret
            val customerId = resp.stripeCustomerId
            val ephemeralKey = resp.ephemeralKey
            android.util.Log.d(
                "OrderDetailConfirm",
                "confirmResult collected: orderId=${resp.orderId} " +
                    "hasClientSecret=${!clientSecret.isNullOrBlank()} " +
                    "hasCustomerId=${!customerId.isNullOrBlank()} " +
                    "hasEphemeralKey=${!ephemeralKey.isNullOrBlank()}",
            )
            // Cash response: clientSecret null → VM already pushed success +
            // refetched. Card response: open PaymentSheet with the returned
            // intent + ephemeral key, mirroring the booking flow's setup.
            if (clientSecret.isNullOrBlank()
                || customerId.isNullOrBlank()
                || ephemeralKey.isNullOrBlank()) {
                android.util.Log.d(
                    "OrderDetailConfirm",
                    "Skipping PaymentSheet — at least one Stripe field is null/blank",
                )
                return@collect
            }
            android.util.Log.d(
                "OrderDetailConfirm",
                "Presenting PaymentSheet for order ${resp.orderId}",
            )
            paymentSheet.presentWithPaymentIntent(
                paymentIntentClientSecret = clientSecret,
                configuration = com.stripe.android.paymentsheet.PaymentSheet.Configuration(
                    merchantDisplayName = "Cleansia",
                    customer = com.stripe.android.paymentsheet.PaymentSheet.CustomerConfiguration(
                        id = customerId,
                        ephemeralKeySecret = ephemeralKey,
                    ),
                    googlePay = com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration(
                        // Follows the Stripe key, not the build type. See build.gradle.kts where
                        // GOOGLE_PAY_PRODUCTION is derived from the publishable-key prefix.
                        environment = if (cz.cleansia.customer.BuildConfig.GOOGLE_PAY_PRODUCTION) {
                            com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration.Environment.Production
                        } else {
                            com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration.Environment.Test
                        },
                        countryCode = "CZ",
                        currencyCode = "CZK",
                    ),
                    allowsDelayedPaymentMethods = false,
                ),
            )
        }
    }

    // Receipt download success: hand the File to openReceiptPdf and route any
    // failure (no viewer installed / other launch error) back through the VM
    // so snackbar ownership stays where Phases 2/3 put it.
    val context = LocalContext.current
    LaunchedEffect(viewModel) {
        viewModel.receiptFile.collect { file ->
            when (openReceiptPdf(context, file)) {
                ReceiptOpenResult.Opened -> Unit
                ReceiptOpenResult.NoViewer -> viewModel.emitReceiptNoViewer()
                is ReceiptOpenResult.Error -> viewModel.emitReceiptOpenError()
            }
        }
    }

    // Figure out which footer actions apply. Only the Loaded branch has an
    // order to pull the status from; the other branches hide the footer.
    val loaded = state as? OrderDetailUiState.Loaded
    val status = loaded?.let { orderStatusFromValue(it.order.orderStatus?.value) }
    val isCancellable = status == OrderStatus.New ||
        status == OrderStatus.Pending ||
        status == OrderStatus.Confirmed
    // Wave 2 Phase 6 — Report Issue is only meaningful AFTER the cleaning has
    // been picked up by a cleaner (Confirmed) and through Completed. New /
    // Pending / Cancelled are hidden because there's nothing to dispute yet.
    val canReportIssue = status == OrderStatus.Confirmed ||
        status == OrderStatus.OnTheWay ||
        status == OrderStatus.InProgress ||
        status == OrderStatus.Completed
    // Wave 3 Phase R1 — "Book again" is only useful for a finished cleaning.
    // Hidden for everything else; the user can still navigate back to Home
    // and tap the FAB to start a fresh booking.
    val canRebook = status == OrderStatus.Completed

    val recurringAuthoring by viewModel.recurringAuthoring.collectAsStateWithLifecycle()
    val canMakeRecurring = canRebook && recurringAuthoring == RecurringAuthoringGate.Allowed

    // Lift the snackbar above the sheet's sticky footer so a cancel error
    // isn't posted underneath the button that caused it.
    if (isCancellable || canReportIssue || canRebook || canMakeRecurring) {
        SnackbarInsetScope(140.dp)
    }

    when (val s = state) {
        is OrderDetailUiState.Loading -> LoadingState(onBack = onBack)
        is OrderDetailUiState.Error -> CleansiaErrorState(
            title = stringResource(R.string.order_detail_error_title),
            message = stringResource(R.string.order_detail_error_message),
            backLabel = stringResource(R.string.common_back),
            // A permanent failure (deleted order, 404) sets canRetry
            // false; passing a null label is what suppresses the CTA,
            // so both halves must stay gated on the same flag.
            retryLabel = if (s.canRetry) stringResource(R.string.order_detail_error_retry) else null,
            onRetry = if (s.canRetry) viewModel::refresh else null,
            onBack = onBack,
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background),
        )
        is OrderDetailUiState.Loaded -> {
            // Kick off the secondary photos fetch once the main detail is
            // resolved. Safe on recomposition — VM guards with its own
            // Idle/Error check so this is effectively one-shot.
            LaunchedEffect(s.order.id) { viewModel.ensurePhotosLoaded() }
            OrderDetailMapLayout(
                order = s.order,
                photosState = photosState,
                showCancel = isCancellable,
                showReportIssue = canReportIssue,
                showRebook = canRebook,
                showMakeRecurring = canMakeRecurring,
                cancelEnabled = !cancelling,
                confirmingRecurring = confirmingRecurring,
                isDownloadingReceipt = downloadingReceipt,
                onBack = onBack,
                onCancel = { showCancelSheet = true },
                onReportIssue = onReportIssue,
                onRebook = onRebook,
                onMakeRecurring = { s.order.id?.let(onMakeRecurring) },
                onLeaveReview = { showReviewSheet = true },
                onDownloadReceipt = { viewModel.downloadReceipt() },
                onViewPhotos = onViewPhotos,
                onConfirmRecurring = { viewModel.confirmRecurring() },
            )
        }
    }

    // Render the sheet on top of the layout. Only Loaded state can open it
    // (isCancellable gates the footer button); guarding here keeps the render
    // defensive in case state flips mid-cancel.
    if (showCancelSheet && loaded != null) {
        // Re-ask on every open: the quote is computed against the server clock
        // at that instant, and a tier boundary moves while the sheet is closed.
        LaunchedEffect(Unit) { viewModel.loadCancellationPreview() }
        CancelOrderSheet(
            previewState = cancellationPreview,
            onRetryPreview = viewModel::loadCancellationPreview,
            isSubmitting = cancelling,
            errorMessage = cancelError,
            onDismiss = {
                // Don't allow dismiss mid-submit — the sheet's own guard also
                // checks, but we double up so the screen's close path is safe
                // whatever path got us here.
                if (!cancelling) {
                    showCancelSheet = false
                    viewModel.dismissCancelError()
                }
            },
            onConfirm = { reason -> viewModel.cancel(reason) },
            onReasonChanged = viewModel::dismissCancelError,
        )
    }

    // Review sheet — opened from ReviewCard for both new reviews ("Leave a
    // review") and edits ("Edit review"). The sheet itself flips title +
    // submit-button copy based on whether an existing review is supplied.
    // The `loaded` guard keeps us defensive if state flips mid-submit.
    if (showReviewSheet && loaded != null) {
        val currentReview = loaded.order.review
        SubmitReviewSheet(
            onDismiss = {
                if (!submittingReview) {
                    showReviewSheet = false
                    viewModel.dismissReviewError()
                }
            },
            onConfirm = { rating, comment, tags ->
                viewModel.submitReview(rating, comment, tags, isEdit = currentReview != null)
            },
            isSubmitting = submittingReview,
            errorMessage = reviewError,
            existingReview = currentReview,
            // A prompt the customer did not ask for offers "Not now" and leads with the question; the
            // card they tapped themselves keeps "Cancel" and the editorial title. Same sheet, honest
            // about which one it is — and the same split iOS makes.
            titleRes = if (reviewAutoOpened && currentReview == null) {
                R.string.order_review_prompt_title
            } else {
                null
            },
            dismissLabelRes = if (reviewAutoOpened && currentReview == null) {
                R.string.order_review_prompt_not_now
            } else {
                R.string.order_review_cancel
            },
        )
    }
}

/* ── Map + sheet shell ── */

@Composable
private fun OrderDetailMapLayout(
    order: OrderDetailDto,
    photosState: PhotosUiState,
    showCancel: Boolean,
    showReportIssue: Boolean,
    showRebook: Boolean,
    showMakeRecurring: Boolean,
    cancelEnabled: Boolean,
    confirmingRecurring: Boolean,
    isDownloadingReceipt: Boolean,
    onBack: () -> Unit,
    onCancel: () -> Unit,
    onReportIssue: () -> Unit,
    onRebook: () -> Unit,
    onMakeRecurring: () -> Unit,
    onLeaveReview: () -> Unit,
    onDownloadReceipt: () -> Unit,
    onViewPhotos: () -> Unit,
    onConfirmRecurring: () -> Unit,
) {
    val status = orderStatusFromValue(order.orderStatus?.value)
    val darkTheme = isSystemInDarkTheme()
    val sheetState = rememberSnapSheetState()
    val scope = rememberCoroutineScope()
    // Hoisted out of the sheet content because the mascot in the overlay needs it too: it is anchored to
    // the TOP OF THE CONTENT, not to the viewport, and the only way an overlay can know where the
    // content's top has got to is to read the same scroll state.
    val contentScroll = rememberScrollState()

    val showMap = canShowOrderMap(
        latitude = order.address?.latitude,
        longitude = order.address?.longitude,
        status = status,
    )
    // Camera padding is held at the resting anchor, not the live sheet
    // position — see OrderMapBackdrop.
    val restingCover = LocalConfiguration.current.screenHeightDp.dp * SnapAnchor.Peek.coveredFraction

    SnapSheet(
        state = sheetState,
        backdrop = {
            if (showMap) {
                OrderMapBackdrop(
                    latitude = order.address!!.latitude!!,
                    longitude = order.address!!.longitude!!,
                    darkTheme = darkTheme,
                    sheetCoverHeight = restingCover,
                )
            } else {
                OrderMapPlaceholder()
            }
        },
        overlay = {
            // Controls live in the overlay rather than on the map so the back
            // button survives the sheet being dragged over the map entirely.
            Row(
                modifier = Modifier
                    .windowInsetsPadding(WindowInsets.statusBars)
                    .padding(start = Spacing.M, top = Spacing.S)
                    .align(Alignment.TopStart),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                MapOverlayButton(
                    icon = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.common_back),
                    onClick = onBack,
                )
                Spacer(Modifier.width(Spacing.XS))
                MapFocusToggle(sheetState = sheetState, onToggle = { target ->
                    scope.launch { sheetState.animateTo(target) }
                })
            }
            OrderFloatingMascot(
                status = status,
                sheetTopPx = { sheetState.sheetTopPx },
                contentScrollPx = { contentScroll.value.toFloat() },
                modifier = Modifier.align(Alignment.TopEnd),
            )
        },
    ) {
        OrderDetailSheetContent(
            order = order,
            status = status,
            scrollState = contentScroll,
            photosState = photosState,
            showCancel = showCancel,
            showReportIssue = showReportIssue,
            showRebook = showRebook,
            showMakeRecurring = showMakeRecurring,
            cancelEnabled = cancelEnabled,
            confirmingRecurring = confirmingRecurring,
            isDownloadingReceipt = isDownloadingReceipt,
            onCancel = onCancel,
            onReportIssue = onReportIssue,
            onRebook = onRebook,
            onMakeRecurring = onMakeRecurring,
            onLeaveReview = onLeaveReview,
            onDownloadReceipt = onDownloadReceipt,
            onViewPhotos = onViewPhotos,
            onConfirmRecurring = onConfirmRecurring,
        )
    }
}

/** Drops the panel to the map anchor, or brings it back to its resting one. */
@Composable
private fun MapFocusToggle(
    sheetState: SnapSheetState,
    onToggle: (SnapAnchor) -> Unit,
) {
    val atMapFocus = sheetState.targetAnchor == SnapAnchor.MapFocus
    MapOverlayButton(
        icon = if (atMapFocus) Icons.AutoMirrored.Outlined.ListAlt else Icons.Outlined.Map,
        contentDescription = stringResource(
            if (atMapFocus) R.string.order_detail_show_details else R.string.order_detail_show_map,
        ),
        onClick = { onToggle(if (atMapFocus) SnapAnchor.Peek else SnapAnchor.MapFocus) },
    )
}

/* ── Sheet content ── */

@Composable
private fun OrderDetailSheetContent(
    order: OrderDetailDto,
    status: OrderStatus?,
    scrollState: ScrollState,
    photosState: PhotosUiState,
    showCancel: Boolean,
    showReportIssue: Boolean,
    showRebook: Boolean,
    showMakeRecurring: Boolean,
    cancelEnabled: Boolean,
    confirmingRecurring: Boolean,
    isDownloadingReceipt: Boolean,
    onCancel: () -> Unit,
    onReportIssue: () -> Unit,
    onRebook: () -> Unit,
    onMakeRecurring: () -> Unit,
    onLeaveReview: () -> Unit,
    onDownloadReceipt: () -> Unit,
    onViewPhotos: () -> Unit,
    onConfirmRecurring: () -> Unit,
) {
    // Wave 3.3 — Pending recurring-template orders need an explicit customer
    // confirm step. Show the CTA when both conditions hold; everything else
    // is a no-op render (already-confirmed orders go through the standard
    // life-cycle UI).
    val showConfirmRecurringCta = !order.recurringTemplateId.isNullOrBlank() &&
        order.paymentStatus?.value == 1
    val hasFooter = showCancel || showReportIssue || showRebook || showMakeRecurring

    // Gesture-priority guard, the same one the partner sheet carries: once the customer has scrolled
    // INTO the sheet content, a vertical drag must keep scrolling that content rather than collapsing
    // the sheet. SnapSheet's own connection implements the stock Material hand-off — drag up expands the
    // sheet first, and only at the deepest anchor does the content move — and that hand-off does not
    // reliably win the race against the content's own scroll, which is what made the details unscrollable
    // on the customer side. Declared here so it sits CLOSER to the scroll source than the sheet's own
    // connection and therefore gets the delta first.
    val sheetGuard = remember(scrollState) {
        object : NestedScrollConnection {
            override fun onPreScroll(available: Offset, source: NestedScrollSource): Offset {
                if (source != NestedScrollSource.UserInput) return Offset.Zero
                val dy = available.y
                // Drag up (dy < 0): the reader wants to reveal more below. While the content can still
                // scroll down, consume the delta into the scroll state so the sheet doesn't expand past
                // its current snap point instead.
                if (dy < 0 && scrollState.value < scrollState.maxValue) {
                    return Offset(0f, -scrollState.dispatchRawDelta(-dy))
                }
                // Drag down (dy > 0): the reverse — scroll the content back to its top before the sheet
                // starts collapsing underneath it.
                if (dy > 0 && scrollState.value > 0) {
                    return Offset(0f, -scrollState.dispatchRawDelta(-dy))
                }
                return Offset.Zero
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .fillMaxHeight()
            .nestedScroll(sheetGuard),
    ) {
        SheetGrabber()

        // Identity, pinned: it does not scroll, so dragging the sheet down onto the map still leaves
        // the customer looking at which order this is. The mascot rides the sheet's top edge on the
        // RIGHT, so this row keeps its trailing half clear — that is why the date sits under the order
        // number rather than opposite it. The iOS twin is `OrderDetailCompactHeader`.
        OrderDetailCompactHeader(order = order)

        Column(
            modifier = Modifier
                .fillMaxWidth()
                // weight() is what keeps the footer below on screen: without
                // it the scroll column takes its content height and pushes the
                // footer past the bottom edge at every anchor but Expanded.
                .weight(1f, fill = true)
                .verticalScroll(scrollState)
                .padding(horizontal = Spacing.ML),
            verticalArrangement = Arrangement.spacedBy(Spacing.S),
        ) {
            // Hero block: the status headline sits directly on the tracker bar with no gap, so the two
            // read as one group — and the inner Column is why it overrides the parent's spacedBy.
            Column(verticalArrangement = Arrangement.spacedBy(0.dp)) {
                OrderStatusHero(order = order, status = status)
                OrderTrackerBar(status = status)
            }

            // Confirmation code and price. Everything that identifies the order is in the pinned
            // header above; this carries only what the header has no room for.
            OrderFactsStrip(order = order)

            // Sits right under the hero so it's the first thing the customer
            // sees after tapping the recurring-scheduled push.
            if (showConfirmRecurringCta) {
                ConfirmRecurringButton(
                    submitting = confirmingRecurring,
                    onClick = onConfirmRecurring,
                )
            }

            order.address?.let { AddressCard(it) }

            CleaningDetailsCard(order)

            if (!order.selectedServices.isNullOrEmpty()) {
                ServicesCard(order.selectedServices)
            }

            if (!order.selectedPackages.isNullOrEmpty()) {
                PackagesCard(order.selectedPackages)
            }

            val hasInstructions = !order.specialInstructions.isNullOrBlank() ||
                !order.accessInstructions.isNullOrBlank() ||
                !order.notes.isNullOrBlank()
            if (hasInstructions) {
                InstructionsCard(order)
            }

            // Photos summary — only renders when we have a Loaded response with a
            // non-empty photo list. Idle / Loading / Error all suppress the card so
            // the section doesn't flicker in before we know whether it's worth showing.
            (photosState as? PhotosUiState.Loaded)?.response?.takeIf { it.photos.isNotEmpty() }
                ?.let { resp ->
                    PhotosSection(response = resp, onViewPhotos = onViewPhotos)
                }

            if (!order.assignedEmployees.isNullOrEmpty()) {
                AssignedCleanersCard(order.assignedEmployees)
            }

            PriceBreakdownCard(order)

            if (!order.statusHistory.isNullOrEmpty()) {
                TimelineCard(order.statusHistory)
            }

            if (status == OrderStatus.Completed) {
                ReviewCard(order = order, onLeaveReview = onLeaveReview)
            }

            val showReceipt = !order.receiptNumber.isNullOrBlank() ||
                status == OrderStatus.Completed
            if (showReceipt) {
                ReceiptCard(
                    order = order,
                    onDownload = onDownloadReceipt,
                    isDownloading = isDownloadingReceipt,
                )
            }

            Spacer(Modifier.height(Spacing.XS))
        }

        if (hasFooter) {
            ActionsFooter(
                showCancel = showCancel,
                showReportIssue = showReportIssue,
                showRebook = showRebook,
                showMakeRecurring = showMakeRecurring,
                cancelEnabled = cancelEnabled,
                onCancel = onCancel,
                onReportIssue = onReportIssue,
                onRebook = onRebook,
                onMakeRecurring = onMakeRecurring,
            )
        }
    }
}

@Composable
private fun SheetGrabber() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp, bottom = 8.dp),
        contentAlignment = Alignment.TopCenter,
    ) {
        Box(
            modifier = Modifier
                .size(width = 36.dp, height = 4.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(MaterialTheme.colorScheme.outlineVariant),
        )
    }
}

@Composable
private fun ConfirmRecurringButton(submitting: Boolean, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        enabled = !submitting,
        modifier = Modifier
            .fillMaxWidth()
            .height(52.dp),
        shape = RoundedCornerShape(14.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = MaterialTheme.colorScheme.primary,
            contentColor = MaterialTheme.colorScheme.onPrimary,
        ),
    ) {
        if (submitting) {
            CircularProgressIndicator(
                modifier = Modifier.size(22.dp),
                color = MaterialTheme.colorScheme.onPrimary,
                strokeWidth = 2.dp,
            )
        } else {
            Text(
                text = stringResource(R.string.recurring_confirm_cta),
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.SemiBold,
            )
        }
    }
}

/* ── Actions footer ── */

/**
 * Footer that hosts the Cancel + Report Issue actions. Both buttons are
 * full-width; when both are visible they stack vertically with Cancel on
 * top (destructive intent gets the prime real-estate) and Report Issue
 * below (constructive secondary). Sits on a filled surface so the borders
 * read cleanly regardless of the page bg tint, and respects the system nav
 * inset so it isn't clipped on gesture-bar devices.
 */
@Composable
private fun ActionsFooter(
    showCancel: Boolean,
    showReportIssue: Boolean,
    showRebook: Boolean,
    showMakeRecurring: Boolean,
    cancelEnabled: Boolean,
    onCancel: () -> Unit,
    onReportIssue: () -> Unit,
    onRebook: () -> Unit,
    onMakeRecurring: () -> Unit,
) {
    Surface(
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 0.dp,
        shadowElevation = 8.dp,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(horizontal = 16.dp, vertical = 12.dp),
        ) {
            // Wave 3 — "Book again" sits on top as the primary CTA when shown.
            // Status gating in the parent guarantees showRebook is only true
            // when status == Completed; cancel + rebook never coexist.
            if (showRebook) {
                Button(
                    onClick = onRebook,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = MaterialTheme.colorScheme.primary,
                        contentColor = MaterialTheme.colorScheme.onPrimary,
                    ),
                ) {
                    Icon(
                        Icons.Outlined.Refresh,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp),
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.order_action_rebook),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }

            if (showRebook && (showCancel || showReportIssue || showMakeRecurring)) {
                Spacer(Modifier.height(8.dp))
            }

            // PA14 Path B — sits between Rebook (primary) and the
            // outlined Cancel/Report buttons. Outlined-secondary style so
            // it doesn't compete with Rebook.
            if (showMakeRecurring) {
                OutlinedButton(
                    onClick = onMakeRecurring,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.primary),
                    colors = ButtonDefaults.outlinedButtonColors(
                        contentColor = MaterialTheme.colorScheme.primary,
                    ),
                ) {
                    Icon(
                        Icons.Outlined.CalendarMonth,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp),
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.order_action_make_recurring),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
                if (showCancel || showReportIssue) {
                    Spacer(Modifier.height(8.dp))
                }
            }

            if (showCancel) {
                OutlinedButton(
                    onClick = onCancel,
                    enabled = cancelEnabled,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    border = BorderStroke(
                        1.dp,
                        MaterialTheme.colorScheme.error,
                    ),
                    colors = ButtonDefaults.outlinedButtonColors(
                        contentColor = MaterialTheme.colorScheme.error,
                    ),
                ) {
                    Icon(
                        Icons.Outlined.Cancel,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp),
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.order_action_cancel),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }

            if (showCancel && showReportIssue) {
                Spacer(Modifier.height(8.dp))
            }

            // Error-tinted by owner decision although reporting destroys nothing.
            // It borrows the error PALETTE, deliberately not
            // CleansiaDestructiveButton: that component fills a fixed red
            // container, which on a completed order would out-shout the filled
            // primary "Book again" above it — the exact rank inversion its own
            // doc was written to prevent — and iOS has no filled sibling to match
            // it with. Staying outlined also keeps the red confined to a 1dp
            // stroke plus the glyph and label, so red-300 in dark mode never
            // paints an area.
            if (showReportIssue) {
                OutlinedButton(
                    onClick = onReportIssue,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    border = BorderStroke(
                        1.dp,
                        MaterialTheme.colorScheme.error,
                    ),
                    colors = ButtonDefaults.outlinedButtonColors(
                        contentColor = MaterialTheme.colorScheme.error,
                    ),
                ) {
                    Icon(
                        Icons.Outlined.ReportProblem,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp),
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.order_action_report_issue),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }
        }
    }
}

/* ── States ── */

@Composable
private fun LoadingState(onBack: () -> Unit) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        CircularProgressIndicator(
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.align(Alignment.Center),
        )
        // The map layout has no top bar, so a slow fetch would otherwise leave
        // the user on a bare spinner with only the system gesture to leave on.
        MapOverlayButton(
            icon = Icons.AutoMirrored.Outlined.ArrowBack,
            contentDescription = stringResource(R.string.common_back),
            onClick = onBack,
            modifier = Modifier
                .windowInsetsPadding(WindowInsets.statusBars)
                .padding(start = Spacing.M, top = Spacing.S)
                .align(Alignment.TopStart),
        )
    }
}

/* ── Shared building blocks ── */

@Composable
internal fun Card(content: @Composable () -> Unit) {
    // Flat, exactly like the partner sheet's `OrderSectionCard`: a surface-coloured block with no
    // border and no elevation. The 1dp outline this used to carry drew a hard edge around every
    // section, so a sheet of six cards read as six boxes stacked on a page rather than one panel.
    // The section divider under each title is what separates them now.
    // No horizontal inset. The scroll column already insets everything by Spacing.ML, the same as the
    // pinned header, so a further 16dp here started every section's text 16dp to the right of the
    // order number it belongs under. With the card flat there is no container edge for an inset to
    // hold away from anyway — only the vertical padding, which is what separates one section from the
    // next.
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface)
            .padding(vertical = 16.dp),
    ) { content() }
}

@Composable
internal fun SectionHeader(
    title: String,
    icon: (@Composable () -> Unit)? = null,
) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        if (icon != null) {
            icon()
            Spacer(Modifier.width(8.dp))
        }
        Text(
            text = title,
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
        )
    }
    // The rule that separates a section from its content now that the cards carry no border —
    // the partner sheet's `OrderSectionCard` spacing, to the pixel.
    Spacer(Modifier.height(8.dp))
    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
    Spacer(Modifier.height(12.dp))
}

@Composable
internal fun InfoRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/* ── Previews ── */

/**
 * The sheet content at each anchor height, at the owner's 411dp reference
 * width. Heights are `SnapAnchor.coveredFraction * 891dp` — a Pixel-class
 * display — so the map-focus preview shows exactly what survives when the
 * panel is dragged out of the way.
 */
private val previewOrder = OrderDetailDto(
    id = "order-1",
    displayOrderNumber = "10427",
    address = OrderAddressDto(
        street = "Vinohradská 1511/230",
        city = "Praha 3",
        zipCode = "130 00",
        country = "Česko",
        latitude = 50.0779,
        longitude = 14.4680,
    ),
    rooms = 3,
    bathrooms = 1,
    cleaningDateTime = "2026-08-14T09:00:00Z",
    paymentType = CodeDto(type = "PaymentType", name = "Card", value = 2),
    paymentStatus = CodeDto(type = "PaymentStatus", name = "Paid", value = 2),
    totalPrice = 2150.0,
    originalSubtotal = 2500.0,
    appliedDiscountSource = 2,
    membershipDiscountAmount = 350.0,
    estimatedTime = 180,
    orderStatus = CodeDto(type = "OrderStatus", name = "InProgress", value = 4),
    confirmationCode = "CLS-4417",
    currency = OrderCurrencyDetailDto(code = "CZK", symbol = "Kč"),
    statusHistory = listOf(
        OrderStatusTrackDto(
            status = CodeDto(type = "OrderStatus", name = "InProgress", value = 4),
            createdOn = "2026-08-14T09:05:00Z",
        ),
    ),
)

@Composable
private fun PreviewSheet() {
    CleansiaTheme {
        Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.surface) {
            OrderDetailSheetContent(
                order = previewOrder,
                status = OrderStatus.InProgress,
                scrollState = rememberScrollState(),
                photosState = PhotosUiState.Idle,
                showCancel = false,
                showReportIssue = true,
                showRebook = false,
                showMakeRecurring = false,
                cancelEnabled = true,
                confirmingRecurring = false,
                isDownloadingReceipt = false,
                onCancel = {},
                onReportIssue = {},
                onRebook = {},
                onMakeRecurring = {},
                onLeaveReview = {},
                onDownloadReceipt = {},
                onViewPhotos = {},
                onConfirmRecurring = {},
            )
        }
    }
}

@Preview(name = "Map focus · ru", locale = "ru", widthDp = 411, heightDp = 267)
@Composable
private fun SheetMapFocusRuPreview() = PreviewSheet()

@Preview(name = "Peek · ru", locale = "ru", widthDp = 411, heightDp = 668)
@Composable
private fun SheetPeekRuPreview() = PreviewSheet()

@Preview(name = "Expanded · ru", locale = "ru", widthDp = 411, heightDp = 846)
@Composable
private fun SheetExpandedRuPreview() = PreviewSheet()

@Preview(name = "Map focus · uk", locale = "uk", widthDp = 411, heightDp = 267)
@Composable
private fun SheetMapFocusUkPreview() = PreviewSheet()

@Preview(name = "Peek · uk", locale = "uk", widthDp = 411, heightDp = 668)
@Composable
private fun SheetPeekUkPreview() = PreviewSheet()

@Preview(name = "Expanded · uk", locale = "uk", widthDp = 411, heightDp = 846)
@Composable
private fun SheetExpandedUkPreview() = PreviewSheet()
