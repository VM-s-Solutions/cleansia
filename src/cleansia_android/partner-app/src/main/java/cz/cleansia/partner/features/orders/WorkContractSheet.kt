package cz.cleansia.partner.features.orders

import android.text.format.DateFormat
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.Crossfade
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.MutableTransitionState
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.slideInVertically
import androidx.compose.foundation.Image
import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.background
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.SheetValue
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.money.CurrencySymbols
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.HtmlContentView
import cz.cleansia.core.ui.components.OrderCalendarLine
import cz.cleansia.core.ui.components.SwipeToConfirmButton
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.data.orders.WorkContract
import cz.cleansia.partner.data.orders.WorkContractAcceptanceFacts
import cz.cleansia.partner.data.orders.WorkContractJobFacts
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlin.math.abs
import kotlinx.coroutines.delay

/**
 * The contract for work, on one screen with the gesture that accepts it: the job's facts, a plain summary when
 * the version's clauses support it, the text, and the swipe beneath. A take and a standalone acceptance differ
 * only in what the swipe calls; a read shows the stored facts and the accepted version with no swipe at all.
 *
 * Opened from the Orders board, the take carries the board row: its facts show at once while the preview
 * loads, and a refusal keeps the sheet open with the reason. A take turns the sheet into its taken state for
 * any host that says so ([taken]), with the street the take unlocked once the host has read it ([takenRow]).
 * The detail and Dashboard-offers hosts pass no board row and handle a refusal themselves.
 *
 * Every open re-loads — the facts are the server's word at that moment, never the list row's.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WorkContractSheet(
    request: WorkContractRequest,
    onDismiss: () -> Unit,
    onOutcome: (WorkContractOutcome) -> Unit,
    onOpenLegalDocuments: () -> Unit,
    taken: Boolean = false,
    takenRow: OrderListItem? = null,
    boardRefusal: String? = null,
    onOpenJob: () -> Unit = {},
    onBackToJobs: (() -> Unit)? = null,
    viewModel: WorkContractSheetViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val actionState by viewModel.actionState.collectAsStateWithLifecycle()
    val notice by viewModel.notice.collectAsStateWithLifecycle()
    val submitting = actionState is ActionState.Submitting
    val submittingNow by rememberUpdatedState(submitting)
    // Swipe-to-dismiss is refused while the take is in flight, as Close is.
    val sheetState = rememberModalBottomSheetState(
        skipPartiallyExpanded = true,
        confirmValueChange = { it != SheetValue.Hidden || !submittingNow },
    )

    LaunchedEffect(viewModel, request) { viewModel.open(request) }
    LaunchedEffect(viewModel) { viewModel.outcome.collect(onOutcome) }

    ModalBottomSheet(
        onDismissRequest = closeWhenIdle(submitting, onDismiss),
        sheetState = sheetState,
        modifier = Modifier.windowInsetsPadding(WindowInsets.statusBars),
        containerColor = MaterialTheme.colorScheme.surface,
        dragHandle = { SheetHandle() },
    ) {
        WorkContractSheetContent(
            request = request,
            uiState = uiState,
            notice = notice,
            submitting = submitting,
            onAccept = viewModel::accept,
            onRetry = viewModel::retry,
            onClose = closeWhenIdle(submitting, onDismiss),
            onOpenLegalDocuments = onOpenLegalDocuments,
            taken = taken,
            takenRow = takenRow,
            boardRefusal = boardRefusal,
            onOpenJob = onOpenJob,
            onBackToJobs = onBackToJobs,
        )
    }
}

@Composable
fun WorkContractSheetContent(
    request: WorkContractRequest,
    uiState: WorkContractUiState,
    notice: WorkContractNotice?,
    submitting: Boolean,
    onAccept: () -> Unit,
    onRetry: () -> Unit,
    onClose: () -> Unit,
    onOpenLegalDocuments: () -> Unit,
    taken: Boolean = false,
    takenRow: OrderListItem? = null,
    boardRefusal: String? = null,
    onOpenJob: () -> Unit = {},
    onBackToJobs: (() -> Unit)? = null,
    now: Instant = remember { Instant.now() },
) {
    val board = (request as? WorkContractRequest.Take)?.board
    val loaded = (uiState as? WorkContractUiState.Loaded)?.contract
    val facts = board?.row ?: takenRow
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .then(if (taken && facts != null) Modifier else Modifier.fillMaxHeight())
            .padding(horizontal = Spacing.M)
            .navigationBarsPadding(),
    ) {
        if (taken && facts != null) {
            TakenContent(
                facts = facts,
                takenRow = takenRow,
                version = loaded?.version,
                contractFacts = loaded?.facts,
                now = now,
                onBackToJobs = onBackToJobs ?: onClose,
                onOpenJob = onOpenJob,
            )
        } else {
            ContractStates(
                request = request,
                uiState = uiState,
                board = board,
                loaded = loaded,
                notice = notice,
                submitting = submitting,
                boardRefusal = boardRefusal,
                now = now,
                onAccept = onAccept,
                onRetry = onRetry,
                onClose = onClose,
                onOpenLegalDocuments = onOpenLegalDocuments,
            )
        }
    }
}

@Composable
private fun ContractStates(
    request: WorkContractRequest,
    uiState: WorkContractUiState,
    board: BoardTake?,
    loaded: WorkContract?,
    notice: WorkContractNotice?,
    submitting: Boolean,
    boardRefusal: String?,
    now: Instant,
    onAccept: () -> Unit,
    onRetry: () -> Unit,
    onClose: () -> Unit,
    onOpenLegalDocuments: () -> Unit,
) {
    val body = rememberScrollState()
    Column(Modifier.fillMaxSize()) {
        SheetTitleRow(
            title = when {
                board != null -> stringResource(R.string.take_sheet_title)
                else -> loaded?.title ?: stringResource(R.string.work_contract_title)
            },
            onClose = onClose,
            enabled = !submitting,
            scrolled = uiState is WorkContractUiState.Loaded && body.value > 0,
        )

        Crossfade(targetState = uiState, animationSpec = tween(200), label = "work-contract-state") { state ->
            Column(Modifier.fillMaxSize()) {
                when (state) {
                    WorkContractUiState.Loading ->
                        if (board != null) {
                            BoardLoading(board = board, now = now)
                        } else {
                            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
                        }

                    WorkContractUiState.Error -> SheetMessage(
                        text = stringResource(R.string.work_contract_load_error),
                        ctaLabel = stringResource(R.string.retry),
                        onCta = onRetry,
                    )

                    WorkContractUiState.Unavailable -> SheetMessage(
                        text = stringResource(R.string.error_legal_document_not_found),
                        ctaLabel = stringResource(R.string.close),
                        onCta = onClose,
                    )

                    WorkContractUiState.LegalDocumentsNotAccepted -> SheetMessage(
                        text = stringResource(R.string.error_employee_legal_documents_not_accepted),
                        ctaLabel = stringResource(R.string.legal_documents_review),
                        onCta = onOpenLegalDocuments,
                    )

                    is WorkContractUiState.Loaded -> LoadedContract(
                        request = request,
                        contract = state.contract,
                        body = body,
                        board = board,
                        notice = notice,
                        submitting = submitting,
                        refusal = boardRefusal,
                        now = now,
                        onAccept = onAccept,
                        onClose = onClose,
                    )
                }
            }
        }
    }
}

/** Board context, preview still loading: the card's facts at once, skeletons for the rest, the swipe waiting. */
@Composable
private fun ColumnScope.BoardLoading(board: BoardTake, now: Instant) {
    val rules = OrdersBoardRules
    val row = board.row
    val locale = appLocale()
    Spacer(Modifier.height(Spacing.XXS))
    FactsCard(
        start = rules.start(row),
        minutes = rules.minutes(row),
        area = rules.areaLine(row),
        band = rules.bandText(row.homeDistanceBand)?.text(),
        scope = rules.scopeItems(row, locale.language).map { it.text() },
        reward = rules.payText(row, locale),
        payUpdated = null,
        now = now,
    )
    Spacer(Modifier.height(Spacing.M))
    SkeletonBars(listOf(1f, 0.6f))
    Spacer(Modifier.height(Spacing.M))
    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant, modifier = Modifier.offsetHorizontal())
    Spacer(Modifier.height(Spacing.M))
    SkeletonBars(listOf(1f, 0.8f, 0.9f))
    Spacer(Modifier.weight(1f))
    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant, modifier = Modifier.offsetHorizontal())
    Spacer(Modifier.height(Spacing.S))
    val loading = stringResource(R.string.work_contract_loading)
    SwipeToConfirmButton(
        text = loading,
        onConfirmed = {},
        modifier = Modifier.slideSemantics(label = loading, busyLabel = loading, enabled = false, isBusy = false, onCommit = {}, disabledReason = loading),
        enabled = false,
        maxLines = 2,
        thumbLanes = true,
    )
    Spacer(Modifier.height(Spacing.L))
}

