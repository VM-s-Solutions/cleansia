package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.BottomSheetScaffold
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SheetValue
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.rememberBottomSheetScaffoldState
import androidx.compose.material3.rememberStandardBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
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
import cz.cleansia.core.location.MapStyles
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PaymentStatus
import cz.cleansia.partner.api.model.PaymentType

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
            OrderDetailBottomSheetLayout(
                order = s.order,
                inFlight = inFlightAction,
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
                onMarkCashCollected = viewModel::markCashCollected,
                // onContentMutated routes through the staleness-gated
                // refresh path, so photo upload / note add re-fetches
                // silently (no full-page spinner flash). Repository
                // invalidates its watermark on mutation success, so the
                // gate always lets this through.
                onPhotosChanged = viewModel::onContentMutated,
                onNavigateBack = onNavigateBack,
            )
        }
        OrderDetailUiState.Error -> Unit
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun OrderDetailBottomSheetLayout(
    order: OrderItem,
    inFlight: OrderAction?,
    checkedIds: Set<String>,
    onToggleChecklistItem: (String, Boolean) -> Unit,
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkCashCollected: () -> Unit,
    onPhotosChanged: () -> Unit,
    onNavigateBack: () -> Unit,
) {
    val status = order.orderStatus.toOrderStatus()
    val isMine = order.isAssignedToCurrentUser == true
    val isInProgress = status == OrderStatus._4
    val darkTheme = isSystemInDarkTheme()

    val hasCoords = order.address?.latitude != null && order.address?.longitude != null
    // Keep the map visible on Completed too — the location is still
    // meaningful context (cleaner may want to revisit, customer may
    // want to confirm where the job happened). Only Cancelled hides
    // the map, since the visit never happened.
    val canShowMap = hasCoords && status != OrderStatus._6

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
                    isMine = isMine,
                    isInProgress = isInProgress,
                    inFlight = inFlight,
                    checkedIds = checkedIds,
                    onToggleChecklistItem = onToggleChecklistItem,
                    onTake = onTake,
                    onStart = onStart,
                    onNotifyOnTheWay = onNotifyOnTheWay,
                    onCompleteClick = onCompleteClick,
                    onMarkCashCollected = onMarkCashCollected,
                    onPhotosChanged = onPhotosChanged,
                )
            },
        ) { _ ->
            Box(modifier = Modifier.fillMaxSize()) {
                if (canShowMap) {
                    MapBackdrop(
                        latitude = order.address!!.latitude!!,
                        longitude = order.address!!.longitude!!,
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
    isMine: Boolean,
    isInProgress: Boolean,
    inFlight: OrderAction?,
    checkedIds: Set<String>,
    onToggleChecklistItem: (String, Boolean) -> Unit,
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkCashCollected: () -> Unit,
    onPhotosChanged: () -> Unit,
) {
    val showAccessCard = isMine &&
        !order.accessInstructions.isNullOrBlank() &&
        (status == OrderStatus._3 || status == OrderStatus._4)

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

            if (showAccessCard) {
                AccessCard(accessInstructions = order.accessInstructions!!)
            }

            CustomerCard(
                customerName = order.customerName,
                customerPhone = order.customerPhone,
                address = order.address,
                isAssignedToCurrentUser = isMine,
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

            // Notes/Issues differ: even on a Completed order the
            // cleaner (and the customer / admin via their own apps)
            // need to see the historical record of what was reported
            // during the job. The section renders whenever the order
            // is mine (active OR terminal) — read-only on terminal,
            // self-hides if there's nothing to show. Add buttons are
            // gated separately to OnTheWay/InProgress only (no adds
            // while merely Confirmed — work hasn't started yet).
            val canAddNotesOrIssues =
                status == OrderStatus._3 || status == OrderStatus._4
            if (isMine) {
                NotesAndIssuesSection(
                    notes = order.orderNotes.orEmpty(),
                    issues = order.orderIssues.orEmpty(),
                    isReadOnly = isTerminal,
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
            onTake = onTake,
            onStart = onStart,
            onNotifyOnTheWay = onNotifyOnTheWay,
            onCompleteClick = onCompleteClick,
            onMarkCashCollected = onMarkCashCollected,
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
    onTake: () -> Unit,
    onStart: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkCashCollected: () -> Unit,
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
            OrderPrimaryAction(
                status = status,
                isAssignedToCurrentUser = isMine,
                inFlight = inFlight,
                onTake = onTake,
                onStart = onStart,
                onNotifyOnTheWay = onNotifyOnTheWay,
                onCompleteClick = onCompleteClick,
                onMarkCashCollected = onMarkCashCollected,
                canComplete = canComplete,
                needsCashCollection = needsCashCollection,
            )
        }
    }
}

