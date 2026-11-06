package cz.cleansia.partner.features.orders

import android.content.Intent
import android.net.Uri
import androidx.annotation.DrawableRes
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Assignment
import androidx.compose.material.icons.automirrored.outlined.FormatListBulleted
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material.icons.outlined.Cancel
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.LastBaseline
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.OrderTimelinePoint
import cz.cleansia.core.ui.components.OrderTimelineSegment
import cz.cleansia.core.ui.components.SwipeToConfirmButton
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PaymentStatus
import cz.cleansia.partner.api.model.PaymentType
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.features.media.rememberPhotoSourcePicker
import cz.cleansia.partner.ui.theme.Green400
import cz.cleansia.partner.ui.theme.Green800
import cz.cleansia.partner.ui.theme.SuccessBg
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlinx.coroutines.delay

/**
 * The job detail: the map band with the back button (the area blurred before the take, the door once hers), one sheet
 * over it with the state's figure on its edge, then the head, the run sheet and the reference sections, the state's one
 * move pinned under the thumb. A row, a push and the Dashboard open it alike: the route carries the id, the board's row
 * comes from the store.
 */
@Composable
fun OrderDetailScreen(
    onNavigateBack: () -> Unit,
    onOpenLegalDocuments: () -> Unit,
    onOpenOrder: (String) -> Unit = {},
    onShowAvailable: () -> Unit = {},
    onOpenEarnings: () -> Unit = {},
    viewModel: OrderDetailViewModel = hiltViewModel(),
    checklistViewModel: CleaningChecklistViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val inFlightAction by viewModel.inFlightAction.collectAsStateWithLifecycle()
    val offerRefusal by viewModel.offerRefusal.collectAsStateWithLifecycle()
    val preferredOffer by viewModel.preferredOffer.collectAsStateWithLifecycle()
    val contractRequest by viewModel.contractRequest.collectAsStateWithLifecycle()
    val takenRow by viewModel.takenRow.collectAsStateWithLifecycle()
    val contractStanding by viewModel.contractStanding.collectAsStateWithLifecycle()
    val completionStep by viewModel.completionStep.collectAsStateWithLifecycle()
    val boardRow by viewModel.boardRow.collectAsStateWithLifecycle()
    val ownRows by viewModel.ownRows.collectAsStateWithLifecycle()
    val openRows by viewModel.openRows.collectAsStateWithLifecycle()
    val myEmployeeId by viewModel.myEmployeeId.collectAsStateWithLifecycle()
    val areaCentre by viewModel.areaCentre.collectAsStateWithLifecycle()
    val checkedIds by checklistViewModel.checkedIds.collectAsStateWithLifecycle()

    // A silent freshness check on every resume; the repository's watermark makes it free while the order is warm.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onResume()
    }
    LaunchedEffect(viewModel) { viewModel.left.collect { onNavigateBack() } }

    // The status line counts minutes and Withdraw closes at the start, so the clock is read again each minute and on
    // every resume: a delay stops while the device sleeps.
    var now by remember { mutableStateOf(Instant.now()) }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { now = Instant.now() }
    LaunchedEffect(now) {
        delay(Duration.ofSeconds(60 - now.epochSecond % 60).toMillis())
        now = Instant.now()
    }

    // Photos are hers only: GetPhotos refuses a job she is not on.
    val order = (uiState as? OrderDetailUiState.Loaded)?.order
    val photosViewModel: OrderPhotosViewModel? = if (order?.isAssignedToCurrentUser == true) hiltViewModel() else null
    val photosState = photosViewModel?.uiState?.collectAsStateWithLifecycle()?.value
    val photoMutation = photosViewModel?.mutation?.collectAsStateWithLifecycle()?.value ?: PhotoMutationState()
    val photoVersion = photosViewModel?.mutationVersion?.collectAsStateWithLifecycle()?.value ?: 0
    LaunchedEffect(photoVersion) { if (photoVersion > 0) viewModel.onContentMutated() }
    var pickingType by remember { mutableStateOf<PhotoType?>(null) }
    val pickPhoto = rememberPhotoSourcePicker(cameraOnly = true) { uri ->
        val type = pickingType
        pickingType = null
        if (type != null) photosViewModel?.compressAndUpload(type, uri)
    }

    var confirmingCash by remember { mutableStateOf(false) }
    var confirmingCashNotPaid by remember { mutableStateOf(false) }
    var decliningOffer by remember { mutableStateOf(false) }
    var confirmingDrop by remember { mutableStateOf(false) }
    var confirmingStart by remember { mutableStateOf(false) }
    val context = LocalContext.current

    OrderDetailContent(
        uiState = uiState,
        boardRow = boardRow,
        ownRows = ownRows,
        openRows = openRows,
        removedJob = viewModel.removedJob,
        now = now,
        myEmployeeId = myEmployeeId,
        photos = (photosState as? OrderPhotosUiState.Loaded)?.photos.orEmpty(),
        photoMutation = photoMutation,
        inFlight = inFlightAction,
        preferredOffer = preferredOffer,
        contractStanding = contractStanding,
        completionStep = completionStep,
        checkedIds = checkedIds,
        onNavigateBack = onNavigateBack,
        onRetry = viewModel::refresh,
        onCheckAgain = viewModel::checkAgain,
        areaCentre = areaCentre,
        onOpenOrder = onOpenOrder,
        onShowAvailable = {
            viewModel.showAvailable()
            onShowAvailable()
        },
        onOpenEarnings = onOpenEarnings,
        onToggleChecklistItem = checklistViewModel::setChecked,
        onTake = viewModel::take,
        onOpenContract = viewModel::openContract,
        onStart = viewModel::start,
        onStartRequested = { confirmingStart = true },
        onNotifyOnTheWay = viewModel::notifyOnTheWay,
        // The backend takes null for both the actual minutes and the notes: the slide is the whole confirmation.
        onCompleteClick = { viewModel.complete(null, null) },
        onMarkMyPartDone = viewModel::markMyPartDone,
        onCashConfirmRequested = { confirmingCash = true },
        onCashNotPaidRequested = { confirmingCashNotPaid = true },
        onDeclineOffer = { decliningOffer = true },
        onDropRequested = { confirmingDrop = true },
        onReportLockout = viewModel::reportLockout,
        onCall = { phone ->
            runCatching { context.startActivity(Intent(Intent.ACTION_DIAL, Uri.parse("tel:${phone.replace(" ", "")}")).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }
        },
        onAddPhoto = { type ->
            pickingType = type
            pickPhoto.open()
        },
        onDeletePhoto = { id -> photosViewModel?.delete(id) },
        notes = { readOnly, canAdd ->
            NotesAndIssuesSection(
                notes = order?.orderNotes.orEmpty(),
                issues = order?.orderIssues.orEmpty(),
                isReadOnly = readOnly,
                canAddNotes = canAdd,
                onMutated = viewModel::onContentMutated,
            )
        },
    )

    if (decliningOffer) {
        AlertDialog(
            onDismissRequest = { decliningOffer = false },
            title = { Text(stringResource(R.string.offer_decline_title)) },
            text = { Text(stringResource(R.string.offer_decline_body)) },
            confirmButton = {
                TextButton(
                    onClick = {
                        decliningOffer = false
                        viewModel.declinePreferredOffer()
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text(stringResource(R.string.offer_decline_cta)) }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { decliningOffer = false }) { Text(stringResource(R.string.cancel)) }
            },
        )
    }

    offerRefusal?.let { refusal ->
        OfferRefusalDialog(refusal = refusal, onDismiss = viewModel::dismissOfferRefusal)
    }

    contractRequest?.let { request ->
        WorkContractSheet(
            request = request,
            onDismiss = viewModel::dismissContract,
            onOutcome = viewModel::onWorkContractOutcome,
            onOpenLegalDocuments = {
                viewModel.dismissContract()
                onOpenLegalDocuments()
            },
            taken = takenRow != null,
            takenRow = takenRow,
            onOpenJob = viewModel::dismissContract,
            onBackToJobs = {
                viewModel.dismissContract()
                viewModel.showAvailable()
                onShowAvailable()
            },
        )
    }

    // The start from Confirmed once the start has passed: the same slide as on the way, one gesture per act.
    if (confirmingStart) {
        AlertDialog(
            onDismissRequest = { confirmingStart = false },
            title = { Text(stringResource(R.string.late_already_there)) },
            text = {
                val idle = stringResource(R.string.slide_to_start)
                val busy = stringResource(R.string.starting_order)
                val start = {
                    confirmingStart = false
                    viewModel.start()
                }
                SwipeToConfirmButton(
                    text = idle,
                    onConfirmed = start,
                    modifier = Modifier.slideSemantics(label = idle, busyLabel = busy, enabled = true, isBusy = false, onCommit = start),
                    maxLines = 2,
                    thumbLanes = true,
                )
            },
            confirmButton = {},
            dismissButton = {
                CleansiaTextButton(onClick = { confirmingStart = false }) { Text(stringResource(R.string.cancel)) }
            },
        )
    }

    // The only irreversible money action: the confirm names what the customer owes, the total less credit.
    if (confirmingCash) {
        AlertDialog(
            onDismissRequest = { confirmingCash = false },
            title = { Text(stringResource(R.string.partner_order_mark_cash_collected_confirm_title)) },
            text = {
                Text(
                    order?.cashDueText(appLocale())
                        ?.let { stringResource(R.string.partner_order_mark_cash_collected_confirm_message, it) }
                        ?: stringResource(R.string.partner_order_mark_cash_collected_confirm_message_no_amount),
                )
            },
            confirmButton = {
                CleansiaTextButton(
                    onClick = {
                        confirmingCash = false
                        viewModel.markCashCollected()
                    },
                ) {
                    Text(stringResource(R.string.partner_order_mark_cash_collected_confirm_action))
                }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { confirmingCash = false }) { Text(stringResource(R.string.cancel)) }
            },
        )
    }

    if (confirmingCashNotPaid) {
        AlertDialog(
            onDismissRequest = { confirmingCashNotPaid = false },
            title = { Text(stringResource(R.string.order_cash_not_paid_confirm_title)) },
            text = {
                Text(
                    cashDueLabel(order?.cashNotPaidOwed(), order?.asBoardRow()?.let(OrdersBoardRules::symbolOf))
                        ?.let { stringResource(R.string.order_cash_not_paid_confirm_message, it) }
                        ?: stringResource(R.string.order_cash_not_paid_confirm_message_no_amount),
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        confirmingCashNotPaid = false
                        viewModel.reportCashNotPaid()
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text(stringResource(R.string.order_cash_not_paid_confirm_action)) }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { confirmingCashNotPaid = false }) {
                    Text(stringResource(R.string.cancel))
                }
            },
        )
    }

    if (confirmingDrop) {
        AlertDialog(
            onDismissRequest = { confirmingDrop = false },
            title = { Text(stringResource(R.string.order_drop_confirm_title)) },
            text = { Text(stringResource(R.string.order_drop_confirm_message)) },
            confirmButton = {
                TextButton(
                    onClick = {
                        confirmingDrop = false
                        viewModel.dropOrder()
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text(stringResource(R.string.order_drop_confirm_action)) }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { confirmingDrop = false }) {
                    Text(stringResource(R.string.order_drop_keep))
                }
            },
        )
    }
}