@Composable
private fun SkeletonBars(widths: List<Float>) {
    Column(verticalArrangement = Arrangement.spacedBy(Spacing.XS)) {
        widths.forEach { fraction ->
            Box(
                Modifier
                    .fillMaxWidth(fraction)
                    .height(12.dp)
                    .background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(6.dp)),
            )
        }
    }
}

@Composable
private fun ColumnScope.LoadedContract(
    request: WorkContractRequest,
    contract: WorkContract,
    body: ScrollState,
    board: BoardTake?,
    notice: WorkContractNotice?,
    submitting: Boolean,
    refusal: String?,
    now: Instant,
    onAccept: () -> Unit,
    onClose: () -> Unit,
) {
    val locale = appLocale()
    val rules = OrdersBoardRules
    val facts = contract.facts
    val snapshot = board?.row
    // The board's money format, so the reward reads exactly as on the card it was taken from.
    val reward = rules.money(facts.reward, CurrencySymbols.forCode(facts.currencyCode), locale)
    val payUpdated = snapshot?.estimatedCleanerPay?.takeIf { abs(it - facts.reward) >= 0.005 }?.let { rules.payText(snapshot, locale) }
    val section = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold)
    // One scroll for the facts, the four lines, the rules and the contract; the slide stays pinned under it.
    Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(body)) {
        Spacer(Modifier.height(Spacing.XXS))
        FactsCard(
            start = runCatching { Instant.parse(facts.cleaningDateTimeUtc) }.getOrNull(),
            minutes = facts.estimatedMinutes,
            area = facts.locationApproximate?.takeIf { it.isNotBlank() },
            band = snapshot?.let { rules.bandText(it.homeDistanceBand)?.text() },
            scope = factsScope(facts, snapshot, locale.language),
            reward = reward,
            payUpdated = payUpdated,
            now = now,
        )
        contract.acceptance?.let { acceptance ->
            Spacer(Modifier.height(Spacing.XS))
            AcceptanceFacts(acceptance = acceptance, renderedLanguage = contract.language)
        }
        if (rules.summaryVisible(request, contract.version)) Summary()
        if (snapshot != null) BoardRules(row = snapshot, version = contract.version, reward = reward, now = now)
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant, modifier = Modifier.padding(top = Spacing.M).offsetHorizontal())
        Text(
            text = stringResource(R.string.work_contract_title),
            style = section,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.padding(top = Spacing.M),
        )
        Text(
            text = stringResource(R.string.work_contract_version, contract.version) + OrdersBoardRules.SEP + stringResource(R.string.work_contract_parties),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 2.dp),
        )
        HtmlContentView(html = contract.contentHtml, modifier = Modifier.fillMaxWidth().padding(top = 6.dp, bottom = Spacing.M))
    }
    if (notice == WorkContractNotice.TextUpdated) {
        Spacer(Modifier.height(Spacing.XS))
        NoticeRow(text = stringResource(R.string.error_contract_text_mismatch))
    }
    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant, modifier = Modifier.offsetHorizontal())
    Spacer(Modifier.height(Spacing.S))
    when {
        board != null && refusal != null && !submitting -> RefusedBlock(reason = refusal, onBackToJobs = onClose)
        request is WorkContractRequest.Take || request is WorkContractRequest.Accept -> {
            val idle = stringResource(R.string.work_contract_swipe_to_accept)
            val busy = stringResource(R.string.work_contract_accepting)
            SwipeToConfirmButton(
                text = idle,
                onConfirmed = onAccept,
                modifier = Modifier
                    .slideSemantics(label = idle, busyLabel = busy, enabled = true, isBusy = submitting, onCommit = onAccept)
                    .announcePolitely(),
                isBusy = submitting,
                busyText = busy,
                maxLines = 2,
                thumbLanes = true,
            )
        }
        else -> CleansiaPrimaryButton(text = stringResource(R.string.close), onClick = onClose)
    }
    Spacer(Modifier.height(Spacing.L))
}

