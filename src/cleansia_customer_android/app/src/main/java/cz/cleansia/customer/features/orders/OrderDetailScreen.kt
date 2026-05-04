package cz.cleansia.customer.features.orders

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
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
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.ReportProblem
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.format.formatOrderDateRange
import cz.cleansia.customer.core.format.formatOrderDateTime
import cz.cleansia.customer.core.format.formatOrderPrice
import cz.cleansia.customer.core.format.orderStatusColor
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import coil3.compose.AsyncImage
import cz.cleansia.customer.core.orders.AssignedEmployeeDto
import cz.cleansia.customer.core.orders.OrderAddressDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPackageDetailsDto
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.customer.core.orders.OrderServiceDetailsDto
import cz.cleansia.customer.core.orders.OrderStatusTrackDto
import cz.cleansia.customer.core.orders.ReceiptOpenResult
import cz.cleansia.customer.core.orders.openReceiptPdf
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.theme.Poppins
import cz.cleansia.customer.ui.theme.WarningStar

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
    val cancelling by viewModel.cancelling.collectAsStateWithLifecycle()
    val cancelError by viewModel.cancelError.collectAsStateWithLifecycle()
    val submittingReview by viewModel.submittingReview.collectAsStateWithLifecycle()
    val reviewError by viewModel.reviewError.collectAsStateWithLifecycle()
    val downloadingReceipt by viewModel.downloadingReceipt.collectAsStateWithLifecycle()
    val photosState by viewModel.photos.collectAsStateWithLifecycle()

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
        status == OrderStatus.InProgress ||
        status == OrderStatus.Completed
    // Wave 3 Phase R1 — "Book again" is only useful for a finished cleaning.
    // Hidden for everything else; the user can still navigate back to Home
    // and tap the FAB to start a fresh booking.
    val canRebook = status == OrderStatus.Completed

    // PA14 Path B — "Make this recurring". Same Completed-only gate as
    // rebook, plus an active-Plus-membership gate (recurring is a Plus
    // perk). Membership state is read from the singleton repo via Hilt
    // entry point — no need to wire through the VM.
    val membershipRepo = remember {
        dagger.hilt.android.EntryPointAccessors
            .fromApplication(context, OrderDetailMembershipEntryPoint::class.java)
            .membershipRepository()
    }
    val membership by membershipRepo.current.collectAsStateWithLifecycle(initialValue = null)
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
) {
    val currentStatus = orderStatusFromValue(order.orderStatus?.value)

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
        if (currentStatus == OrderStatus.Confirmed || currentStatus == OrderStatus.InProgress) {
            LiveProgressHero(order)
        } else {
            HeroCard(order)
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

/* ── Hero ── */

@Composable
private fun HeroCard(order: OrderDetailDto) {
    Card {
        Row(verticalAlignment = Alignment.CenterVertically) {
            StatusPill(
                label = order.orderStatus?.name ?: "—",
                color = orderStatusColor(order.orderStatus?.value),
            )
            Spacer(Modifier.weight(1f))
            order.confirmationCode?.takeIf { it.isNotBlank() }?.let { code ->
                Column(horizontalAlignment = Alignment.End) {
                    Text(
                        stringResource(R.string.order_detail_code_label),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        code,
                        style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }
        Spacer(Modifier.height(12.dp))
        Text(
            text = formatOrderDateRange(
                iso = order.cleaningDateTime,
                estimatedMinutes = order.estimatedTime,
            ),
            style = MaterialTheme.typography.titleLarge.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.Bold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = formatOrderPrice(order.totalPrice, order.currency?.code),
            style = MaterialTheme.typography.headlineMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.Bold,
            ),
            color = MaterialTheme.colorScheme.primary,
        )
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

/* ── Address ── */

@Composable
private fun AddressCard(address: OrderAddressDto) {
    val cityZip = buildString {
        address.zipCode?.takeIf { it.isNotBlank() }?.let { append(it) }
        address.city?.takeIf { it.isNotBlank() }?.let {
            if (isNotEmpty()) append(' ')
            append(it)
        }
    }
    Card {
        SectionHeader(
            icon = {
                Icon(
                    Icons.Outlined.LocationOn,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(18.dp),
                )
            },
            title = stringResource(R.string.order_detail_address),
        )
        Spacer(Modifier.height(6.dp))
        Text(
            text = address.street ?: "—",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurface,
        )
        if (cityZip.isNotBlank()) {
            Text(
                text = cityZip,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        address.country?.takeIf { it.isNotBlank() }?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/* ── Cleaning details ── */

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun CleaningDetailsCard(order: OrderDetailDto) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_section_details))
        Spacer(Modifier.height(8.dp))

        // Rooms + bathrooms (backend always returns 0 if unset — show anyway).
        InfoRow(
            label = stringResource(R.string.order_detail_rooms),
            value = stringResource(
                R.string.order_detail_rooms_bathrooms,
                order.rooms,
                order.bathrooms,
            ),
        )
        Spacer(Modifier.height(6.dp))
        InfoRow(
            label = stringResource(R.string.order_detail_estimated),
            value = if (order.estimatedTime > 0) {
                stringResource(R.string.order_detail_duration_minutes, order.estimatedTime)
            } else {
                "—"
            },
        )

        // Extras — only those flagged `true` in the map. Skip block entirely if none.
        val activeExtras = order.extras.orEmpty().filter { it.value }.keys.toList()
        if (activeExtras.isNotEmpty()) {
            Spacer(Modifier.height(10.dp))
            Text(
                stringResource(R.string.order_detail_extras),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                activeExtras.forEach { key ->
                    Text(
                        text = prettifyExtraKey(key),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier
                            .padding(vertical = 3.dp)
                            .background(
                                MaterialTheme.colorScheme.surfaceVariant,
                                RoundedCornerShape(999.dp),
                            )
                            .padding(horizontal = 10.dp, vertical = 4.dp),
                    )
                }
            }
        }
    }
}

/**
 * Turn an extras-map key like `eco_products` or `stainRemoval` into a
 * readable label ("Eco Products" / "Stain Removal"). Fallback only — backend
 * may later localise these and send a display name.
 */
private fun prettifyExtraKey(key: String): String {
    if (key.isBlank()) return key
    // Split camelCase + snake/kebab into words, then title-case each.
    val spaced = key
        .replace('_', ' ')
        .replace('-', ' ')
        .replace(Regex("([a-z])([A-Z])"), "$1 $2")
    return spaced.split(' ').joinToString(" ") { word ->
        word.lowercase().replaceFirstChar { c -> c.titlecase() }
    }
}

/* ── Services ── */

@Composable
private fun ServicesCard(services: List<OrderServiceDetailsDto>) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_services_header))
        Spacer(Modifier.height(6.dp))
        services.forEachIndexed { idx, svc ->
            if (idx > 0) {
                Spacer(Modifier.height(6.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(6.dp))
            }
            Row(verticalAlignment = Alignment.Top) {
                Column(Modifier.weight(1f)) {
                    Text(
                        text = svc.name ?: "—",
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    svc.description?.takeIf { it.isNotBlank() }?.let { desc ->
                        Text(
                            text = desc,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 2,
                        )
                    }
                }
                if (svc.estimatedTime > 0) {
                    Spacer(Modifier.width(8.dp))
                    TimeChip(minutes = svc.estimatedTime)
                }
            }
        }
    }
}

@Composable
private fun TimeChip(minutes: Int) {
    Text(
        text = stringResource(R.string.order_detail_duration_minutes, minutes),
        style = MaterialTheme.typography.labelSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier
            .background(
                MaterialTheme.colorScheme.surfaceVariant,
                RoundedCornerShape(999.dp),
            )
            .padding(horizontal = 10.dp, vertical = 4.dp),
    )
}

/* ── Packages ── */

@Composable
private fun PackagesCard(packages: List<OrderPackageDetailsDto>) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_packages_header))
        Spacer(Modifier.height(6.dp))
        packages.forEachIndexed { idx, pkg ->
            if (idx > 0) {
                Spacer(Modifier.height(6.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(6.dp))
            }
            Row(verticalAlignment = Alignment.Top) {
                Column(Modifier.weight(1f)) {
                    Text(
                        text = pkg.name ?: "—",
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    pkg.description?.takeIf { it.isNotBlank() }?.let { desc ->
                        Text(
                            text = desc,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 2,
                        )
                    }
                    pkg.includedServices?.takeIf { it.isNotEmpty() }?.let { included ->
                        Spacer(Modifier.height(2.dp))
                        Text(
                            text = included.joinToString(", "),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                Spacer(Modifier.width(8.dp))
                Text(
                    text = formatOrderPrice(pkg.price, pkg.currencyCode),
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onBackground,
                )
            }
        }
    }
}

/* ── Instructions ── */

@Composable
private fun InstructionsCard(order: OrderDetailDto) {
    val blocks = listOfNotNull(
        order.specialInstructions?.takeIf { it.isNotBlank() }
            ?.let { stringResource(R.string.order_detail_special_instructions) to it },
        order.accessInstructions?.takeIf { it.isNotBlank() }
            ?.let { stringResource(R.string.order_detail_access_instructions) to it },
        order.notes?.takeIf { it.isNotBlank() }
            ?.let { stringResource(R.string.order_detail_notes) to it },
    )
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_instructions))
        Spacer(Modifier.height(6.dp))
        blocks.forEachIndexed { idx, (label, text) ->
            if (idx > 0) {
                Spacer(Modifier.height(8.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                Spacer(Modifier.height(8.dp))
            }
            Text(
                text = label,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = text,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}

/* ── Assigned cleaners ── */

@Composable
private fun AssignedCleanersCard(employees: List<AssignedEmployeeDto>) {
    val context = LocalContext.current
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_cleaners))
        Spacer(Modifier.height(8.dp))
        employees.forEachIndexed { idx, emp ->
            if (idx > 0) Spacer(Modifier.height(10.dp))
            val displayName = emp.fullName?.takeIf { it.isNotBlank() }
                ?: stringResource(R.string.order_detail_cleaner_fallback)
            val initial = displayName.firstOrNull()?.uppercaseChar()?.toString().orEmpty()
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = initial,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        text = displayName,
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    emp.phoneNumber?.takeIf { it.isNotBlank() }?.let { phone ->
                        Text(
                            text = phone,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                emp.phoneNumber?.takeIf { it.isNotBlank() }?.let { phone ->
                    Box(
                        modifier = Modifier
                            .size(36.dp)
                            .clip(CircleShape)
                            .background(MaterialTheme.colorScheme.primaryContainer)
                            .clickable {
                                val intent = Intent(Intent.ACTION_DIAL, Uri.parse("tel:$phone"))
                                // Wrap in try/catch — some devices (e.g. tablets without a dialer)
                                // can throw ActivityNotFoundException. We silently no-op on failure
                                // since this is a Wave 1 convenience tap; Wave 2 may surface a snackbar.
                                runCatching {
                                    ContextCompat.startActivity(context, intent, null)
                                }
                            },
                        contentAlignment = Alignment.Center,
                    ) {
                        Icon(
                            Icons.Outlined.Phone,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(18.dp),
                        )
                    }
                }
            }
        }
    }
}

/* ── Timeline ── */

@Composable
private fun TimelineCard(history: List<OrderStatusTrackDto>) {
    // Sort ascending (oldest first). Null createdOn sorts to the bottom so it
    // stays visible; the formatter will render "—" for such rows.
    val sorted = history.sortedWith(
        compareBy(nullsLast()) { it.createdOn },
    )
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_timeline))
        Spacer(Modifier.height(10.dp))
        sorted.forEachIndexed { idx, entry ->
            TimelineRow(entry, isLast = idx == sorted.lastIndex)
        }
    }
}

@Composable
private fun TimelineRow(entry: OrderStatusTrackDto, isLast: Boolean) {
    val dotColor = orderStatusColor(entry.status?.value)
    Row(Modifier.padding(vertical = 4.dp)) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(
                Modifier
                    .size(12.dp)
                    .background(dotColor, CircleShape),
            )
            if (!isLast) {
                Box(
                    Modifier
                        .width(2.dp)
                        .height(28.dp)
                        .background(MaterialTheme.colorScheme.outlineVariant),
                )
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.padding(bottom = if (isLast) 0.dp else 4.dp)) {
            Text(
                text = entry.status?.name ?: "—",
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = formatOrderDateTime(entry.createdOn),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/* ── Review ── */

@Composable
private fun ReviewCard(
    order: OrderDetailDto,
    onLeaveReview: () -> Unit,
) {
    Card {
        SectionHeader(title = stringResource(R.string.order_detail_your_review))
        Spacer(Modifier.height(8.dp))
        val review = order.review
        if (review != null) {
            Row {
                repeat(5) { idx ->
                    Icon(
                        Icons.Outlined.Star,
                        contentDescription = null,
                        tint = if (idx < review.rating) WarningStar else MaterialTheme.colorScheme.outlineVariant,
                        modifier = Modifier.size(22.dp),
                    )
                }
            }
            review.comment?.takeIf { it.isNotBlank() }?.let { comment ->
                Spacer(Modifier.height(6.dp))
                Text(
                    text = "“$comment”",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(10.dp))
            // Wave 3 Phase R2 — "Edit review" CTA. Reuses the same sheet via the
            // shared onLeaveReview callback; the parent wires existingReview so
            // the sheet enters edit mode.
            OutlinedButton(
                onClick = onLeaveReview,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(44.dp),
                shape = CircleShape,
            ) {
                Text(
                    text = stringResource(R.string.order_review_edit_action),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        } else {
            // Wave 2 Phase 3 — the CTA is live. Filled primary button, full-
            // width, opens the SubmitReviewSheet via the parent's callback.
            Button(
                onClick = onLeaveReview,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp),
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
            ) {
                Text(
                    text = stringResource(R.string.order_detail_leave_review),
                    style = MaterialTheme.typography.titleMedium,
                )
            }
        }
    }
}

/* ── Receipt ── */

/**
 * Download-receipt card. Active when `order.receiptNumber` is non-blank; shows
 * a disabled button + "not ready yet" caption otherwise so the card can still
 * render for Completed orders whose backend receipt number hasn't caught up.
 *
 * Loading pattern mirrors the Phase 2 cancel button — inline spinner replaces
 * the button label while the download is in flight.
 */
@Composable
private fun ReceiptCard(
    order: OrderDetailDto,
    onDownload: () -> Unit,
    isDownloading: Boolean,
) {
    val hasReceipt = !order.receiptNumber.isNullOrBlank()
    Card {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                Icons.Outlined.Description,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.order_detail_download_receipt),
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                order.receiptNumber?.takeIf { it.isNotBlank() }?.let { num ->
                    Text(
                        text = num,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            Spacer(Modifier.width(8.dp))
            Button(
                onClick = onDownload,
                enabled = hasReceipt && !isDownloading,
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
                modifier = Modifier.height(40.dp),
            ) {
                if (isDownloading) {
                    CircularProgressIndicator(
                        color = MaterialTheme.colorScheme.onPrimary,
                        strokeWidth = 2.dp,
                        modifier = Modifier.size(16.dp),
                    )
                } else {
                    Text(
                        text = stringResource(R.string.order_detail_download_receipt),
                        style = MaterialTheme.typography.labelLarge,
                    )
                }
            }
        }
        if (!hasReceipt) {
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.order_receipt_not_ready),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/* ── Photos (Wave 2 Phase 5) ── */

/**
 * Summary card rendered on the detail screen when the order has photos.
 * Shows a Before/After count pill row and up to 6 thumbnail previews; the
 * entire card is tappable and delegates navigation to [onViewPhotos].
 *
 * `photoType` is serialized as an Int (1 = Before, 2 = After); anything
 * null/unknown is bucketed under Before on the gallery itself, but here we
 * trust the backend's `beforePhotoCount` / `afterPhotoCount` fields which
 * mirror that convention.
 */
@Composable
private fun PhotosSection(
    response: OrderPhotosResponse,
    onViewPhotos: () -> Unit,
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .clickable(onClick = onViewPhotos),
    ) {
        Card {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = stringResource(R.string.order_photos_section_title),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onBackground,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    text = stringResource(R.string.order_photos_view_button),
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.width(4.dp))
                Icon(
                    Icons.AutoMirrored.Outlined.ArrowForward,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(16.dp),
                )
            }
            Spacer(Modifier.height(10.dp))
            Row {
                PhotoCountPill(
                    text = stringResource(R.string.order_photos_summary_before, response.beforePhotoCount),
                )
                Spacer(Modifier.width(8.dp))
                PhotoCountPill(
                    text = stringResource(R.string.order_photos_summary_after, response.afterPhotoCount),
                )
            }
            Spacer(Modifier.height(12.dp))
            val previewThumbs = response.photos.take(6)
            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(previewThumbs) { photo ->
                    PhotoThumb(url = photo.blobUrl, size = 72.dp)
                }
            }
        }
    }
}

@Composable
private fun PhotoCountPill(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier
            .background(
                MaterialTheme.colorScheme.surfaceVariant,
                RoundedCornerShape(999.dp),
            )
            .padding(horizontal = 10.dp, vertical = 4.dp),
    )
}

@Composable
private fun PhotoThumb(url: String?, size: Dp) {
    Box(
        modifier = Modifier
            .size(size)
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant),
    ) {
        AsyncImage(
            model = url,
            contentDescription = null,
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize(),
        )
    }
}

/* ── Shared building blocks ── */

@Composable
private fun Card(content: @Composable () -> Unit) {
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
private fun SectionHeader(
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
private fun InfoRow(label: String, value: String) {
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