/** The detail for every state, from what the screen holds; nothing here reads a view model. */
@Composable
internal fun OrderDetailContent(
    uiState: OrderDetailUiState,
    boardRow: OrderListItem?,
    ownRows: PaneState,
    openRows: PaneState,
    removedJob: OrderListItem?,
    now: Instant,
    myEmployeeId: String?,
    photos: List<GetOrderPhotosOrderPhotoDto>,
    photoMutation: PhotoMutationState,
    inFlight: OrderAction?,
    preferredOffer: PendingOffer?,
    contractStanding: WorkContractStanding,
    completionStep: CompletionStep,
    checkedIds: Set<String>,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    onCheckAgain: () -> Unit,
    areaCentre: MapPoint? = null,
    onOpenOrder: (String) -> Unit,
    onShowAvailable: () -> Unit,
    onOpenEarnings: () -> Unit,
    onToggleChecklistItem: (String, Boolean) -> Unit,
    onTake: () -> Unit,
    onOpenContract: (WorkContractRequest) -> Unit,
    onStart: () -> Unit,
    onStartRequested: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkMyPartDone: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onCashNotPaidRequested: () -> Unit,
    onDeclineOffer: () -> Unit,
    onDropRequested: () -> Unit,
    onReportLockout: (String) -> Unit,
    onCall: (String) -> Unit,
    onAddPhoto: (PhotoType) -> Unit,
    onDeletePhoto: (String) -> Unit,
    notes: @Composable (readOnly: Boolean, canAdd: Boolean) -> Unit,
) {
    val lists = DetailLists(ownRows, openRows, now, onOpenOrder, onShowAvailable, areaCentre)
    when (uiState) {
        is OrderDetailUiState.Loaded -> {
            val order = uiState.order
            when {
                order.orderStatus.toOrderStatus() == OrderStatus._6 -> CancelledDetail(order, boardRow, now, lists, onNavigateBack, onOpenEarnings)
                order.isAssignedToCurrentUser == true -> OwnJobDetail(
                    order = order,
                    now = now,
                    myEmployeeId = myEmployeeId,
                    photos = photos,
                    photoMutation = photoMutation,
                    inFlight = inFlight,
                    contractStanding = contractStanding,
                    completionStep = completionStep,
                    checkedIds = checkedIds,
                    areaCentre = areaCentre,
                    onNavigateBack = onNavigateBack,
                    onOpenEarnings = onOpenEarnings,
                    onToggleChecklistItem = onToggleChecklistItem,
                    onOpenContract = onOpenContract,
                    onStart = onStart,
                    onStartRequested = onStartRequested,
                    onNotifyOnTheWay = onNotifyOnTheWay,
                    onCompleteClick = onCompleteClick,
                    onMarkMyPartDone = onMarkMyPartDone,
                    onCashConfirmRequested = onCashConfirmRequested,
                    onCashNotPaidRequested = onCashNotPaidRequested,
                    onDropRequested = onDropRequested,
                    onReportLockout = onReportLockout,
                    onCall = onCall,
                    onAddPhoto = onAddPhoto,
                    onDeletePhoto = onDeletePhoto,
                    notes = notes,
                )
                else -> NotTakenDetail(order, boardRow, now, lists, inFlight, preferredOffer, onNavigateBack, onTake, onDeclineOffer)
            }
        }
        OrderDetailUiState.Loading -> boardRow?.let { RowDetail(it, lists, preferredOffer, inFlight, now, failed = false, onNavigateBack, onRetry, onDeclineOffer) }
            ?: NoRowLoading(onNavigateBack)
        is OrderDetailUiState.Error -> boardRow?.takeUnless { uiState.refused }?.let { RowDetail(it, lists, preferredOffer, inFlight, now, failed = true, onNavigateBack, onRetry, onDeclineOffer) }
            ?: NoRowError(gone = false, lists, onNavigateBack, onRetry)
        OrderDetailUiState.Gone -> NoRowError(gone = true, lists, onNavigateBack, onRetry)
        is OrderDetailUiState.Removed -> RemovedPage(uiState.reason, removedJob, lists, onNavigateBack, onCheckAgain)
    }
}

/** Her own rows and the open jobs as the page holds them, where their rows lead, and where the job's area lies. */
private class DetailLists(
    val own: PaneState,
    val open: PaneState,
    val now: Instant,
    val onOpenOrder: (String) -> Unit,
    val onShowAvailable: () -> Unit,
    val areaCentre: MapPoint?,
)