/** The sheet stays while the take is in flight, so its outcome reaches the board that opened it. */
internal fun closeWhenIdle(submitting: Boolean, onDismiss: () -> Unit): () -> Unit = { if (!submitting) onDismiss() }

/** *Take this job?* ‖ *Close*: the sheet's title row, opaque over the body, ruled once the body has scrolled under it. */
@Composable
private fun SheetTitleRow(title: String?, onClose: () -> Unit, enabled: Boolean = true, scrolled: Boolean = false) {
    Column(Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.surface)) {
        Row(Modifier.fillMaxWidth().heightIn(min = 48.dp).padding(vertical = Spacing.XXS), verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = title.orEmpty(),
                style = MaterialTheme.typography.headlineSmall,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f).semantics { heading() },
            )
            if (enabled) {
                // A least width of its own lifts the text button's 58 dp minimum, so the short word ends 4 inside the link.
                CleansiaTextLink(text = stringResource(R.string.close), onClick = onClose, modifier = Modifier.offset(x = LinkPadding).widthIn(min = 1.dp))
            } else {
                Text(
                    text = stringResource(R.string.close),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.38f),
                    modifier = Modifier.padding(horizontal = LinkPadding),
                )
            }
        }
        HorizontalDivider(color = if (scrolled) MaterialTheme.colorScheme.outlineVariant else Color.Transparent, modifier = Modifier.offsetHorizontal())
    }
}

