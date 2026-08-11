package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.BottomSheetScaffold
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SheetValue
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberBottomSheetScaffoldState
import androidx.compose.material3.rememberStandardBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.nestedscroll.NestedScrollConnection
import androidx.compose.ui.input.nestedscroll.NestedScrollSource
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.mapbox.geojson.Point
import com.mapbox.maps.ViewAnnotationAnchor
import com.mapbox.maps.extension.compose.MapboxMap
import com.mapbox.maps.extension.compose.animation.viewport.rememberMapViewportState
import com.mapbox.maps.extension.compose.annotation.ViewAnnotation
import com.mapbox.maps.extension.compose.style.MapStyle
import com.mapbox.maps.viewannotation.annotationAnchor
import com.mapbox.maps.viewannotation.geometry
import com.mapbox.maps.viewannotation.viewAnnotationOptions
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.location.MapStyles
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaErrorState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PaymentStatus
import cz.cleansia.partner.api.model.PaymentType
import java.util.Locale

/**
 * v2 layout: Mapbox tile as full-bleed backdrop, BottomSheetScaffold
 * with three snap points carrying all detail content. The cleaner can
 * drag the sheet up to focus on the work or down to focus on the map
 * (Wolt / Foodora pattern). Compact header is always visible at the
 * top of the sheet so the order number, status, date and pay never
 * scroll away.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderDetailScreen(
    onNavigateBack: () -> Unit,
    viewModel: OrderDetailViewModel = hiltViewModel(),
    checklistViewModel: CleaningChecklistViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val inFlightAction by viewModel.inFlightAction.collectAsStateWithLifecycle()
    val offerRefusal by viewModel.offerRefusal.collectAsStateWithLifecycle()
    val preferredOffer by viewModel.preferredOffer.collectAsStateWithLifecycle()
    val checkedIds by checklistViewModel.checkedIds.collectAsStateWithLifecycle()

    // No local SnackbarHostState — all VMs push directly to the
    // app-wide SnackbarController bus, rendered by GlobalSnackbarHost
    // at the nav root. Errors/successes therefore look identical to
    // every other surface in the app, not the bare Material default.

    // Silent freshness check on every resume so coming back from a
    // sub-screen (photo picker, notes dialog) shows the latest server
    // state without a visible spinner. Repository gates this on a 30s
    // staleness window — when the cache is warm the call short-circuits
    // before any network I/O, so this is cheap to fire on every resume.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onResume()
    }

    when (val s = uiState) {
        OrderDetailUiState.Loading -> {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }
        }
        is OrderDetailUiState.Loaded -> {
            // Cash confirmation state lives HERE, at the screen root —
            // deliberately not inside `sheetContent`. The dialog opens its own
            // window either way, but state hoisted into the sheet's content is
            // recomposed by the sheet's drag-anchor recalculation, the same
            // collision ReferralCodeBottomSheet documents as having frozen its
            // input pipeline. iOS hosts it at its ZStack root for the same
            // reason (OrderDetailContent.swift).
            var confirmingCash by remember { mutableStateOf(false) }
            var decliningOffer by remember { mutableStateOf(false) }

            Box(modifier = Modifier.fillMaxSize()) {
                OrderDetailBottomSheetLayout(
                    order = s.order,
                    inFlight = inFlightAction,
                    preferredOffer = preferredOffer,
                    checkedIds = checkedIds,
                    onToggleChecklistItem = checklistViewModel::setChecked,
                    onTake = viewModel::take,
                    onStart = viewModel::start,
                    onNotifyOnTheWay = viewModel::notifyOnTheWay,
                    // Slide-to-complete now: no dialog, no optional
                    // fields. Backend accepts null for both actualMinutes
                    // and notes — the cleaner just confirms with the
                    // slide gesture and the order flips to Completed.
                    onCompleteClick = { viewModel.complete(null, null) },
                    onCashConfirmRequested = { confirmingCash = true },
                    onDeclineOffer = { decliningOffer = true },
                    // onContentMutated routes through the staleness-gated
                    // refresh path, so photo upload / note add re-fetches
                    // silently (no full-page spinner flash). Repository
                    // invalidates its watermark on mutation success, so the
                    // gate always lets this through.
                    onPhotosChanged = viewModel::onContentMutated,
                    onNavigateBack = onNavigateBack,
                )

                if (decliningOffer) {
                    CleansiaDialog(
                        onDismiss = { decliningOffer = false },
                        title = stringResource(R.string.offer_decline_title),
                        message = stringResource(R.string.offer_decline_body),
                        confirmLabel = stringResource(R.string.offer_decline_cta),
                        dismissLabel = stringResource(R.string.cancel),
                        destructive = true,
                        onConfirm = {
                            decliningOffer = false
                            viewModel.declinePreferredOffer()
                        },
                    )
                }

                offerRefusal?.let { refusal ->
                    OfferRefusalDialog(refusal = refusal, onDismiss = viewModel::dismissOfferRefusal)
                }

                if (confirmingCash) {
                    CleansiaDialog(
                        onDismiss = { confirmingCash = false },
                        title = stringResource(
                            R.string.partner_order_mark_cash_collected_confirm_title,
                        ),
                        message = cashDueLabel(
                            s.order.totalPrice,
                            s.order.currency?.code ?: s.order.currency?.symbol,
                        )?.let {
                            stringResource(
                                R.string.partner_order_mark_cash_collected_confirm_message,
                                it,
                            )
                        } ?: stringResource(
                            R.string.partner_order_mark_cash_collected_confirm_message_no_amount,
                        ),
                        icon = Icons.Outlined.Payments,
                        confirmLabel = stringResource(
                            R.string.partner_order_mark_cash_collected_confirm_action,
                        ),
                        onConfirm = {
                            confirmingCash = false
                            viewModel.markCashCollected()
                        },
                        dismissLabel = stringResource(R.string.cancel),
                        // Belt to the button's own spinner: an in-flight
                        // collection must not be confirmable twice.
                        confirmEnabled = inFlightAction != OrderAction.MarkCashCollected,
                    )
                }
            }
        }
        // Retry is wired to `refresh()`, NOT to `onResume()`/
        // `ensureFreshOrCachedAsync()`. The latter short-circuits on a
        // non-stale cache, so on a warm-but-failed order the button would
        // visibly do nothing. `refresh()` always re-fetches.
        //
        // Tapping Retry on a still-broken network re-renders this same
        // screen with no spinner — `fetch()` only ever writes Loaded or
        // Error, never back to Loading. That is accepted rather than fixed
        // here: the failing fetch also raises a translated snackbar
        // (OrderDetailViewModel.fetch, notifyOnError = true), which is the
        // feedback, and adding a Loading transition would mean touching a
        // ViewModel PR #152 has just changed. The customer app made the
        // opposite call because its Error lives inside a Scaffold that
        // stays mounted; here the branch owns the whole window.
        //
        // The explicit background is load-bearing: unlike Loaded, this
        // branch composes at the top level with no Mapbox backdrop beneath
        // it, so without a fill it would render over whatever the nav host
        // last drew.
        OrderDetailUiState.Error -> CleansiaErrorState(
            title = stringResource(R.string.order_detail_error_title),
            message = stringResource(R.string.order_detail_error_message),
            retryLabel = stringResource(R.string.retry),
            onRetry = viewModel::refresh,
            backLabel = stringResource(R.string.back),
            onBack = onNavigateBack,
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background),
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun OrderDetailBottomSheetLayout(
    order: OrderItem,
    inFlight: OrderAction?,
    preferredOffer: PendingOffer?,
    checkedIds: Set<String>,
    onToggleChecklistItem: (String, Boolean) -> Unit,
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onDeclineOffer: () -> Unit,
    onPhotosChanged: () -> Unit,
    onNavigateBack: () -> Unit,
) {
    val status = order.orderStatus.toOrderStatus()
    val isMine = order.isAssignedToCurrentUser == true
    val isInProgress = status == OrderStatus._4
    val darkTheme = isSystemInDarkTheme()

    val location = order.orderLocation()
    val mapPoint = location.mapPoint(status)

    val screenHeight = LocalConfiguration.current.screenHeightDp.dp
    // Sheet peek = 75% of screen so the map shrinks to ~25% — just
    // enough to read the location at a glance without dominating the
    // sheet's working area. Cleaner can still drag down for a bigger
    // map glimpse if they need to scout the route.
    val sheetPeekHeight = screenHeight * 0.75f
    val sheetState = rememberStandardBottomSheetState(
        initialValue = SheetValue.PartiallyExpanded,
        skipHiddenState = true,
    )
    val scaffoldState = rememberBottomSheetScaffoldState(bottomSheetState = sheetState)

    // Outer wrapping Box hosts:
    //   1. BottomSheetScaffold (map + sheet)
    //   2. FloatingMascot drawn on top of both, anchored to the sheet's
    //      top edge so half of it sits over the map and half over the
    //      sheet (Wolt/Foodora overlay pattern).
    Box(modifier = Modifier.fillMaxSize()) {
        BottomSheetScaffold(
            scaffoldState = scaffoldState,
            sheetPeekHeight = sheetPeekHeight,
            sheetContainerColor = MaterialTheme.colorScheme.surface,
            sheetContentColor = MaterialTheme.colorScheme.onSurface,
            sheetTonalElevation = 0.dp,
            sheetShadowElevation = 12.dp,
            sheetDragHandle = {
                OrderDetailCompactHeader()
            },
            containerColor = MaterialTheme.colorScheme.background,
            sheetContent = {
                OrderDetailSheetContent(
                    order = order,
                    status = status,
                    location = location,
                    isMine = isMine,
                    isInProgress = isInProgress,
                    inFlight = inFlight,
                    preferredOffer = preferredOffer,
                    checkedIds = checkedIds,
                    onToggleChecklistItem = onToggleChecklistItem,
                    onTake = onTake,
                    onStart = onStart,
                    onNotifyOnTheWay = onNotifyOnTheWay,
                    onCompleteClick = onCompleteClick,
                    onCashConfirmRequested = onCashConfirmRequested,
                    onDeclineOffer = onDeclineOffer,
                    onPhotosChanged = onPhotosChanged,
                )
            },
        ) { _ ->
            Box(modifier = Modifier.fillMaxSize()) {
                if (mapPoint != null) {
                    MapBackdrop(
                        latitude = mapPoint.first,
                        longitude = mapPoint.second,
                        darkTheme = darkTheme,
                        sheetCoverHeight = sheetPeekHeight,
                    )
                } else {
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .background(MaterialTheme.colorScheme.primaryContainer),
                    )
                }
                FloatingBackButton(
                    onClick = onNavigateBack,
                    modifier = Modifier
                        .windowInsetsPadding(WindowInsets.statusBars)
                        .padding(start = Spacing.M, top = Spacing.S)
                        .align(Alignment.TopStart),
                )
            }
        }
        // Foodora-style mascot puck: floats over the sheet edge on
        // the RIGHT side (TopEnd align), half on the map and half on
        // the sheet. Animated WebP for InProgress; static PNG others.
        FloatingMascot(
            status = status,
            sheetState = sheetState,
            modifier = Modifier.align(Alignment.TopEnd),
        )
    }
}

/**
 * Map backdrop. The pin is a Mapbox ViewAnnotation (not a Compose
 * overlay) so it stays glued to the geographic coordinate when the
 * cleaner pans/zooms the map OR drags the bottom sheet over it — a
 * Compose overlay centered in the Box would drift as the visible map
 * portion shrinks. The viewport state and Point are remembered so the
 * MapView isn't reinitialised on every sheet drag recomposition.
 */
