package cz.cleansia.customer.features.orders

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.Cancel
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.ReportProblem
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.ReceiptOpenResult
import cz.cleansia.customer.core.orders.openReceiptPdf
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.ui.theme.Poppins

/**
 * Order detail screen — shows the real DTO loaded by [OrderDetailViewModel].
 *
 * Wave 1 surface is read-only: the `onRebook`, `onReportIssue`,
 * `onDownloadReceipt` callbacks are preserved on the signature because the
 * nav host still passes them, but nothing invokes them yet. Wave 2 will
 * wire up the footer actions.
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
     * timeOfDay. Only shown when the order is Completed AND the user has
     * an active Plus membership (recurring is a Plus perk).
     */
    onMakeRecurring: (orderId: String) -> Unit = {},
    @Suppress("UNUSED_PARAMETER") onDownloadReceipt: () -> Unit = {},
    onViewPhotos: () -> Unit = {},
    viewModel: OrderDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    // Wave 4 — single ActionState replaces (cancelling, cancelError) etc.
    // The screen still derives the same boolean / message values from the
    // sealed variant; sheets receive those derived bits via their existing
    // params, keeping their composables unchanged.
    val cancelState by viewModel.cancelState.collectAsStateWithLifecycle()
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

    // Local sheet visibility — lifted above the Scaffold so the footer button
    // and the observed success flow can both drive it.
    var showCancelSheet by remember { mutableStateOf(false) }
    var showReviewSheet by remember { mutableStateOf(false) }

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
                        environment = if (cz.cleansia.customer.BuildConfig.DEBUG) {
                            com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration.Environment.Test
                        } else {
                            com.stripe.android.paymentsheet.PaymentSheet.GooglePayConfiguration.Environment.Production
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

    val title = when (val s = state) {
        is OrderDetailUiState.Loaded -> s.order.displayOrderNumber?.let { "#$it" } ?: "—"
        else -> ""
    }

    // Figure out whether the cancel footer should be visible. Only the Loaded
    // branch has an order to pull the status from; the other branches hide it.
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

    // PA14 Path B — "Make this recurring". Same Completed-only gate as
    // rebook, plus an active-Plus-membership gate (recurring is a Plus
    // perk). Membership state is read through the VM, which exposes the
    // singleton repo via Hilt — no EntryPointAccessors detour.
    val membership by viewModel.membershipRepository.current.collectAsStateWithLifecycle(initialValue = null)
    val canMakeRecurring = canRebook && membership?.hasMembership == true

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        title,
                        style = MaterialTheme.typography.titleMedium.copy(
                            fontFamily = Poppins,
                            fontWeight = FontWeight.SemiBold,
                        ),
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.common_back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                ),
            )
        },
        bottomBar = {
            // Footer rules:
            //  - Cancellable + reportable (Confirmed): stack Cancel on top
            //    and Report issue below.
            //  - Cancellable only (New / Pending): just Cancel.
            //  - Reportable only (InProgress): just Report issue.
            //  - Completed: Book again (primary) on top, Report issue below.
            //  - Neither (Cancelled, or Loading/Error): no footer.
            if (isCancellable || canReportIssue || canRebook || canMakeRecurring) {
                val orderId = (state as? OrderDetailUiState.Loaded)?.order?.id
                ActionsFooter(
                    showCancel = isCancellable,
                    showReportIssue = canReportIssue,
                    showRebook = canRebook,
                    showMakeRecurring = canMakeRecurring,
                    cancelEnabled = !cancelling,
                    onCancel = { showCancelSheet = true },
                    onReportIssue = onReportIssue,
                    onRebook = onRebook,
                    onMakeRecurring = { orderId?.let(onMakeRecurring) },
                )
            }
        },
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when (val s = state) {
                is OrderDetailUiState.Loading -> LoadingState()
                is OrderDetailUiState.Error -> ErrorState(
                    canRetry = s.canRetry,
                    onRetry = viewModel::refresh,
                    onBack = onBack,
                )
                is OrderDetailUiState.Loaded -> {
                    // Kick off the secondary photos fetch once the main detail is
                    // resolved. Safe on recomposition — VM guards with its own
                    // Idle/Error check so this is effectively one-shot.
                    LaunchedEffect(s.order.id) { viewModel.ensurePhotosLoaded() }
                    LoadedState(
                        order = s.order,
                        onLeaveReview = { showReviewSheet = true },
                        onDownloadReceipt = { viewModel.downloadReceipt() },
                        isDownloadingReceipt = downloadingReceipt,
                        photosState = photosState,
                        onViewPhotos = onViewPhotos,
                        onConfirmRecurring = { viewModel.confirmRecurring() },
                        confirmingRecurring = confirmingRecurring,
                    )
                }
            }
        }
    }

    // Render the sheet on top of the scaffold. Only Loaded state can open it
    // (isCancellable gates the footer button); guarding here keeps the render
    // defensive in case state flips mid-cancel.
    if (showCancelSheet && loaded != null) {
        CancelOrderSheet(
            order = loaded.order,
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
            onConfirm = { rating, comment ->
                viewModel.submitReview(rating, comment, isEdit = currentReview != null)
            },
            isSubmitting = submittingReview,
            errorMessage = reviewError,
            existingReview = currentReview,
        )
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

            if (showReportIssue) {
                OutlinedButton(
                    onClick = onReportIssue,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    border = BorderStroke(
                        1.dp,
                        MaterialTheme.colorScheme.primary,
                    ),
                    colors = ButtonDefaults.outlinedButtonColors(
                        contentColor = MaterialTheme.colorScheme.primary,
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
private fun LoadingState() {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

@Composable
private fun ErrorState(
    canRetry: Boolean,
    onRetry: () -> Unit,
    onBack: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.order_detail_error_title),
            style = MaterialTheme.typography.titleMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.SemiBold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = stringResource(R.string.order_detail_error_message),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        if (canRetry) {
            CleansiaPrimaryButton(
                text = stringResource(R.string.order_detail_error_retry),
                onClick = onRetry,
            )
            Spacer(Modifier.height(8.dp))
        }
        Text(
            text = stringResource(R.string.common_back),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onBack)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

/* ── Loaded layout ── */

@Composable
private fun LoadedState(
    order: OrderDetailDto,
    onLeaveReview: () -> Unit,
    onDownloadReceipt: () -> Unit,
    isDownloadingReceipt: Boolean,
    photosState: PhotosUiState,
    onViewPhotos: () -> Unit,
    onConfirmRecurring: () -> Unit,
    confirmingRecurring: Boolean,
) {
    val currentStatus = orderStatusFromValue(order.orderStatus?.value)
    // Wave 3.3 — Pending recurring-template orders need an explicit customer
    // confirm step. Show the CTA when both conditions hold; everything else
    // is a no-op render (already-confirmed orders go through the standard
    // life-cycle UI).
    val showConfirmRecurringCta = !order.recurringTemplateId.isNullOrBlank()
        && order.paymentStatus?.value == 1 // PaymentStatus.Pending — 0 = Pending was wrong; backend enum is 1-indexed

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp),
    ) {
        Spacer(Modifier.height(8.dp))

        // For active orders, swap the static HeroCard for the LiveProgressHero,
        // which embeds the mascot, status pill, contextual headline, optional
        // progress bar, and step indicator. Falls back to the original card for
        // terminal states (Completed, Cancelled) and the pre-acceptance phase
        // (New, Pending) — those benefit from the simpler "facts only" layout.
        if (currentStatus == OrderStatus.Confirmed
            || currentStatus == OrderStatus.OnTheWay
            || currentStatus == OrderStatus.InProgress) {
            LiveProgressHero(order)
        } else {
            HeroCard(order)
        }

        // Confirm CTA sits right under the hero so it's the first thing the
        // customer sees after tapping the recurring-scheduled push. Hidden
        // for non-recurring or already-confirmed orders so existing flows
        // don't grow a redundant button.
        if (showConfirmRecurringCta) {
            Spacer(Modifier.height(12.dp))
            Button(
                onClick = onConfirmRecurring,
                enabled = !confirmingRecurring,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(52.dp),
                shape = RoundedCornerShape(14.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
            ) {
                if (confirmingRecurring) {
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

        order.address?.let {
            Spacer(Modifier.height(12.dp))
            AddressCard(it)
        }

        Spacer(Modifier.height(12.dp))
        CleaningDetailsCard(order)

        if (!order.selectedServices.isNullOrEmpty()) {
            Spacer(Modifier.height(12.dp))
            ServicesCard(order.selectedServices)
        }

        if (!order.selectedPackages.isNullOrEmpty()) {
            Spacer(Modifier.height(12.dp))
            PackagesCard(order.selectedPackages)
        }

        val hasInstructions = !order.specialInstructions.isNullOrBlank() ||
            !order.accessInstructions.isNullOrBlank() ||
            !order.notes.isNullOrBlank()
        if (hasInstructions) {
            Spacer(Modifier.height(12.dp))
            InstructionsCard(order)
        }

        // Photos summary — only renders when we have a Loaded response with a
        // non-empty photo list. Idle / Loading / Error all suppress the card so
        // the section doesn't flicker in before we know whether it's worth showing.
        (photosState as? PhotosUiState.Loaded)?.response?.takeIf { it.photos.isNotEmpty() }
            ?.let { resp ->
                Spacer(Modifier.height(12.dp))
                PhotosSection(response = resp, onViewPhotos = onViewPhotos)
            }

        if (!order.assignedEmployees.isNullOrEmpty()) {
            Spacer(Modifier.height(12.dp))
            AssignedCleanersCard(order.assignedEmployees)
        }

        if (!order.statusHistory.isNullOrEmpty()) {
            Spacer(Modifier.height(12.dp))
            TimelineCard(order.statusHistory)
        }

        if (currentStatus == OrderStatus.Completed) {
            Spacer(Modifier.height(12.dp))
            ReviewCard(order = order, onLeaveReview = onLeaveReview)
        }

        val showReceipt = !order.receiptNumber.isNullOrBlank() || currentStatus == OrderStatus.Completed
        if (showReceipt) {
            Spacer(Modifier.height(12.dp))
            ReceiptCard(
                order = order,
                onDownload = onDownloadReceipt,
                isDownloading = isDownloadingReceipt,
            )
        }

        // Footer actions live in the Scaffold's bottomBar (cancel — Wave 2 Phase 2).
        // Rebook / report-issue / download-receipt callbacks are preserved on the
        // screen signature for Wave 2 Phases 3–6. Pad for breathing room above
        // the bottom bar.
        Spacer(Modifier.height(48.dp))
    }
}

/* ── Shared building blocks ── */

@Composable
internal fun Card(content: @Composable () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(16.dp),
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