/** The text link's own padding: Close's words end on the sheet's text edge. */
private val LinkPadding = 4.dp

/** The sheet's handle: 36 by 4 in the outline variant, 8 under the sheet's top. */
@Composable
internal fun SheetHandle() {
    Box(Modifier.padding(top = Spacing.XS).size(width = 36.dp, height = 4.dp).background(MaterialTheme.colorScheme.outlineVariant, CircleShape))
}

/** Services and packages by the row's translated name for the same id, then rooms, baths and short extras. */
@Composable
private fun factsScope(facts: WorkContractJobFacts, row: OrderListItem?, lang: String): List<String> {
    fun translated(ids: List<String>, names: List<String>, lookup: (String) -> String?): List<String> =
        names.mapIndexed { i, name -> ids.getOrNull(i)?.let(lookup) ?: name }
    val packages = translated(facts.packageIds, facts.packages) { id ->
        row?.selectedPackages?.firstOrNull { it.id == id }?.let { resolveTranslatedName(it.translations, lang, null) }
    }
    val services = translated(facts.serviceIds, facts.services) { id ->
        row?.selectedServices?.firstOrNull { it.id == id }?.let { resolveTranslatedName(it.translations, lang, null) }
    }
    val parts = listOfNotNull(
        (packages + services).filter(String::isNotBlank).takeIf { it.isNotEmpty() }?.let { BoardText.Literal(it.joinToString(", ")) },
        facts.rooms.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.scope_rooms, it) },
        facts.bathrooms.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.scope_baths, it) },
        OrdersBoardRules.extrasLine(facts.extraSlugs),
    )
    return parts.map { it.text() }
}

/**
 * The facts in a well, in the job head's own slots: how far off the day is, the calendar line, the place, the scope;
 * then the reward.
 */