@Composable
private fun MapBackdrop(
    latitude: Double,
    longitude: Double,
    darkTheme: Boolean,
    sheetCoverHeight: Dp,
) {
    val point = remember(latitude, longitude) {
        Point.fromLngLat(longitude, latitude)
    }
    val density = LocalDensity.current
    // Mapbox camera padding tells the map "the bottom N pixels are
    // obscured by another layer" (the bottom sheet). The viewport
    // recenters in the unobscured area, which renders the pin in the
    // visible upper portion of the map instead of being hidden under
    // the sheet's peek. The padding scales with the sheet peek height
    // so the math holds across phone sizes.
    val bottomPaddingPx = with(density) { sheetCoverHeight.toPx().toDouble() }
    val viewportState = rememberMapViewportState {
        setCameraOptions {
            center(point)
            zoom(15.0)
            padding(
                com.mapbox.maps.EdgeInsets(0.0, 0.0, bottomPaddingPx, 0.0),
            )
        }
    }
    val annotationOptions = remember(point) {
        viewAnnotationOptions {
            geometry(point)
            // Anchor BOTTOM so the marker's bottom point sits on the
            // exact coordinate (visually the tip of the pin is the
            // address, not its center).
            annotationAnchor { anchor(ViewAnnotationAnchor.BOTTOM) }
            allowOverlap(true)
        }
    }

    MapboxMap(
        modifier = Modifier.fillMaxSize(),
        mapViewportState = viewportState,
        style = { MapStyle(style = if (darkTheme) MapStyles.DARK else MapStyles.LIGHT) },
        scaleBar = {},
        compass = {},
        logo = {},
        attribution = {},
    ) {
        ViewAnnotation(options = annotationOptions) {
            MapBackdropPin()
        }
    }
}