@Composable
private fun OwnJobDetail(
    order: OrderItem,
    now: Instant,
    myEmployeeId: String?,
    photos: List<GetOrderPhotosOrderPhotoDto>,
    photoMutation: PhotoMutationState,
    inFlight: OrderAction?,
    contractStanding: WorkContractStanding,
    completionStep: CompletionStep,
    checkedIds: Set<String>,
    areaCentre: MapPoint?,
    onNavigateBack: () -> Unit,
    onOpenEarnings: () -> Unit,
    onToggleChecklistItem: (String, Boolean) -> Unit,
    onOpenContract: (WorkContractRequest) -> Unit,
    onStart: () -> Unit,
    onStartRequested: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkMyPartDone: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onCashNotPaidRequested: () -> Unit,
    onDropRequested: () -> Unit,
    onReportLockout: (String) -> Unit,
    onCall: (String) -> Unit,
    onAddPhoto: (PhotoType) -> Unit,
    onDeletePhoto: (String) -> Unit,
    notes: @Composable (readOnly: Boolean, canAdd: Boolean) -> Unit,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val status = order.orderStatus.toOrderStatus()
    val isMine = order.isAssignedToCurrentUser == true
    val row = order.asBoardRow()
    val start = rules.start(row) ?: now
    val minutes = rules.minutes(row)
    val location = order.orderLocation()
    val disclosure = order.orderDisclosure()
    val past = order.customerDetailsClosedNote() != null
    val seats = order.crewSeats(myEmployeeId)
    val crew = seats.size >= 2
    val myMarkAt = seats.firstOrNull { it.isMe }?.doneAt
    val mateMarked = seats.any { !it.isMe && it.doneAt != null }
    val afterPhotos = photos.filter { it.photoType == PhotoType._2 }
    val firstAfterPhotoAt = afterPhotos.mapNotNull { it.capturedAt?.let(::instantOrNull) }.minOrNull()
    val afterPhoto = order.hasAfterPhotos == true || afterPhotos.isNotEmpty()
    val stamps = order.statusStamps()
    val finishedAt = stamps[OrderStatus._5] ?: order.completedAt?.let(::instantOrNull)
    val lockoutAt = order.lockoutReportedAt?.let(::instantOrNull)
    val cash = rules.isCash(row)
    val step = rules.runStep(status?.value, crew, myMarkAt, firstAfterPhotoAt, afterPhoto = afterPhoto)
    val statusItems = rules.runStatus(
        status = status?.value,
        start = start,
        minutes = minutes,
        now = now,
        zone = zone,
        locale = locale,
        crew = crew,
        myMarkAt = myMarkAt,
        firstAfterPhotoAt = firstAfterPhotoAt,
        lockoutAt = lockoutAt,
        finishedAt = finishedAt,
        dayPattern = android.text.format.DateFormat.getBestDateTimePattern(locale, "EEEMMMd"),
        afterPhoto = afterPhoto,
    ).map { it.text() }
    val mascot = detailMascot(status, isMine = true, reported = lockoutAt != null, cash = cash)
    val move = pinnedMove(
        status = status,
        isAssignedToCurrentUser = isMine,
        now = now,
        start = order.cleaningStart(),
        minutes = order.estimatedTime ?: 0,
        completionStep = completionStep,
        hasAfterPhoto = afterPhoto,
        needsCashCollection = order.needsCashCollection(),
        canReportCashNotPaid = order.cashNotPaidReportable(),
        cardCharged = order.cardCharged(),
        waitingFor = seats.firstOrNull { !it.isMe && it.doneAt == null },
        mateMarked = mateMarked,
    )
    val pay = rules.payText(row, locale)
    val caption = stringResource(if ((row.requiredEmployees ?: 1) > 1) R.string.orders_card_your_share else R.string.work_contract_reward)
    val canDrop = order.droppable(now)
    val showWorkSections = isMine &&
        (status == OrderStatus._2 || status == OrderStatus._3 || status == OrderStatus._4)
    val canAddNotesOrIssues = isMine && (status == OrderStatus._3 || status == OrderStatus._4)
    val facts = order.accessFacts()
    val accessFacts = facts.takeIf { disclosure.showsAccessFacts(status) }.orEmpty()
    val floorAndFlat = (if (accessModeRes(order.accessMode) != null) facts.drop(1) else facts).map { it.text() }
    val sections = ownSections(
        status = status?.value,
        step = step,
        crew = crew,
        mateMarked = mateMarked,
        cash = cash,
        past = past,
        accessShown = accessFacts.isNotEmpty() || disclosure.showsAccessCard(status),
    )
    val customerRowShown = status == OrderStatus._2 || status == OrderStatus._3 || (status == OrderStatus._5 && !past)

    val lockout: @Composable () -> Unit = {
        LockoutCard(
            order = order,
            now = now,
            entrancePhotos = photos.filter { it.photoType == PhotoType._3 },
            mutation = photoMutation,
            isReporting = inFlight == OrderAction.ReportLockout,
            actionsEnabled = inFlight == null,
            onReport = onReportLockout,
            onAddEntrancePhoto = { onAddPhoto(PhotoType._3) },
            onDeletePhoto = onDeletePhoto,
        )
    }
    val photoBlock: @Composable (Boolean) -> Unit = { slot ->
        PhotosBlock(
            photos = photos.filter { it.photoType != PhotoType._3 },
            mutation = photoMutation,
            canUploadBefore = photoWindowOpen(PhotoType._1, status),
            canUploadAfter = photoWindowOpen(PhotoType._2, status),
            onAdd = onAddPhoto,
            onDelete = onDeletePhoto,
            afterNeeded = slot && !afterPhoto,
        )
    }
    val checklist: @Composable () -> Unit = {
        if (showWorkSections) {
            CleaningChecklist(order = order, checkedIds = checkedIds, onToggle = onToggleChecklistItem, interactive = status == OrderStatus._4)
        }
    }
    val gate: @Composable () -> Unit = {
        GateList(seats = seats.takeIf { crew }, afterPhoto = afterPhoto, afterPhotoAt = firstAfterPhotoAt)
    }
    val customer: @Composable () -> Unit = {
        CustomerCard(customerName = order.customerName, disclosure = disclosure, location = location, closedNote = null)
    }
    val access: @Composable () -> Unit = {
        AccessCard(
            mode = accessModeRes(order.accessMode)?.let { stringResource(it) },
            floorAndFlat = floorAndFlat,
            accessInstructions = disclosure.accessInstructions.takeIf { disclosure.showsAccessCard(status) },
        )
    }

    val subs = mapOf<RunStep, @Composable ColumnScope.() -> Unit>(
        RunStep.GetIn to {
            if (accessFacts.isNotEmpty()) DotFlow(items = accessFacts.map { AnnotatedString(it.text()) }, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(text = OrdersBoardRules.tie(stringResource(R.string.access_shown_on_set_off)), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (order.showsLockout(now)) lockout()
        },
        RunStep.Finish to {
            Text(
                text = OrdersBoardRules.tie(
                    stringResource(
                        when {
                            crew -> R.string.finish_needs_crew
                            cash -> R.string.partner_order_complete_blocked_cash_sequence
                            else -> R.string.finish_needs_photo
                        },
                    ),
                ),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        },
    )
    val bodies = mapOf<RunStep, @Composable ColumnScope.() -> Unit>(
        RunStep.SetOff to { customer() },
        RunStep.GetIn to {
            val standing = order.lockoutStanding(now)
            val leads = standing is LockoutStanding.Open || standing is LockoutStanding.Reported
            if (leads) lockout()
            access()
            customer()
            if (!leads) lockout()
        },
        RunStep.Clean to {
            if (crew && mateMarked) {
                gate()
                photoBlock(false)
            } else {
                checklist()
                photoBlock(true)
                if (disclosure.showsWorkRecordSection(canAddNotesOrIssues)) notes(false, canAddNotesOrIssues)
            }
        },
        RunStep.Finish to {
            when {
                crew -> {
                    gate()
                    photoBlock(false)
                }
                cash -> CashBlock(order, row, afterPhoto = afterPhoto, afterPhotoAt = firstAfterPhotoAt)
                else -> {
                    gate()
                    checklist()
                    photoBlock(false)
                }
            }
        },
    )
    val records = if (status == OrderStatus._5) {
        mapOf<RunStep, @Composable ColumnScope.() -> Unit>(
            RunStep.Clean to {
                photoBlock(false)
                if (!past && disclosure.showsWorkRecordSection(false)) notes(true, false)
            },
        )
    } else {
        emptyMap()
    }

    val precise = location as? OrderLocation.Precise
    JobDetailFrame(
        onNavigateBack = onNavigateBack,
        mascot = mascot,
        map = { band, sheetTop ->
            JobBandMap(
                mode = jobMapMode(taken = isMine, latitude = precise?.latitude, longitude = precise?.longitude),
                areaCentre = areaCentre,
                street = rules.streetLine(precise?.line),
                band = band,
                sheetTopPx = sheetTop,
            )
        },
        cleaning = cleaningAnimated(step),
        pinned = if (status == OrderStatus._5 || move == PinnedMove.None) {
            null
        } else {
            {
                StickyActionFooter(
                    order = order,
                    move = move,
                    now = now,
                    inFlight = inFlight,
                    onStart = onStart,
                    onStartRequested = onStartRequested,
                    onNotifyOnTheWay = onNotifyOnTheWay,
                    onCompleteClick = onCompleteClick,
                    onMarkMyPartDone = onMarkMyPartDone,
                    onCashConfirmRequested = onCashConfirmRequested,
                    onCashNotPaidRequested = onCashNotPaidRequested,
                    onTakeAfterPhoto = { onAddPhoto(PhotoType._2) },
                    onCall = onCall,
                )
            }
        },
        head = { clearance ->
            JobHead(
                number = order.displayOrderNumber?.takeIf { it.isNotBlank() },
                status = status?.value,
                day = calendarDay(rules.start(row), zone),
                window = rules.window(start, minutes, zone, locale),
                statusItems = statusItems,
                progress = headProgress(step, start, minutes, now),
                street = if (past) rules.areaLine(row) else rules.streetLine(location.line),
                sub = when {
                    past -> emptyList()
                    customerRowShown -> listOfNotNull(rules.areaLine(row))
                    else -> listOfNotNull(rules.areaLine(row), order.customerName?.trim()?.takeIf { it.isNotEmpty() })
                },
                pay = pay,
                caption = caption.takeIf { pay != null },
                clearance = clearance,
            )
        },
    ) {
        OwnDayLine(row, status, start, now)
        if (cash && (status == OrderStatus._2 || status == OrderStatus._3 || status == OrderStatus._4)) {
            rules.cashFlag(row, locale)?.let {
                Spacer(Modifier.height(12.dp))
                OrderFlag(it.text(), FlagTone.Warn, Icons.Outlined.Payments)
            }
        }
        if (contractStanding is WorkContractStanding.Pending) {
            Spacer(Modifier.height(12.dp))
            WorkContractCard(
                standing = contractStanding,
                onAccept = { order.id?.let { onOpenContract(WorkContractRequest.Accept(it)) } },
                onRead = {},
            )
        }
        JobRunSheet(
            rows = runSheet(
                status = status?.value,
                start = start,
                minutes = minutes,
                now = now,
                zone = zone,
                locale = locale,
                crew = crew,
                onWayAt = stamps[OrderStatus._3],
                startedAt = stamps[OrderStatus._4],
                myMarkAt = myMarkAt,
                firstAfterPhotoAt = firstAfterPhotoAt,
                finishedAt = finishedAt,
                afterPhoto = afterPhoto,
            ),
            subs = subs,
            bodies = bodies,
            records = records,
        )
        val customerParts = buildList<@Composable () -> Unit> {
            when (sections.who) {
                CustomerWho.Contact -> add {
                    CustomerCard(
                        customerName = order.customerName,
                        disclosure = disclosure,
                        location = location,
                        closedNote = finishedAt?.let { stringResource(detailsCloseRes(it, now, zone), rules.timeText(it, zone, locale)) },
                        withNavigate = false,
                    )
                }
                CustomerWho.Removed -> add {
                    Text(
                        text = OrdersBoardRules.tie(stringResource(R.string.order_customer_details_closed_completed)),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                CustomerWho.None -> Unit
            }
            if (sections.customerWords && (!order.notes.isNullOrBlank() || !order.specialInstructions.isNullOrBlank())) {
                add { FromCustomerNotesCard(customerNotes = order.notes, specialInstructions = order.specialInstructions) }
            }
            if (sections.access) {
                add {
                    Column {
                        PartHeading(stringResource(R.string.access_section_title))
                        Spacer(Modifier.height(6.dp))
                        access()
                    }
                }
            }
        }
        CustomerSection(customerParts)
        ScopeCard(order = order, row = row, lateJoin = false, own = true, rows = sections.scopeRows)
        PaymentSection(
            cashRule = pay?.takeIf { sections.cashRule }?.let { stringResource(R.string.cash_rule, it) },
            onOpenEarnings = onOpenEarnings.takeIf { sections.payouts },
        )
        if (contractStanding is WorkContractStanding.Accepted) {
            WorkContractCard(
                standing = contractStanding,
                onAccept = {},
                onRead = { onOpenContract(WorkContractRequest.Read(it)) },
            )
        }
        DetailFooter(withdraw = canDrop, start = start, now = now, inFlight = inFlight, onDropRequested = onDropRequested)
    }
}

/** *Customer*: who, what they wrote and how to get in, 16 apart, each part only with content; nothing without one. */
@Composable
private fun CustomerSection(parts: List<@Composable () -> Unit>) {
    if (parts.isEmpty()) return
    DetailSection(title = stringResource(R.string.customer), icon = Icons.Outlined.Person) {
        parts.forEachIndexed { index, part ->
            if (index > 0) Spacer(Modifier.height(16.dp))
            part()
        }
    }
}

/** *Payment*: only what the head does not say, the cash rule on a cash job and where the payout goes once done. */
@Composable
private fun PaymentSection(cashRule: String?, onOpenEarnings: (() -> Unit)?) {
    if (cashRule == null && onOpenEarnings == null) return
    DetailSection(title = stringResource(R.string.payment_section_title), icon = Icons.Outlined.CreditCard) {
        cashRule?.let {
            Text(text = OrdersBoardRules.tie(it), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
        }
        onOpenEarnings?.let { PayoutsLink(it) }
    }
}

@Composable
private fun PayoutsLink(onOpenEarnings: () -> Unit) {
    CleansiaTextLink(
        text = stringResource(R.string.history_payouts_link, stringResource(R.string.earnings_title)),
        onClick = onOpenEarnings,
        trailingIcon = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
        maxLines = 2,
        modifier = Modifier.offset(x = (-4).dp),
    )
}

/** *Before you can complete*: each crew seat by given name with its own mark, then the after photo. The crew's one Call is the pinned move. */
@Composable
private fun GateList(seats: List<CrewSeat>?, afterPhoto: Boolean, afterPhotoAt: Instant?) {
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val clock = { at: Instant -> OrdersBoardRules.timeText(at, zone, locale) }
    Column(Modifier.fillMaxWidth()) {
        if (seats != null) PartHeading(stringResource(R.string.finish_before_complete), Modifier.padding(bottom = 4.dp))
        seats?.forEach { seat ->
            val done = seat.doneAt?.let { stringResource(R.string.crew_done_at, clock(it)) }
            GateRow(
                mark = { Avatar(seat) },
                title = if (seat.isMe) stringResource(R.string.crew_you) else seat.givenName.orEmpty(),
                sub = done ?: stringResource(R.string.crew_not_yet),
                done = done != null,
            )
        }
        if (afterPhoto) {
            GateRow(mark = { OrderTimelinePoint(OrderTimelineSegment.Past) }, title = stringResource(R.string.finish_after_photo), sub = afterPhotoAt?.let(clock), done = false)
        }
        if (seats != null) {
            Text(
                text = OrdersBoardRules.tie(stringResource(if (seats.all { it.doneAt != null }) R.string.crew_all_marked else R.string.crew_both_parts)),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp),
            )
        }
    }
}

@Composable
private fun GateRow(mark: @Composable () -> Unit, title: String, sub: String?, done: Boolean) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 44.dp).padding(vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Box(Modifier.size(36.dp), contentAlignment = Alignment.Center) { mark() }
        Column(Modifier.weight(1f)) {
            Text(text = title, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurface)
            if (!sub.isNullOrEmpty()) {
                Row {
                    val subStyle = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum")
                    if (done) Icon(Icons.Outlined.Check, contentDescription = null, tint = primaryText(), modifier = capBandAligned(subStyle).padding(end = 4.dp).size(14.dp))
                    Text(text = sub, style = subStyle, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.alignBy(FirstBaseline))
                }
            }
        }
    }
}

/** Identity only, never a status: her in sky, the crew-mate in green. */
@Composable
private fun Avatar(seat: CrewSeat) {
    val (fill, ink) = when {
        seat.isMe -> MaterialTheme.colorScheme.primaryContainer to MaterialTheme.colorScheme.onPrimaryContainer
        isDarkScheme() -> Green400.copy(alpha = 0.16f) to Green400
        else -> SuccessBg to Green800
    }
    Box(Modifier.size(36.dp).background(fill, CircleShape).clearAndSetSemantics {}, contentAlignment = Alignment.Center) {
        Text(text = seat.initials, style = MaterialTheme.typography.labelLarge, color = ink)
    }
}

/**
 * Finishing on a cash job: what the customer wrote about the money, then the gates. The amount is the head's flag and
 * the pinned move's, the rule the Payment section's; once collected, the head's flag says so and its gate is done.
 */
@Composable
private fun CashBlock(order: OrderItem, row: OrderListItem, afterPhoto: Boolean, afterPhotoAt: Instant?) {
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val collected = OrdersBoardRules.cashCollected(row)
    order.specialInstructions?.takeIf { it.isNotBlank() }?.let {
        Column {
            WordsLabel(stringResource(R.string.from_customer_section_title) + OrdersBoardRules.SEP + stringResource(R.string.note_special_label))
            Text(text = customerText(it), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.padding(top = 2.dp))
        }
    }
    Column {
        if (afterPhoto) {
            GateRow(mark = { OrderTimelinePoint(OrderTimelineSegment.Past) }, title = stringResource(R.string.finish_after_photo), sub = afterPhotoAt?.let { OrdersBoardRules.timeText(it, zone, locale) }, done = false)
        }
        GateRow(
            mark = { OrderTimelinePoint(if (collected) OrderTimelineSegment.Past else OrderTimelineSegment.Future) },
            title = stringResource(R.string.finish_cash_collected),
            sub = null,
            done = false,
        )
        GateRow(mark = { OrderTimelinePoint(OrderTimelineSegment.Future) }, title = stringResource(R.string.order_complete_job), sub = null, done = false)
    }
}

/** The end of the sheet: *Withdraw from this job* and until when. */
@Composable
private fun DetailFooter(withdraw: Boolean, start: Instant, now: Instant, inFlight: OrderAction?, onDropRequested: () -> Unit) {
    if (!withdraw) return
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    Column(Modifier.fillMaxWidth().padding(top = 24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        if (inFlight == OrderAction.Drop) {
            CircularProgressIndicator(modifier = Modifier.padding(vertical = 18.dp).size(20.dp), strokeWidth = 2.dp)
        } else {
            CleansiaOutlinedButton(
                text = stringResource(R.string.order_drop_action),
                onClick = onDropRequested,
                leadingIcon = Icons.Outlined.Cancel,
                enabled = inFlight == null,
                contentColor = MaterialTheme.colorScheme.error,
                borderColor = MaterialTheme.colorScheme.error,
            )
        }
        val sameDay = start.atZone(zone).toLocalDate() == now.atZone(zone).toLocalDate()
        val at = if (sameDay) {
            OrdersBoardRules.timeText(start, zone, locale)
        } else {
            DateTimeFormatter.ofPattern("EEE", locale).format(start.atZone(zone)).trimEnd('.') + " " + OrdersBoardRules.timeText(start, zone, locale)
        }
        Text(
            text = stringResource(R.string.drop_possible_until, at).replaceFirstChar { it.titlecase(locale) },
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(top = 6.dp),
        )
    }
}

/** Her own job's pinned move, from [pinnedMove]; the cash amount is what the customer owes. */
@Composable
private fun StickyActionFooter(
    order: OrderItem,
    move: PinnedMove,
    now: Instant,
    inFlight: OrderAction?,
    onStart: () -> Unit,
    onStartRequested: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onCompleteClick: () -> Unit,
    onMarkMyPartDone: () -> Unit,
    onCashConfirmRequested: () -> Unit,
    onCashNotPaidRequested: () -> Unit,
    onTakeAfterPhoto: () -> Unit,
    onCall: (String) -> Unit,
) {
    OrderPrimaryAction(
        move = move,
        inFlight = inFlight,
        now = now,
        cashDue = order.cashDueText(appLocale()),
        onTake = {},
        onDeclineOffer = {},
        onStart = onStart,
        onStartRequested = onStartRequested,
        onNotifyOnTheWay = onNotifyOnTheWay,
        onCompleteClick = onCompleteClick,
        onMarkMyPartDone = onMarkMyPartDone,
        onCashConfirmRequested = onCashConfirmRequested,
        onCashNotPaidRequested = onCashNotPaidRequested,
        onTakeAfterPhoto = onTakeAfterPhoto,
        onCallMate = onCall,
    )
}

/** The order's stamps by status, each its earliest: the run sheet's start is the first InProgress stamp. */
private fun OrderItem.statusStamps(): Map<OrderStatus, Instant> =
    statusHistory.orEmpty()
        .mapNotNull { entry -> entry.status.toOrderStatus()?.let { status -> entry.createdOn?.let(::instantOrNull)?.let { status to it } } }
        .groupBy({ it.first }, { it.second })
        .mapValues { (_, stamps) -> stamps.min() }

private fun instantOrNull(text: String): Instant? = runCatching { Instant.parse(text) }.getOrNull()

@Composable
private fun NotTakenDetail(
    order: OrderItem,
    boardRow: OrderListItem?,
    now: Instant,
    lists: DetailLists,
    inFlight: OrderAction?,
    preferredOffer: PendingOffer?,
    onNavigateBack: () -> Unit,
    onTake: () -> Unit,
    onDeclineOffer: () -> Unit,
) {
    val rules = OrdersBoardRules
    val row = order.asBoardRow(band = boardRow?.homeDistanceBand)
    val start = rules.start(row) ?: now
    val own = lists.own
    NotTakenPage(row = row, order = order, boardRow = boardRow, now = now, lists = lists, preferredOffer = preferredOffer, failed = false, onNavigateBack = onNavigateBack, onRetry = {}) {
        OrderPrimaryAction(
            move = pinnedMove(
                status = order.orderStatus.toOrderStatus(),
                isAssignedToCurrentUser = false,
                now = now,
                start = start,
                minutes = rules.minutes(row),
                preferred = preferredOffer != null,
                clash = if (own.hasLoaded && !own.failed) rules.clashes(row, own.rows) else null,
                ownRowsAnswered = own.hasLoaded || own.failed,
            ),
            inFlight = inFlight,
            now = now,
            cashDue = null,
            onTake = onTake,
            onDeclineOffer = onDeclineOffer,
            onStart = {},
            onStartRequested = {},
            onNotifyOnTheWay = {},
            onCompleteClick = {},
            onMarkMyPartDone = {},
            onCashConfirmRequested = {},
            onCashNotPaidRequested = {},
            onTakeAfterPhoto = {},
            onCallMate = {},
        )
    }
}

/**
 * A job not taken, loaded or drawn from the board's row while it loads ([order] null): the head reading New with that
 * day's line and its finding, Property & scope and what taking it shows, then what else that day holds; the error under
 * the head once the rest failed.
 */
@Composable
private fun NotTakenPage(
    row: OrderListItem,
    order: OrderItem?,
    boardRow: OrderListItem?,
    now: Instant,
    lists: DetailLists,
    preferredOffer: PendingOffer?,
    failed: Boolean,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    pinned: (@Composable () -> Unit)?,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val status = OrderStatus.entries.firstOrNull { it.value == rules.status(row) }
    val start = rules.start(row) ?: now
    val minutes = rules.minutes(row)
    val day = start.atZone(zone).toLocalDate()
    val today = now.atZone(zone).toLocalDate()
    val own = lists.own
    val clash = if (own.hasLoaded && !own.failed) rules.clashes(row, own.rows) else null
    val lateJoin = status == OrderStatus._3 || status == OrderStatus._4
    val pay = rules.payText(row, locale)
    val statusItems = if (lateJoin) {
        listOf(stringResource(R.string.day_today), stringResource(R.string.orders_flag_under_way_short))
    } else {
        listOf(notTakenDay(start, now, zone).text().replaceFirstChar { it.titlecase(locale) })
    }
    val mine = rules.ownOfDay(own.rows, day, zone)
    val number = (order?.displayOrderNumber ?: row.displayOrderNumber)?.takeIf { it.isNotBlank() }

    JobDetailFrame(
        onNavigateBack = onNavigateBack,
        mascot = if (failed) R.drawable.mascot_resting else detailMascot(status, isMine = false, offered = preferredOffer != null),
        pinned = pinned,
        map = { band, sheetTop ->
            JobBandMap(mode = jobMapMode(taken = false, latitude = null, longitude = null), areaCentre = lists.areaCentre, street = null, band = band, sheetTopPx = sheetTop)
        },
        head = { clearance ->
            JobHead(
                number = number,
                status = 0,
                day = calendarDay(rules.start(row), zone),
                window = rules.window(start, minutes, zone, locale),
                statusItems = statusItems,
                progress = null,
                street = rules.areaLine(row),
                sub = listOfNotNull(rules.bandText(row.homeDistanceBand)?.text()),
                pay = pay,
                caption = stringResource(R.string.orders_card_your_share).takeIf { (row.requiredEmployees ?: 1) > 1 && pay != null },
                clearance = clearance,
            )
        },
    ) {
        rules.daySpan(row, day, zone)?.let { span ->
            val nowMinutes = now.atZone(zone).let { it.hour * 60 + it.minute }.takeIf { day == today }
            val mineSpans = mine.mapNotNull { rules.daySpan(it, day, zone) }
            Spacer(Modifier.height(16.dp))
            OrdersDayLine(
                range = rules.dayRange(mineSpans + span),
                mine = mineSpans,
                open = rules.openSlots(listOf(row), own.rows, day, zone),
                now = nowMinutes,
                nowLabel = nowMinutes?.let { rules.timeText(now, zone, locale) },
            )
        }
        when {
            clash != null -> YourJobThatDay(clash, lists.onOpenOrder)
            preferredOffer != null -> HoldLine(preferredOffer, now)
            lateJoin -> {
                val until = rules.bookedEnd(row)?.let { stringResource(R.string.orders_flag_take_until, rules.timeText(it, zone, locale)) }
                val crew = order?.lateJoinCrew() ?: row.assignedEmployeesCount?.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.orders_flag_crew_taken, it) }
                Spacer(Modifier.height(12.dp))
                OrderFlag(listOfNotNull(until, crew?.text()).joinToString(OrdersBoardRules.SEP), FlagTone.Neutral, Icons.Outlined.Schedule)
            }
        }
        if (failed) {
            DetailErrorCard(
                title = stringResource(R.string.detail_error_rest),
                body = stringResource(R.string.detail_error_rest_body),
                onRetry = onRetry,
                modifier = Modifier.padding(top = 24.dp),
            )
            return@JobDetailFrame
        }
        ScopeCard(order = order, row = row, lateJoin = lateJoin)
        ShownAfterTake()
        if (boardRow != null) {
            val others = mine.filter { it.id != clash?.id && !rules.isExpired(it, now) }
            if (others.isNotEmpty()) {
                OwnRowsSection(stringResource(if (day == today) R.string.detail_later_today else R.string.detail_your_job_that_day), others, lists)
            }
            val also = rules.openOfDay(lists.open.rows, day, row.id, now, zone)
            if (also.isNotEmpty()) {
                OpenRowsSection(stringResource(if (day == today) R.string.detail_open_today else R.string.detail_also_open_that_day), also, own.rows, lists)
            }
        } else {
            NextJobs(lists, exceptId = null)
        }
    }
}

/** Under the day line: the job of hers this one hits, a whole-row link to it, its chevron on the last line. */
@Composable
private fun YourJobThatDay(clash: OrderListItem, onOpenOrder: (String) -> Unit) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val streetStyle = MaterialTheme.typography.bodyLarge
    Column(Modifier.padding(top = 12.dp)) {
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        Row(
            Modifier
                .fillMaxWidth()
                .heightIn(min = 44.dp)
                .clickable(role = Role.Button) { clash.id?.let(onOpenOrder) }
                .padding(top = 8.dp),
        ) {
            Column(Modifier.weight(1f).alignBy(LastBaseline)) {
                Text(text = stringResource(R.string.detail_your_job_that_day), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                DotFlow(
                    items = listOfNotNull(
                        rules.streetLine(clash.customerAddress)?.let { styled(it, SpanStyle(fontSize = streetStyle.fontSize)) },
                        rules.window(rules.start(clash), rules.minutes(clash), zone, locale)?.let { AnnotatedString(it) },
                        rules.heroTeam(clash)?.text()?.let { AnnotatedString(it) },
                    ),
                    style = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum"),
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.padding(top = 2.dp),
                )
            }
            Icon(
                Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = primaryText(),
                modifier = capBandAligned(streetStyle).padding(start = 8.dp).size(20.dp),
            )
        }
    }
}

/** *A customer asked for you* / *Yours until 12:06 today* · *then it goes to other cleaners*. */
@Composable
private fun HoldLine(offer: PendingOffer, now: Instant) {
    Column(Modifier.padding(top = 12.dp)) {
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        Text(
            text = OrdersBoardRules.tie(stringResource(R.string.offers_card_title)),
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.padding(top = 12.dp),
        )
        DotFlow(
            items = listOf(
                styled(reservedUntilLabel(offer.respondByUtc, now.toEpochMilli()), SpanStyle(color = primaryText())),
                styled(OrdersBoardRules.tie(stringResource(R.string.available_reserved_then)), SpanStyle(color = MaterialTheme.colorScheme.onSurfaceVariant)),
            ),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.padding(top = 2.dp),
        )
    }
}

@Composable
private fun CancelledDetail(
    order: OrderItem,
    boardRow: OrderListItem?,
    now: Instant,
    lists: DetailLists,
    onNavigateBack: () -> Unit,
    onOpenEarnings: () -> Unit,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val row = order.asBoardRow(band = boardRow?.homeDistanceBand)
    val start = rules.start(row) ?: now
    val minutes = rules.minutes(row)
    val reason = order.systemCancellationReason?.takeIf { it.isNotBlank() }
    val day = start.atZone(zone).toLocalDate()
    val today = now.atZone(zone).toLocalDate()
    val number = order.displayOrderNumber?.takeIf { it.isNotBlank() }

    JobDetailFrame(
        onNavigateBack = onNavigateBack,
        mascot = detailMascot(OrderStatus._6, isMine = true),
        pinned = null,
        head = { clearance ->
            JobHead(
                number = number,
                status = OrderStatus._6.value,
                day = calendarDay(rules.start(row), zone),
                window = rules.window(start, minutes, zone, locale),
                statusItems = rules.runStatus(
                    status = 6, start = start, minutes = minutes, now = now, zone = zone, locale = locale,
                    cancelledAt = order.statusStamps()[OrderStatus._6], cancelReason = reason,
                ).map { it.text() },
                progress = null,
                cancelled = true,
                street = rules.areaLine(row),
                sub = rules.scopeItems(row, locale.language).map { it.text() },
                pay = null,
                caption = null,
                clearance = clearance,
            )
        },
    ) {
        val span = rules.daySpan(row, day, zone)
        if (span != null) {
            val nowMinutes = now.atZone(zone).let { it.hour * 60 + it.minute }.takeIf { day == today }
            val mine = rules.ownOfDay(lists.own.rows, day, zone).filter { it.id != order.id && !rules.isExpired(it, now) }.mapNotNull { rules.daySpan(it, day, zone) }
            Spacer(Modifier.height(16.dp))
            OrdersDayLine(
                range = rules.dayRange(mine + span),
                mine = mine,
                open = listOfNotNull(rules.freedSlot(row, now, zone)?.let { OpenSlot(it, null, SlotLane.Full) }),
                now = nowMinutes,
                nowLabel = nowMinutes?.let { rules.timeText(now, zone, locale) },
                slotLabel = rules.window(start, minutes, zone, locale)?.takeIf { start > now }?.let { "$it ${stringResource(R.string.day_line_free)}" },
            )
        }
        DetailSection(title = stringResource(R.string.payment_section_title), icon = Icons.Outlined.CreditCard) {
            Text(text = OrdersBoardRules.tie(stringResource(rules.cancelPayLine(reason))), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
            PayoutsLink(onOpenEarnings)
        }
        CustomerSection(
            listOf<@Composable () -> Unit>({
                Text(
                    text = OrdersBoardRules.tie(stringResource(R.string.order_customer_details_closed_cancelled)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }),
        )
        val laterToday = rules.nextOwn(lists.own.rows, order.id, now).filter { rules.day(it, zone) == today }
        if (laterToday.isNotEmpty()) {
            OwnRowsSection(stringResource(R.string.detail_later_today), laterToday, lists)
        } else {
            NextJobs(lists, exceptId = order.id)
        }
        val openToday = rules.openOfDay(lists.open.rows, today, null, now, zone)
        if (openToday.isNotEmpty()) {
            OpenRowsSection(stringResource(R.string.detail_open_today), openToday, lists.own.rows, lists)
        }
        rules.availableBadge(lists.open.hasLoaded, lists.open.total, lists.open.rows, lists.now)?.let { total ->
            SeeOpenButton(total, lists.onShowAvailable, Modifier.padding(top = 16.dp))
        }
    }
}

@Composable
private fun SeeOpenButton(total: Int, onShowAvailable: () -> Unit, modifier: Modifier = Modifier) {
    CleansiaOutlinedButton(
        text = OrdersBoardRules.tie(pluralStringResource(R.plurals.active_see_open, total, total)),
        onClick = onShowAvailable,
        maxLines = 2,
        modifier = modifier,
        size = CleansiaButtonSize.Medium,
        contentColor = primaryText(),
        borderColor = MaterialTheme.colorScheme.primary,
    )
}

/** *Your next jobs · 3 jobs*: her own rows that have not ended, never this one. */
@Composable
private fun NextJobs(lists: DetailLists, exceptId: String?) {
    val rows = OrdersBoardRules.nextOwn(lists.own.rows, exceptId, lists.now)
    if (rows.isEmpty()) return
    OwnRowsSection(stringResource(R.string.orders_your_next_jobs), rows, lists, count = rows.size)
}

/** Her own jobs in a section on the sheet, under the calendar. */
@Composable
private fun OwnRowsSection(title: String, rows: List<OrderListItem>, lists: DetailLists, count: Int? = null) {
    DetailSection(
        title = title,
        icon = Icons.Outlined.CalendarToday,
        subtitle = count?.let { pluralStringResource(R.plurals.orders_job_count, it, it) },
    ) {
        FlatRows(rows) { row, edge ->
            OwnJobRow(row = row, now = lists.now, onClick = { row.id?.let(lists.onOpenOrder) }, modifier = edge, flags = false)
        }
    }
}

/** The board's open rows in a section on the sheet, under the Orders tab's clipboard. */
@Composable
private fun OpenRowsSection(title: String, rows: List<OrderListItem>, own: List<OrderListItem>, lists: DetailLists) {
    DetailSection(title = title, icon = Icons.AutoMirrored.Outlined.Assignment) {
        FlatRows(rows) { row, edge ->
            AvailableJobRow(
                row = row,
                now = lists.now,
                clash = OrdersBoardRules.clashes(row, own),
                onOpen = { row.id?.let(lists.onOpenOrder) },
                modifier = edge,
            )
        }
    }
}

/** *Open jobs, next 7 days*: the Active tab's week strip over the open jobs the page holds, her days filled. */
@Composable
private fun OpenWeek(lists: DetailLists, own: List<OrderListItem>, onCheckAgain: (() -> Unit)?) {
    if (!lists.open.hasLoaded) return
    val zone = ZoneId.systemDefault()
    val open = lists.open.rows.filter { !OrdersBoardRules.isExpired(it, lists.now) }
    WeekSection(
        cells = OrdersBoardRules.weekCells(lists.now.atZone(zone).toLocalDate(), open, own, zone, loaded = lists.open.hasLoaded),
        openTotal = OrdersBoardRules.availableBadge(lists.open.hasLoaded, lists.open.total, lists.open.rows, lists.now),
        onShowAvailable = lists.onShowAvailable,
        onCheckAgain = onCheckAgain,
    )
}

@Composable
private fun WeekSection(cells: List<WeekCell>, openTotal: Int?, onShowAvailable: () -> Unit, onCheckAgain: (() -> Unit)?) {
    DetailSection(title = OrdersBoardRules.tie(stringResource(R.string.active_open_next_7_days)), icon = Icons.AutoMirrored.Outlined.Assignment) {
        WeekStrip(cells = cells)
        if (cells.any { it.mine }) {
            Row(Modifier.padding(top = 8.dp), verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(10.dp).background(primaryText(), RoundedCornerShape(3.dp)))
                Text(
                    text = stringResource(R.string.active_week_legend_your_job),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(start = 6.dp).clearAndSetSemantics {},
                )
            }
        }
        openTotal?.let { SeeOpenButton(it, onShowAvailable, Modifier.padding(top = 12.dp)) }
        onCheckAgain?.let { CheckAgainLink(it) }
    }
}

@Composable
private fun CheckAgainLink(onCheckAgain: () -> Unit) {
    Box(Modifier.fillMaxWidth().padding(top = 4.dp), contentAlignment = Alignment.Center) {
        CleansiaTextLink(text = stringResource(R.string.orders_check_again), onClick = onCheckAgain, leadingIcon = Icons.Outlined.Refresh)
    }
}

/**
 * Loading, or failed, with the board's row: what the row holds at once, the rest as bars or the error under the head.
 * A job not hers is the not-taken page from the row; the pinned area holds only what the row can decide (see [rowDetailPin]).
 */
@Composable
private fun RowDetail(
    row: OrderListItem,
    lists: DetailLists,
    preferredOffer: PendingOffer?,
    inFlight: OrderAction?,
    now: Instant,
    failed: Boolean,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    onDeclineOffer: () -> Unit,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val start = rules.start(row) ?: now
    val minutes = rules.minutes(row)
    val status = rules.status(row)
    val orderStatus = OrderStatus.entries.firstOrNull { it.value == status }
    val own = lists.own.rows.any { it.id == row.id }
    val pay = rules.payText(row, locale)
    val team = (row.requiredEmployees ?: 1) > 1
    val move = pinnedMove(
        status = orderStatus,
        isAssignedToCurrentUser = own,
        now = now,
        start = start,
        minutes = minutes,
        preferred = preferredOffer != null && !own,
        ownRowsAnswered = false,
    )
    val pinned: (@Composable () -> Unit)? = when (rowDetailPin(move, failed)) {
        RowPin.Move -> {
            {
                OrderPrimaryAction(
                    move = move, inFlight = inFlight, now = now, cashDue = null, onTake = {}, onDeclineOffer = onDeclineOffer, onStart = {}, onStartRequested = {},
                    onNotifyOnTheWay = {}, onCompleteClick = {}, onMarkMyPartDone = {}, onCashConfirmRequested = {}, onCashNotPaidRequested = {},
                    onTakeAfterPhoto = {}, onCallMate = {},
                )
            }
        }
        RowPin.SlotBar -> {
            { Bar(width = Dp.Unspecified, height = 56.dp, modifier = Modifier.fillMaxWidth().clip(CircleShape).clearAndSetSemantics {}) }
        }
        RowPin.None -> null
    }
    if (!own) {
        NotTakenPage(
            row = row, order = null, boardRow = row, now = now, lists = lists, preferredOffer = preferredOffer, failed = failed,
            onNavigateBack = onNavigateBack, onRetry = onRetry, pinned = pinned,
        )
        return
    }
    val number = row.displayOrderNumber?.takeIf { it.isNotBlank() }
    val step = rules.runStep(status, crew = false, myMarkAt = null, firstAfterPhotoAt = null)
    JobDetailFrame(
        onNavigateBack = onNavigateBack,
        mascot = if (failed) R.drawable.mascot_resting else detailMascot(orderStatus, isMine = true, cash = rules.isCash(row)),
        pinned = pinned,
        map = { band, sheetTop ->
            JobBandMap(
                mode = jobMapMode(taken = true, latitude = row.customerAddressLatitude, longitude = row.customerAddressLongitude),
                areaCentre = lists.areaCentre,
                street = rules.streetLine(row.customerAddress),
                band = band,
                sheetTopPx = sheetTop,
            )
        },
        cleaning = !failed && cleaningAnimated(step),
        head = { clearance ->
            JobHead(
                number = number,
                status = status,
                day = calendarDay(rules.start(row), zone),
                window = rules.window(start, minutes, zone, locale),
                statusItems = rules.runStatus(status = status, start = start, minutes = minutes, now = now, zone = zone, locale = locale).map { it.text() },
                progress = headProgress(step, start, minutes, now),
                street = rules.streetLine(row.customerAddress),
                sub = if (orderStatus == OrderStatus._2 || orderStatus == OrderStatus._3) listOfNotNull(rules.areaLine(row)) else listOfNotNull(rules.areaLine(row), row.customerName),
                pay = pay,
                caption = stringResource(if (team) R.string.orders_card_your_share else R.string.work_contract_reward).takeIf { pay != null },
                clearance = clearance,
            )
        },
    ) {
        OwnDayLine(row, orderStatus, start, now)
        if (failed) {
            DetailErrorCard(
                title = stringResource(R.string.detail_error_rest),
                body = stringResource(R.string.detail_error_rest_body),
                onRetry = onRetry,
                modifier = Modifier.padding(top = 24.dp),
            )
        }
        JobRunSheet(
            rows = runSheet(status = status, start = start, minutes = minutes, now = now, zone = zone, locale = locale),
            subs = mapOf(
                RunStep.GetIn to {
                    if (failed) {
                        Text(text = "–", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    } else {
                        Bar(230.dp, 16.dp)
                        Bar(250.dp, 14.dp)
                    }
                },
                RunStep.Finish to {
                    val finish = when {
                        team -> R.string.finish_needs_crew
                        rules.isCash(row) -> R.string.partner_order_complete_blocked_cash_sequence
                        else -> R.string.finish_needs_photo
                    }
                    Text(text = OrdersBoardRules.tie(stringResource(finish)), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                },
            ),
            bodies = mapOf(
                RunStep.SetOff to {
                    CustomerLine(name = row.customerName, phone = row.customerPhone, withIcon = true)
                    ContactPills(
                        phone = row.customerPhone,
                        navigateTo = row.customerAddress?.let { OrderLocation.Precise(it, row.customerAddressLatitude, row.customerAddressLongitude) },
                    )
                },
            ),
        )
        if (!failed) {
            DetailSection(title = stringResource(R.string.customer), icon = Icons.Outlined.Person, modifier = Modifier.clearAndSetSemantics {}) {
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Bar(70.dp, 14.dp)
                    Bar(290.dp, 16.dp)
                    Bar(250.dp, 16.dp)
                }
            }
        }
    }
}

/** The head plays the cleaning loop while she cleans; every other step keeps its still. */
internal fun cleaningAnimated(step: RunStep): Boolean = step == RunStep.Clean

/** The head's figure by the state it shows, as iOS `OrderDetailMascotArt`; a cancelled job's rests. */
@DrawableRes
internal fun detailMascot(status: OrderStatus?, isMine: Boolean, offered: Boolean = false, reported: Boolean = false, cash: Boolean = false): Int? = when {
    status == OrderStatus._6 -> R.drawable.mascot_resting
    !isMine -> if (offered) R.drawable.mascot_waving else R.drawable.mascot_leaning
    else -> when (status) {
        OrderStatus._0, OrderStatus._1 -> R.drawable.mascot_leaning
        OrderStatus._2 -> R.drawable.mascot_waving
        OrderStatus._3 -> if (reported) R.drawable.mascot_resting else R.drawable.mascot_ready
        OrderStatus._4 -> if (cash) R.drawable.mascot_invoice else R.drawable.mascot_spray_and_cloth
        else -> R.drawable.mascot_thumbs_up
    }
}

/** What a row-backed page pins: the move the row decides (the locked set-off, a waiting Take), else the move's slot while loading, else nothing. */
internal enum class RowPin { Move, SlotBar, None }

internal fun rowDetailPin(move: PinnedMove, failed: Boolean): RowPin = when {
    move is PinnedMove.SetOffLocked -> RowPin.Move
    failed -> RowPin.None
    move is PinnedMove.TakeWaiting -> RowPin.Move
    else -> RowPin.SlotBar
}

/** Nothing is known, not even whether the job is hers: every value a flat bar and the move's slot held. */
@Composable
private fun NoRowLoading(onNavigateBack: () -> Unit) {
    JobDetailFrame(
        onNavigateBack = onNavigateBack,
        mascot = null,
        pinned = { Bar(width = Dp.Unspecified, height = 56.dp, modifier = Modifier.fillMaxWidth().clip(CircleShape).clearAndSetSemantics {}) },
        spokenTitle = stringResource(R.string.loading),
    ) {
        Column(Modifier.clearAndSetSemantics {}) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Bar(130.dp, 20.dp)
                Bar(84.dp, 24.dp, Modifier.padding(start = 8.dp))
            }
            Bar(170.dp, 18.dp, Modifier.padding(top = 4.dp))
            Row(Modifier.fillMaxWidth().padding(top = 12.dp)) {
                Bar(200.dp, 22.dp)
                Box(Modifier.weight(1f))
                Bar(56.dp, 14.dp)
            }
            Bar(Dp.Unspecified, 4.dp, Modifier.fillMaxWidth().padding(top = 8.dp))
            Row(Modifier.fillMaxWidth().padding(top = 16.dp)) {
                Column(Modifier.weight(1f)) {
                    Bar(150.dp, 20.dp)
                    Bar(190.dp, 16.dp, Modifier.padding(top = 6.dp))
                }
                Bar(96.dp, 34.dp)
            }
        }
        DetailSection(title = stringResource(R.string.run_a11y), icon = Icons.AutoMirrored.Outlined.FormatListBulleted, modifier = Modifier.clearAndSetSemantics {}) {
            repeat(4) { index ->
                Row(Modifier.fillMaxWidth().padding(bottom = if (index < 3) 20.dp else 0.dp)) {
                    Box(Modifier.padding(start = 2.dp, top = 2.dp).size(20.dp).background(MaterialTheme.colorScheme.surfaceVariant, CircleShape))
                    Column(Modifier.weight(1f).padding(start = 14.dp)) {
                        Row {
                            Bar(if (index == 0) 120.dp else 96.dp, 20.dp)
                            Box(Modifier.weight(1f))
                            Bar(56.dp, 16.dp)
                        }
                        Bar(if (index == 0) 230.dp else 170.dp, 16.dp, Modifier.padding(top = 4.dp))
                        if (index == 0) Bar(190.dp, 16.dp, Modifier.padding(top = 4.dp))
                    }
                }
            }
        }
    }
}

/** No row to fall back on: the failure is the sheet's head, then what the app still holds. */
@Composable
private fun NoRowError(gone: Boolean, lists: DetailLists, onNavigateBack: () -> Unit, onRetry: () -> Unit) {
    JobDetailFrame(onNavigateBack = onNavigateBack, mascot = R.drawable.mascot_resting, pinned = null) { clearance ->
        MascotClearColumn(clearance) {
            ErrorWords(
                title = stringResource(if (gone) R.string.detail_gone else R.string.detail_error_job),
                body = stringResource(R.string.order_detail_error_message).takeUnless { gone },
            )
            if (!gone) RetryButton(onRetry, Modifier.padding(top = 12.dp))
        }
        NextJobs(lists, exceptId = null)
        OpenWeek(lists, lists.own.rows, onCheckAgain = null)
    }
}

/**
 * In place of the job, which `GetById` no longer shows her: the title, the job named from the store's row, the
 * administrator's reason as written; then her next jobs and the open jobs, from the store or read by the page itself.
 */
@Composable
private fun RemovedPage(reason: String?, job: OrderListItem?, lists: DetailLists, onNavigateBack: () -> Unit, onCheckAgain: () -> Unit) {
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val ownFailed = lists.own.failed && !lists.own.hasLoaded
    val openFailed = lists.open.failed && !lists.open.hasLoaded
    JobDetailFrame(onNavigateBack = onNavigateBack, mascot = R.drawable.mascot_resting, pinned = null) { clearance ->
        MascotClearColumn(clearance) {
            Text(
                text = OrdersBoardRules.tie(stringResource(if (reason != null) R.string.order_removal_title else R.string.notification_order_assignment_revoked_title)),
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.semantics { heading() },
            )
            job?.let {
                DotFlow(
                    items = OrdersBoardRules.removalJobLine(it, lists.now, zone, locale, android.text.format.DateFormat.getBestDateTimePattern(locale, "EEEEMMMd"))
                        .map { item -> AnnotatedString(item.text()) },
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
            if (reason != null) {
                Text(
                    text = OrdersBoardRules.tie(stringResource(R.string.order_removal_lead)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 12.dp),
                )
                Text(
                    text = customerText(reason),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier
                        .padding(top = 8.dp)
                        .fillMaxWidth()
                        .background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(16.dp))
                        .padding(horizontal = 16.dp, vertical = 12.dp),
                )
            } else {
                Text(
                    text = OrdersBoardRules.tie(stringResource(R.string.detail_off_schedule)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
            if (ownFailed && openFailed) {
                CleansiaOutlinedButton(
                    text = stringResource(R.string.dash_browse_jobs),
                    onClick = lists.onShowAvailable,
                    modifier = Modifier.padding(top = 12.dp),
                    size = CleansiaButtonSize.Medium,
                    contentColor = primaryText(),
                    borderColor = MaterialTheme.colorScheme.primary,
                )
                CheckAgainLink(onCheckAgain)
            }
        }
        if (lists.own.isLoading && !lists.own.hasLoaded) ListBars() else NextJobs(lists, exceptId = job?.id)
        if (lists.open.isLoading && !lists.open.hasLoaded) {
            ListBars()
        } else if (!(ownFailed && openFailed)) {
            OpenWeek(lists, OrdersBoardRules.nextOwn(lists.own.rows, job?.id, lists.now), onCheckAgain = onCheckAgain)
        }
    }
}

/** A list being read holds its place at its real pitch, as flat bars. */
@Composable
private fun ListBars() {
    Column(Modifier.padding(top = 24.dp).clearAndSetSemantics {}) {
        repeat(2) { index ->
            if (index > 0) HorizontalDivider(Modifier.padding(vertical = 12.dp), color = MaterialTheme.colorScheme.outlineVariant)
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Bar(64.dp, 64.dp)
                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Bar(150.dp, 20.dp)
                    Bar(180.dp, 16.dp)
                }
            }
        }
    }
}

/**
 * The sum the not-paid report says the customer will owe, in the board's money format as the mark-collected
 * confirm writes it, or null when the wire carried no usable total — the confirmation then asks without
 * naming an amount rather than guessing one.
 *
 * The `> 0` guard is not cosmetic: "The customer will owe 0 Kč" reads as a bug
 * and is worse than the amount-free copy. iOS pins the same rule in
 * `OrderDetail.cashDueLabel`.
 */
internal fun cashDueLabel(
    totalPrice: Double?,
    currencySymbol: String?,
    locale: Locale = Locale.getDefault(),
): String? = totalPrice
    ?.takeIf { it > 0 }
    ?.let { OrdersBoardRules.money(it, currencySymbol, locale) }

/** The cash to collect as the pinned button and its confirm both write it; null names no amount. */
internal fun OrderItem.cashDueText(locale: Locale): String? =
    amountDueOnCard?.takeIf { it > 0 }?.let { OrdersBoardRules.money(it, OrdersBoardRules.symbolOf(asBoardRow()), locale) }

/**
 * A cash order reaches the door unpaid, a recurring occurrence the customer confirmed included, and the
 * server refuses CompleteOrder until the partner records the cash.
 */
internal fun OrderItem.needsCashCollection(): Boolean =
    paymentType?.value == PaymentType._1.value && paymentStatus?.value != PaymentStatus._2.value

/**
 * Mirrors `CompleteOrder.CardIsCharged`: a card job completes only once Stripe has charged it (paid, refunded or
 * partly refunded). A status that did not arrive is not read as a refusal.
 */
internal fun OrderItem.cardCharged(): Boolean {
    if (paymentType?.value != PaymentType._2.value) return true
    val status = paymentStatus?.value ?: return true
    return status == PaymentStatus._2.value || status == PaymentStatus._4.value || status == PaymentStatus._6.value
}

/**
 * On a cash job the customer's price is what the cleaner collects at the door; on any other job they
 * see their own pay only.
 */
internal fun OrderItem.showsCustomerPrice(): Boolean = paymentType?.value == PaymentType._1.value

/**
 * Mirrors `ReportCashNotPaid`: the cleaner on a cash job in progress whose payment is still pending may
 * report that the customer paid nothing. A refunded or disputed payment is not a debt to report.
 */
internal fun OrderItem.cashNotPaidReportable(): Boolean =
    isAssignedToCurrentUser == true &&
        orderStatus.toOrderStatus() == OrderStatus._4 &&
        paymentType?.value == PaymentType._1.value &&
        paymentStatus?.value == PaymentStatus._1.value

/** What the report makes the customer owe: the price less any credit applied, or null when either is missing. */
internal fun OrderItem.cashNotPaidOwed(): Double? {
    val price = totalPrice ?: return null
    val credit = creditAppliedAmount ?: return null
    return price - credit
}

/** The booked start, or null when the wire carries none that reads as an instant. */
internal fun OrderItem.cleaningStart(): Instant? = cleaningDateTime?.let { runCatching { Instant.parse(it) }.getOrNull() }

/**
 * Mirrors `DropOrder`, which lets the crew leave a New or Confirmed job only. The apps are narrower than the server in
 * two ways: before a lockout report only, because leaving afterwards gives up the reward a confirmed lockout pays, and
 * before the job's start only, because a job nobody has started by then is a no-show, which the administrators handle.
 * Without a start on the wire the status alone decides.
 */
internal fun OrderItem.droppable(now: Instant): Boolean {
    if (isAssignedToCurrentUser != true || !lockoutReportedAt.isNullOrBlank()) return false
    val start = cleaningStart()
    if (start != null && !now.isBefore(start)) return false
    return when (orderStatus.toOrderStatus()) {
        OrderStatus._0, OrderStatus._2 -> true
        else -> false
    }
}