@Composable
private fun FactsCard(
    start: Instant?,
    minutes: Int,
    area: String?,
    band: String?,
    scope: List<String>,
    reward: String?,
    payUpdated: String?,
    now: Instant,
    modifier: Modifier = Modifier,
) {
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val rules = OrdersBoardRules
    val scheme = MaterialTheme.colorScheme
    Column(
        modifier = modifier
            .fillMaxWidth()
            .background(scheme.surfaceVariant, MaterialTheme.shapes.medium)
            .padding(Spacing.M),
    ) {
        start?.let { Text(text = notTakenDay(it, now, zone).text(), style = MaterialTheme.typography.titleLarge, color = scheme.onSurface) }
        calendarDay(start, zone)?.let { OrderCalendarLine(day = it, window = rules.window(start, minutes, zone, locale), modifier = Modifier.padding(top = 2.dp)) }
        Column(Modifier.padding(top = Spacing.XS)) {
            area?.let { Text(text = it, style = MaterialTheme.typography.bodyLarge, color = scheme.onSurface) }
            band?.let { Text(text = it, style = MaterialTheme.typography.bodyMedium, color = scheme.onSurfaceVariant, modifier = Modifier.padding(top = 2.dp)) }
        }
        if (scope.isNotEmpty()) {
            DotFlow(
                items = scope.map { AnnotatedString(it) },
                style = MaterialTheme.typography.bodyMedium,
                color = scheme.onSurfaceVariant,
                modifier = Modifier.padding(top = Spacing.S),
                tieLast = true,
            )
        }
        if (reward != null) {
            HorizontalDivider(color = scheme.outlineVariant, modifier = Modifier.padding(top = Spacing.S))
            Row(modifier = Modifier.padding(top = Spacing.S)) {
                Text(
                    text = stringResource(R.string.work_contract_reward),
                    style = MaterialTheme.typography.bodyLarge,
                    color = scheme.onSurface,
                    modifier = Modifier.weight(1f).alignByBaseline(),
                )
                Text(
                    text = reward,
                    style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                    color = scheme.onSurface,
                    softWrap = false,
                    modifier = Modifier.alignByBaseline(),
                )
            }
        }
        if (payUpdated != null) {
            Text(
                text = stringResource(R.string.take_pay_updated, payUpdated),
                style = MaterialTheme.typography.bodyMedium,
                color = scheme.onSurface,
                modifier = Modifier.padding(top = Spacing.XS),
            )
        }
    }
}

/** Ruling 4: four plain lines above the contract, on the versions whose clauses they are true to. */
@Composable
private fun Summary() {
    val body = MaterialTheme.typography.bodyMedium
    Column(modifier = Modifier.padding(top = Spacing.M)) {
        Text(
            text = OrdersBoardRules.tie(stringResource(R.string.work_contract_summary_lead)),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        listOf(
            R.string.work_contract_summary_lockout,
            R.string.work_contract_summary_late_cancel,
            R.string.work_contract_summary_complaint,
            R.string.work_contract_summary_equipment,
        ).forEach { line ->
            Row(Modifier.padding(top = 6.dp)) {
                Box(xBandAligned(body).padding(start = 3.dp).size(6.dp).background(primaryText(), CircleShape))
                Text(text = OrdersBoardRules.tie(stringResource(line)), style = body, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.padding(start = 7.dp).alignBy(FirstBaseline))
            }
        }
    }
}

/** The board's own lines, from the row snapshot: cancelling, a team job, a cash job. */
@Composable
private fun BoardRules(row: OrderListItem, version: String, reward: String, now: Instant) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val line = rules.cancelLine(rules.status(row), rules.start(row), now, version)
    rules.cancelText(line, TakePhase.BeforeTake)?.let { res ->
        RuleText(cancelSentence(res, line))
    }
    row.requiredEmployees?.takeIf { it > 1 }?.let { required ->
        RuleText(stringResource(R.string.take_team_line, pluralStringResource(R.plurals.crew_size, required, required)))
    }
    if (rules.isCash(row) && version in rules.RELEASE_VERSIONS) {
        row.amountDueOnCard?.let { due ->
            val symbol = row.currency?.symbol?.takeIf { it.isNotBlank() } ?: CurrencySymbols.forCode(row.currency?.code)
            RuleText(stringResource(R.string.take_cash_line, rules.money(due, symbol, locale), reward))
        }
    }
}

@Composable
private fun cancelSentence(res: Int, line: CancelLine?): String {
    val locale = appLocale()
    return if (line is CancelLine.FreeUntil) {
        val pattern = DateFormat.getBestDateTimePattern(locale, "EEEdMMM")
        val local = line.start.atZone(ZoneId.systemDefault())
        stringResource(res, DateTimeFormatter.ofPattern(pattern, locale).format(local) + ", " + DateTimeFormatter.ofPattern("HH:mm", locale).format(local))
    } else {
        stringResource(res)
    }
}