@Composable
private fun MapBackdropPin() {
    Box(
        modifier = Modifier
            .size(40.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primary),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = Icons.Outlined.Place,
            contentDescription = null,
            tint = Color.White,
            modifier = Modifier.size(22.dp),
        )
    }
}

@Composable
private fun FloatingBackButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        onClick = onClick,
        modifier = modifier.size(40.dp),
        shape = CircleShape,
        color = MaterialTheme.colorScheme.surface,
        shadowElevation = 4.dp,
    ) {
        Box(contentAlignment = Alignment.Center) {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                contentDescription = stringResource(R.string.back),
                tint = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}

@Composable
private fun OrderDetailSheetContent(
    order: OrderItem,
    status: OrderStatus?,
    location: OrderLocation,
    isMine: Boolean,
    isInProgress: Boolean,
    inFlight: OrderAction?,
    preferredOffer: PendingOffer?,
    checkedIds: Set<String>,
    onToggleChecklistItem: (String, Boolean) -> Unit,
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onDeclineOffer: () -> Unit,
    onPhotosChanged: () -> Unit,
) {
    val disclosure = order.orderDisclosure()

    // From-customer card shows ONLY the general notes + special
    // instructions now — access has been promoted to its own card.
    val showFromCustomerCard =
        !order.notes.isNullOrBlank() || !order.specialInstructions.isNullOrBlank()

    // Resolved here for the in-sheet OrderTimerCard's live timer.
    val startedAtMillis = remember(order.statusHistory) {
        order.statusHistory.orEmpty()
            .firstOrNull { it.status?.value == OrderStatus._4.value }
            ?.createdOn
            ?.let { runCatching { java.time.Instant.parse(it).toEpochMilli() }.getOrNull() }
    }

    // fillMaxHeight() on the outer Column makes it claim the full sheet
    // viewport (whatever the sheet's current expansion is). The inner
    // scroll Column then gets weight(1f) to take everything that isn't
    // the sticky footer below — without weight, the scroll Column
    // expands to its natural content height and the footer is pushed
    // off-screen at the peek snap point.
    val scrollState = rememberScrollState()
    // Gesture-priority guard: once the cleaner has scrolled into the
    // sheet content, vertical drags must keep scrolling the content
    // instead of collapsing the sheet. M3's BottomSheetScaffold
    // nested-scroll integration alone doesn't always win this race
    // when the content is in a weighted child (the sheet sometimes
    // wins pre-scroll), so we intercept pre-scroll here and consume
    // anything the content can still absorb before letting the sheet
    // see it.
    //
    // Convention: positive `available.y` = drag down (would collapse
    // the sheet); negative = drag up (would expand it / scroll
    // content further). `scrollState.value` grows as the content
    // scrolls down (revealing lower content).
    val sheetGuard = remember(scrollState) {
        object : NestedScrollConnection {
            override fun onPreScroll(available: Offset, source: NestedScrollSource): Offset {
                if (source != NestedScrollSource.UserInput) return Offset.Zero
                val dy = available.y
                // Drag up (dy < 0): user wants to reveal more below.
                // If the content can still scroll down, consume the
                // delta into the scroll state so the sheet doesn't
                // expand past its peek point.
                if (dy < 0 && scrollState.value < scrollState.maxValue) {
                    val consumedByScroll = scrollState.dispatchRawDelta(-dy)
                    return Offset(0f, -consumedByScroll)
                }
                // Drag down (dy > 0): user wants to scroll content
                // back up. If the content isn't at the top, consume
                // so the content scrolls up before the sheet starts
                // collapsing.
                if (dy > 0 && scrollState.value > 0) {
                    val consumedByScroll = scrollState.dispatchRawDelta(-dy)
                    return Offset(0f, -consumedByScroll)
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
        // Scrollable content area — sized to share the remaining height
        // with the sticky action footer below.
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f, fill = true)
                .verticalScroll(scrollState)
                .padding(horizontal = Spacing.M),
            verticalArrangement = Arrangement.spacedBy(Spacing.M),
        ) {
            // Hero block: timer text directly above the segmented
            // progress bar with no whitespace between them. The two
            // read as a single group — Foodora pattern where the
            // primary label sits right on top of the tracker bar —
            // while the surrounding cards (metadata, customer, etc.)
            // keep the standard inter-card spacing via the parent
            // Column's spacedBy.
            Column(verticalArrangement = Arrangement.spacedBy(0.dp)) {
                OrderTimerCard(
                    order = order,
                    status = status,
                    startedAtEpochMillis = startedAtMillis,
                )
                OrderTrackerHero(status = status)
            }

            // Order metadata: order # + date + price chip. Inline
            // (no card background) — reads as the trailing identity
            // strip below the active-state block above.
            OrderMetadataRow(order = order)

            if (disclosure.showsAccessCard(status)) {
                AccessCard(accessInstructions = disclosure.accessInstructions!!)
            }

            CustomerCard(
                customerName = order.customerName,
                disclosure = disclosure,
                location = location,
            )

            ScopeCard(order = order)

            if (showFromCustomerCard) {
                FromCustomerNotesCard(
                    customerNotes = order.notes,
                    accessInstructions = null,
                    specialInstructions = order.specialInstructions,
                )
            }

            // Checklist + Photos are work-in-flight tools: only the
            // assignee needs them, and only while the order is being
            // executed (Confirmed → OnTheWay → InProgress). For
            // unassigned/pre-take orders there's nothing to act on;
            // once the order is Completed or Cancelled the work is
            // closed and these sections would just be visual noise.
            val showWorkSections = isMine &&
                (status == OrderStatus._2 || status == OrderStatus._3 || status == OrderStatus._4)
            val isTerminal = status == OrderStatus._5 || status == OrderStatus._6

            if (showWorkSections) {
                CleaningChecklist(
                    order = order,
                    checkedIds = checkedIds,
                    onToggle = onToggleChecklistItem,
                    interactive = isInProgress,
                )
            }

            // Even on a Completed order the record of what was reported during the job is worth
            // reading, so the section renders off the record's own arrival — the server sends `[]`
            // to a caller it withheld it from. Writing is a different question: adds are the
            // assignee's, at OnTheWay/InProgress only (no adds while merely Confirmed — work hasn't
            // started), and everyone else reads.
            val canAddNotesOrIssues =
                isMine && (status == OrderStatus._3 || status == OrderStatus._4)
            if (disclosure.showsWorkRecordSection(canAddNotesOrIssues)) {
                NotesAndIssuesSection(
                    notes = order.orderNotes.orEmpty(),
                    issues = order.orderIssues.orEmpty(),
                    isReadOnly = isTerminal || !isMine,
                    canAddNotes = canAddNotesOrIssues,
                    onMutated = onPhotosChanged, // same refresh path; renames not worth a turn
                )
            }

            if (showWorkSections) {
                // Per-rail gating: Before photos are uploadable once
                // the cleaner is OnTheWay or InProgress (no pre-arrival
                // uploads while merely Confirmed). After photos are
                // only uploadable once work is InProgress. Existing
                // photos still render read-only outside their upload
                // window.
                val canUploadBefore =
                    status == OrderStatus._3 || status == OrderStatus._4
                val canUploadAfter = status == OrderStatus._4
                PhotosSection(
                    // Refresh the surrounding OrderItem after each
                    // upload / delete so `hasAfterPhotos` stays live
                    // and the Complete slide unlocks the moment the
                    // cleaner adds an "after" photo.
                    onPhotosChanged = onPhotosChanged,
                    canUploadBefore = canUploadBefore,
                    canUploadAfter = canUploadAfter,
                )
            }

            PaymentCard(order = order)

            StatusTimeline(order = order)

            // Modest tail spacer — the footer below has its own
            // physical box now that the layout uses weight(), so we
            // don't need to push content above it manually.
            Spacer(Modifier.height(Spacing.S))
        }

        // Cash orders reach the door still Pending; the server blocks
        // CompleteOrder until the cleaner records the cash (PaymentType._1
        // = Cash, PaymentStatus._2 = Paid — Code.value carries the enum
        // ordinal, same as OrderStatus above).
        val needsCashCollection = order.paymentType?.value == PaymentType._1.value &&
            order.paymentStatus?.value != PaymentStatus._2.value

        StickyActionFooter(
            status = status,
            isMine = isMine,
            inFlight = inFlight,
            canComplete = order.hasAfterPhotos == true,
            needsCashCollection = needsCashCollection,
            preferredOffer = preferredOffer,
            onTake = onTake,
            onStart = onStart,
            onNotifyOnTheWay = onNotifyOnTheWay,
            onCompleteClick = onCompleteClick,
            onCashConfirmRequested = onCashConfirmRequested,
            onDeclineOffer = onDeclineOffer,
        )
    }
}

@Composable
private fun StickyActionFooter(
    status: OrderStatus?,
    isMine: Boolean,
    inFlight: OrderAction?,
    canComplete: Boolean,
    needsCashCollection: Boolean,
    preferredOffer: PendingOffer?,
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onDeclineOffer: () -> Unit,
) {
    // Completed / Cancelled / null — no action available. Don't even
    // render the footer so the cleaner doesn't see a hollow strip.
    val hasAction = when (status) {
        OrderStatus._0, OrderStatus._2 -> true
        OrderStatus._3, OrderStatus._4 -> isMine
        else -> false
    }
    if (!hasAction) return

    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surface,
        shadowElevation = 8.dp,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                // Bottom padding lifts the slide button off the
                // gesture bar so the cleaner isn't fighting the system
                // back-swipe area while sliding the thumb. Also pads
                // the navigation-bar inset so the button doesn't slide
                // under the 3-button nav on older devices.
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(
                    start = Spacing.M,
                    end = Spacing.M,
                    top = Spacing.S,
                    bottom = Spacing.M,
                ),
        ) {
            if (preferredOffer != null && !isMine) {
                ReservedForYouRow(respondByUtc = preferredOffer.respondByUtc)
                Spacer(Modifier.height(Spacing.S))
            }
            OrderPrimaryAction(
                status = status,
                isAssignedToCurrentUser = isMine,
                inFlight = inFlight,
                onTake = onTake,
                onStart = onStart,
                onNotifyOnTheWay = onNotifyOnTheWay,
                onCompleteClick = onCompleteClick,
                onCashConfirmRequested = onCashConfirmRequested,
                canComplete = canComplete,
                needsCashCollection = needsCashCollection,
                isPreferredOffer = preferredOffer != null,
            )
            if (preferredOffer != null && !isMine) {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
                    TextButton(onClick = onDeclineOffer, enabled = inFlight == null) {
                        Text(stringResource(R.string.offer_decline))
                    }
                }
            }
        }
    }
}

/**
 * The formatted sum the cleaner is about to record as taken in cash, or null
 * when the wire carried no usable total — the confirmation then asks without
 * naming an amount rather than guessing one.
 *
 * The `> 0` guard is not cosmetic: "Confirm you have taken 0 Kč in cash" reads
 * as a bug and is worse than the amount-free copy. iOS pins the same rule in
 * `OrderDetail.cashDueLabel`.
 */
internal fun cashDueLabel(
    totalPrice: Double?,
    currencyCode: String?,
    locale: Locale = Locale.getDefault(),
): String? = totalPrice
    ?.takeIf { it > 0 }
    ?.let { formatOrderPrice(it, currencyCode, locale) }