@Composable
private fun RuleText(text: String) {
    Text(text = OrdersBoardRules.tie(text), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.padding(top = Spacing.S))
}

/** Refused on the board: the server's reason in its existing words, focused and announced, and the way back. */
@Composable
private fun RefusedBlock(reason: String, onBackToJobs: () -> Unit) {
    val focus = remember { FocusRequester() }
    LaunchedEffect(reason) { runCatching { delay(250); focus.requestFocus() } }
    Column(modifier = Modifier.fillMaxWidth()) {
        FlagFlow(
            Modifier
                .focusRequester(focus)
                .focusable()
                .announceAssertively(),
        ) { OrderFlag(text = reason, tone = FlagTone.Negative, icon = Icons.Outlined.ErrorOutline) }
        CleansiaPrimaryButton(text = stringResource(R.string.take_back_to_jobs), onClick = onBackToJobs, modifier = Modifier.padding(top = Spacing.S))
    }
}

/**
 * The taken state: the title with the thumbs-up figure, the newly revealed door on a small map with its street (no map
 * until the host's read lands, none when the row has no geocode), the day square beside the day and window and the
 * area and reward, the cancel rule on one quiet line, then *Open job* and *Back to jobs*. Taps wait 300 ms so the
 * swipe's own finger cannot press a button that just appeared.
 */
@Composable
private fun ColumnScope.TakenContent(
    facts: OrderListItem,
    takenRow: OrderListItem?,
    version: String?,
    contractFacts: WorkContractJobFacts?,
    now: Instant,
    onBackToJobs: () -> Unit,
    onOpenJob: () -> Unit,
) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val row = takenRow ?: facts
    val visible = remember { MutableTransitionState(false).apply { targetState = true } }
    var armed by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        delay(300)
        armed = true
    }
    val title = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { delay(350); title.requestFocus() } }
    val easing = CubicBezierEasing(0.22f, 1f, 0.36f, 1f)
    val rise = with(LocalDensity.current) { 12.dp.roundToPx() }
    val scheme = MaterialTheme.colorScheme
    AnimatedVisibility(
        visibleState = visible,
        enter = fadeIn(tween(320, easing = easing)) + slideInVertically(tween(320, easing = easing)) { rise },
    ) {
        Column(Modifier.fillMaxWidth()) {
            Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState())) {
                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Bottom) {
                    Text(
                        text = stringResource(R.string.take_done_title),
                        style = MaterialTheme.typography.headlineSmall,
                        color = scheme.onSurface,
                        modifier = Modifier
                            .weight(1f)
                            .padding(bottom = Spacing.S)
                            .focusRequester(title)
                            .focusable()
                            .semantics { heading() }
                            .announcePolitely(),
                    )
                    Image(painter = painterResource(R.drawable.mascot_thumbs_up), contentDescription = null, modifier = Modifier.size(96.dp))
                }
                takenStripPoint(takenRow)?.let { point ->
                    JobMapStrip(point = point, street = rules.streetLine(takenRow?.customerAddress), modifier = Modifier.padding(top = Spacing.XS))
                }
                val start = contractFacts?.cleaningDateTimeUtc?.let { runCatching { Instant.parse(it) }.getOrNull() } ?: rules.start(row)
                val minutes = contractFacts?.estimatedMinutes ?: rules.minutes(row)
                val day = start?.let { rules.dayName(it.atZone(zone).toLocalDate(), now.atZone(zone).toLocalDate(), locale).text() }
                val pay = rules.payText(row, locale)
                val reward = pay?.let { "${stringResource(if ((row.requiredEmployees ?: 1) > 1) R.string.orders_card_your_share else R.string.work_contract_reward)} $it" }
                Row(
                    modifier = Modifier.fillMaxWidth().padding(top = Spacing.M).semantics(mergeDescendants = true) {},
                    horizontalArrangement = Arrangement.spacedBy(Spacing.S),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    OrdersDateTile(start = start, size = TileSize.Row, now = now)
                    Column(Modifier.weight(1f)) {
                        DotFlow(
                            items = listOfNotNull(day, rules.window(start, minutes, zone, locale)).map { AnnotatedString(it) },
                            style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                            color = scheme.onSurface,
                        )
                        DotFlow(
                            items = listOfNotNull(rules.areaLine(row) ?: rules.areaLine(facts), reward).map { AnnotatedString(it) },
                            style = MaterialTheme.typography.bodyMedium,
                            color = scheme.onSurfaceVariant,
                            modifier = Modifier.padding(top = 2.dp),
                        )
                    }
                }
                val line = rules.cancelLine(rules.status(row), start, now, version)
                rules.cancelText(line, TakePhase.Taken)?.let { res ->
                    Row(Modifier.padding(top = Spacing.M), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        val style = MaterialTheme.typography.bodyMedium
                        Icon(Icons.Outlined.Info, contentDescription = null, tint = scheme.onSurfaceVariant, modifier = capBandAligned(style).size(16.dp))
                        Text(text = rules.tie(cancelSentence(res, line)), style = style, color = scheme.onSurfaceVariant, modifier = Modifier.alignBy(FirstBaseline))
                    }
                }
                Spacer(Modifier.height(Spacing.M))
            }
            CleansiaPrimaryButton(text = stringResource(R.string.orders_open_job), onClick = { if (armed) onOpenJob() })
            CleansiaTextButton(onClick = { if (armed) onBackToJobs() }, modifier = Modifier.fillMaxWidth().padding(top = Spacing.XS)) {
                Text(stringResource(R.string.take_back_to_jobs))
            }
            Spacer(Modifier.height(Spacing.M))
        }
    }
}

@Composable
private fun AcceptanceFacts(acceptance: WorkContractAcceptanceFacts, renderedLanguage: String?) {
    Column(verticalArrangement = Arrangement.spacedBy(Spacing.XXS)) {
        Text(
            text = stringResource(
                R.string.work_contract_accepted_on,
                formatOrderDateTime(acceptance.acceptedOn),
                acceptance.documentVersion,
            ),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        val accepted = acceptance.acceptedLanguage?.takeIf { it.isNotBlank() }
        if (accepted != null && renderedLanguage != null && !accepted.equals(renderedLanguage, ignoreCase = true)) {
            Text(
                text = stringResource(
                    R.string.work_contract_accepted_in_language,
                    Locale.forLanguageTag(accepted).getDisplayLanguage(Locale.getDefault()),
                ),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun NoticeRow(text: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.tertiaryContainer, RoundedCornerShape(12.dp))
            .padding(Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(Spacing.XS),
    ) {
        Icon(
            imageVector = Icons.Outlined.Info,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onTertiaryContainer,
            modifier = Modifier.size(18.dp),
        )
        Text(
            text = text,
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onTertiaryContainer,
        )
    }
}

@Composable
private fun SheetMessage(text: String, ctaLabel: String, onCta: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = Spacing.XL),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(Spacing.M),
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        CleansiaPrimaryButton(text = ctaLabel, onClick = onCta)
        Spacer(Modifier.height(Spacing.M))
    }
}

@Preview
@Composable
private fun WorkContractSheetPreview() {
    CleansiaPartnerTheme {
        WorkContractSheetContent(
            request = WorkContractRequest.Take("order-1"),
            uiState = WorkContractUiState.Loaded(
                WorkContract(
                    legalDocumentTextId = "text-1",
                    version = "2026-10-27",
                    language = "en",
                    title = "Contract for Work",
                    contentHtml = "<p>This contract for work is concluded between the operating company (the client) and you (the contractor).</p>",
                    facts = WorkContractJobFacts(
                        orderNumber = "CL-2026-0042",
                        cleaningDateTimeUtc = "2026-08-12T09:00:00Z",
                        estimatedMinutes = 180,
                        reward = 620.0,
                        currencyCode = "CZK",
                        locationApproximate = "Praha 4",
                        rooms = 3,
                        bathrooms = 1,
                        services = listOf("Standard cleaning"),
                        packages = emptyList(),
                        extraSlugs = listOf("inside-oven"),
                    ),
                    acceptance = null,
                ),
            ),
            notice = WorkContractNotice.TextUpdated,
            submitting = false,
            onAccept = {},
            onRetry = {},
            onClose = {},
            onOpenLegalDocuments = {},
        )
    }
}
