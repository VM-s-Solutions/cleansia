package cz.cleansia.customer.features.recurring

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material.icons.outlined.LightMode
import androidx.compose.material.icons.outlined.NightlightRound
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Remove
import androidx.compose.material.icons.outlined.WbSunny
import androidx.compose.material3.Button
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.features.addresses.AddressManagerSheet
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.EntryPointAccessors
import kotlinx.datetime.Clock
import kotlinx.datetime.DateTimeUnit
import kotlinx.datetime.Instant
import kotlinx.datetime.TimeZone
import kotlinx.datetime.atStartOfDayIn
import kotlinx.datetime.plus
import kotlinx.datetime.toLocalDateTime
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.time.format.TextStyle
import java.util.Locale

/**
 * Multi-step "create recurring booking" wizard. Mirrors the order-booking
 * sheet's UX so users get the same step indicator + slide transitions:
 *
 *  Step 1 — When:  Frequency · Day-of-week · Time-of-day
 *  Step 2 — What:  Packages · Services · Rooms · Bathrooms
 *  Step 3 — Where & Pay:  Address · Payment · Starts on
 *
 * Path A (blank) lands on Step 1; Path B (pre-filled from a Completed order)
 * also lands on Step 1 with most fields already populated — the user just
 * taps Next, Next, Create. The ViewModel keys on an optional `orderId` nav
 * arg to decide which mode it's in.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CreateRecurringScreen(
    onBack: () -> Unit,
    onCreated: () -> Unit,
    viewModel: CreateRecurringViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val submitting by viewModel.submitting.collectAsStateWithLifecycle()
    val outcome by viewModel.submitOutcome.collectAsStateWithLifecycle()

    val context = LocalContext.current
    // TODO(W3.3): refactor to VM injection — pull snackbar into
    // CreateRecurringViewModel like ProfileViewModel/OrderDetailViewModel.
    val snackbar = remember {
        EntryPointAccessors
            .fromApplication(context, RecurringSnackbarEntryPoint::class.java)
            .snackbarController()
    }

    var currentStep by remember { mutableIntStateOf(1) }
    var addressSheetOpen by remember { mutableStateOf(false) }

    // Default startsOn to "one week from today" once the form mounts so the
    // user doesn't see a blank field. They can edit via the calendar picker.
    LaunchedEffect(Unit) {
        if (state.startsOnIso.isBlank()) {
            val today = Clock.System.now().toLocalDateTime(TimeZone.currentSystemDefault()).date
            val nextWeek = today.plus(7, DateTimeUnit.DAY)
            viewModel.setStartsOn(nextWeek.atStartOfDayIn(TimeZone.currentSystemDefault()).toString())
        }
    }

    LaunchedEffect(outcome) {
        when (outcome) {
            SubmitOutcome.Success -> {
                snackbar.showSuccess(context.getString(R.string.recurring_create_success))
                viewModel.consumeOutcome()
                onCreated()
            }
            SubmitOutcome.Failed -> {
                snackbar.showError(context.getString(R.string.recurring_create_failed))
                viewModel.consumeOutcome()
            }
            null -> Unit
        }
    }

    val isPathB = viewModel.sourceOrderId != null

    val canAdvance = when (currentStep) {
        1 -> state.timeOfDay.isNotBlank()  // freq + day always have defaults
        2 -> state.selectedServiceIds.isNotEmpty() || state.selectedPackageIds.isNotEmpty()
        3 -> state.savedAddressId.isNotBlank() && state.startsOnIso.isNotBlank()
        else -> false
    }

    Scaffold(
        topBar = {
            WizardTopBar(
                currentStep = currentStep,
                isPathB = isPathB,
                onBack = {
                    if (currentStep > 1) currentStep-- else onBack()
                },
            )
        },
        bottomBar = {
            WizardBottomBar(
                currentStep = currentStep,
                canAdvance = canAdvance,
                submitting = submitting,
                onPrevious = { if (currentStep > 1) currentStep-- },
                onNext = {
                    if (currentStep < TOTAL_STEPS) currentStep++ else viewModel.submit()
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            // Step indicator strip — sits below the top bar. Mirrors the
            // booking sheet so users learn one wizard chrome.
            StepIndicator(currentStep = currentStep, totalSteps = TOTAL_STEPS)

            // Live summary banner — restates the user's choices in plain
            // language. Updates on every tap so users always see "what they
            // just chose" without scrolling back. Hidden on the very first
            // empty state to avoid showing placeholder copy.
            SummaryBanner(state = state)

            AnimatedContent(
                targetState = currentStep,
                transitionSpec = {
                    val forward = targetState > initialState
                    val slide = if (forward) 1 else -1
                    (slideInHorizontally(animationSpec = tween(220)) { it * slide } +
                        fadeIn(animationSpec = tween(220))) togetherWith
                        (slideOutHorizontally(animationSpec = tween(220)) { -it * slide } +
                            fadeOut(animationSpec = tween(220)))
                },
                label = "recurring-wizard-step",
            ) { step ->
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .padding(horizontal = 20.dp, vertical = 16.dp),
                ) {
                    when (step) {
                        1 -> WhenStep(state = state, viewModel = viewModel)
                        2 -> WhatStep(state = state, viewModel = viewModel)
                        3 -> WhereAndPayStep(
                            state = state,
                            viewModel = viewModel,
                            onOpenAddressSheet = { addressSheetOpen = true },
                        )
                    }
                    Spacer(Modifier.height(40.dp))
                }
            }
        }
    }

    // Inline address sheet — opened from the address picker on Step 3.
    AddressManagerSheet(
        visible = addressSheetOpen,
        onDismiss = { addressSheetOpen = false },
        onAddressSelected = { addr ->
            addr.serverId?.let { viewModel.setSavedAddressId(it) }
            addressSheetOpen = false
        },
    )
}

private const val TOTAL_STEPS = 3

/* ─────────────── Wizard chrome ─────────────── */

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun WizardTopBar(currentStep: Int, isPathB: Boolean, onBack: () -> Unit) {
    val titleRes = when (currentStep) {
        1 -> R.string.recurring_create_step_when_title
        2 -> R.string.recurring_create_step_what_title
        else -> R.string.recurring_create_step_where_pay_title
    }
    TopAppBar(
        title = {
            Column {
                Text(
                    text = stringResource(titleRes),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                )
                Text(
                    text = stringResource(
                        if (isPathB) R.string.recurring_create_title_from_order
                        else R.string.recurring_create_title_blank,
                    ),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        },
        navigationIcon = {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Outlined.ArrowBack, contentDescription = null)
            }
        },
    )
}

/**
 * Three step "pills" connected by lines, matching the booking sheet's
 * orientation but more visible for a full-screen wizard. Past = filled
 * checkmark, current = filled number, future = outlined number.
 */
@Composable
private fun StepIndicator(currentStep: Int, totalSteps: Int) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        for (step in 1..totalSteps) {
            val state = when {
                step < currentStep -> StepDotState.Done
                step == currentStep -> StepDotState.Current
                else -> StepDotState.Future
            }
            StepDot(step = step, state = state)
            if (step < totalSteps) {
                val connectorColor = if (step < currentStep) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.outlineVariant
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .height(2.dp)
                        .background(connectorColor),
                )
            }
        }
    }
}

/**
 * Plain-language restatement of the user's current selections. Sits between
 * the step indicator and the active step body — turns a column of abstract
 * controls into a concrete preview of what's about to be scheduled.
 *
 * Always renders (even on Step 1 with only the frequency picked) so the
 * user gets immediate feedback for every tap.
 */
@Composable
private fun SummaryBanner(state: CreateRecurringFormState) {
    val cadenceRes = when (state.frequency) {
        RecurrenceFrequency.Weekly -> R.string.recurring_summary_cadence_weekly
        RecurrenceFrequency.Biweekly -> R.string.recurring_summary_cadence_biweekly
        RecurrenceFrequency.Monthly -> R.string.recurring_summary_cadence_monthly
    }
    val javaDow = if (state.dayOfWeek == 0) 7 else state.dayOfWeek
    val dayName = java.time.DayOfWeek.of(javaDow)
        .getDisplayName(TextStyle.FULL, Locale.getDefault())
    val time = state.timeOfDay.ifBlank { "—" }
    val sentence = stringResource(cadenceRes, dayName, time)

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp, vertical = 4.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.08f))
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Outlined.CalendarMonth,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(10.dp))
        Text(
            text = sentence,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
    }
    Spacer(Modifier.height(8.dp))
}

private enum class StepDotState { Done, Current, Future }

@Composable
private fun StepDot(step: Int, state: StepDotState) {
    val (bg, fg, ring) = when (state) {
        StepDotState.Done -> Triple(
            MaterialTheme.colorScheme.primary,
            MaterialTheme.colorScheme.onPrimary,
            MaterialTheme.colorScheme.primary,
        )
        StepDotState.Current -> Triple(
            MaterialTheme.colorScheme.primary,
            MaterialTheme.colorScheme.onPrimary,
            MaterialTheme.colorScheme.primary,
        )
        StepDotState.Future -> Triple(
            MaterialTheme.colorScheme.surface,
            MaterialTheme.colorScheme.onSurfaceVariant,
            MaterialTheme.colorScheme.outlineVariant,
        )
    }
    Box(
        modifier = Modifier
            .size(28.dp)
            .clip(CircleShape)
            .background(bg)
            .border(1.dp, ring, CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        if (state == StepDotState.Done) {
            Icon(
                Icons.Outlined.Check,
                contentDescription = null,
                tint = fg,
                modifier = Modifier.size(16.dp),
            )
        } else {
            Text(
                text = step.toString(),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                color = fg,
            )
        }
    }
}

@Composable
private fun WizardBottomBar(
    currentStep: Int,
    canAdvance: Boolean,
    submitting: Boolean,
    onPrevious: () -> Unit,
    onNext: () -> Unit,
) {
    // navigationBarsPadding lifts the action row above the system gesture
    // indicator so Back / Next don't sit flush with the bottom bezel.
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface)
            .navigationBarsPadding()
            .padding(horizontal = 20.dp, vertical = 14.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (currentStep > 1) {
            OutlinedButton(
                onClick = onPrevious,
                enabled = !submitting,
                modifier = Modifier.weight(1f).height(54.dp),
            ) {
                Text(
                    text = stringResource(R.string.recurring_create_back),
                    style = MaterialTheme.typography.titleMedium,
                )
            }
        }
        Button(
            onClick = onNext,
            enabled = canAdvance && !submitting,
            modifier = Modifier.weight(if (currentStep > 1) 1f else 2f).height(54.dp),
        ) {
            Text(
                text = stringResource(
                    if (currentStep < TOTAL_STEPS) R.string.recurring_create_next
                    else R.string.recurring_create_submit,
                ),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
            )
        }
    }
}

/* ─────────────── Step 1 — When ─────────────── */

@Composable
private fun WhenStep(state: CreateRecurringFormState, viewModel: CreateRecurringViewModel) {
    SectionLabel(stringResource(R.string.recurring_create_frequency_label))
    Spacer(Modifier.height(8.dp))
    FrequencyChips(selected = state.frequency, onSelect = viewModel::setFrequency)

    Spacer(Modifier.height(24.dp))

    SectionLabel(stringResource(R.string.recurring_create_day_label))
    Spacer(Modifier.height(8.dp))
    DayOfWeekChips(selected = state.dayOfWeek, onSelect = viewModel::setDayOfWeek)

    Spacer(Modifier.height(24.dp))

    SectionLabel(stringResource(R.string.recurring_create_time_label))
    Spacer(Modifier.height(8.dp))
    TimeOfDayPicker(selected = state.timeOfDay, onSelect = viewModel::setTimeOfDay)
}

/* ─────────────── Step 2 — What ─────────────── */

@Composable
private fun WhatStep(state: CreateRecurringFormState, viewModel: CreateRecurringViewModel) {
    SectionLabel(stringResource(R.string.recurring_create_services_label))
    Spacer(Modifier.height(8.dp))
    ServicesPackagesPicker(
        selectedServiceIds = state.selectedServiceIds,
        selectedPackageIds = state.selectedPackageIds,
        onToggleService = viewModel::toggleService,
        onTogglePackage = viewModel::togglePackage,
    )

    Spacer(Modifier.height(24.dp))

    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        Column(modifier = Modifier.weight(1f)) {
            SectionLabel(stringResource(R.string.recurring_create_rooms_label))
            Spacer(Modifier.height(8.dp))
            Stepper(value = state.rooms, onChange = viewModel::setRooms)
        }
        Column(modifier = Modifier.weight(1f)) {
            SectionLabel(stringResource(R.string.recurring_create_bathrooms_label))
            Spacer(Modifier.height(8.dp))
            Stepper(value = state.bathrooms, onChange = viewModel::setBathrooms)
        }
    }
}

/* ─────────────── Step 3 — Where & Pay ─────────────── */

@Composable
private fun WhereAndPayStep(
    state: CreateRecurringFormState,
    viewModel: CreateRecurringViewModel,
    onOpenAddressSheet: () -> Unit,
) {
    SectionLabel(stringResource(R.string.recurring_create_address_label))
    Spacer(Modifier.height(8.dp))
    SavedAddressPicker(
        selectedId = state.savedAddressId,
        onSelect = viewModel::setSavedAddressId,
        onAddNew = onOpenAddressSheet,
    )

    Spacer(Modifier.height(24.dp))

    SectionLabel(stringResource(R.string.recurring_create_payment_label))
    Spacer(Modifier.height(8.dp))
    PaymentTypePicker(selected = state.paymentType, onSelect = viewModel::setPaymentType)

    Spacer(Modifier.height(24.dp))

    SectionLabel(stringResource(R.string.recurring_create_starts_label))
    Spacer(Modifier.height(8.dp))
    StartsOnPicker(isoValue = state.startsOnIso, onChange = viewModel::setStartsOn)
}

/* ─────────────── Section helpers ─────────────── */

@Composable
private fun SectionLabel(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onBackground,
    )
}

/**
 * Frequency cards — each shows label + cadence subline (e.g. "4 cleanings/
 * month") to make the abstract choice concrete. The biweekly option gets a
 * "Most popular" badge — that cadence really is the cleaning industry's
 * most-booked recurring rhythm and the badge cuts decision paralysis for
 * first-time users.
 */
@Composable
private fun FrequencyChips(selected: RecurrenceFrequency, onSelect: (RecurrenceFrequency) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        FrequencyOptionCard(
            label = stringResource(R.string.recurring_freq_weekly_label),
            subline = stringResource(R.string.recurring_freq_weekly_subline),
            badgeLabel = null,
            selected = selected == RecurrenceFrequency.Weekly,
            onClick = { onSelect(RecurrenceFrequency.Weekly) },
        )
        FrequencyOptionCard(
            label = stringResource(R.string.recurring_freq_biweekly_label),
            subline = stringResource(R.string.recurring_freq_biweekly_subline),
            badgeLabel = stringResource(R.string.recurring_freq_most_popular_badge),
            selected = selected == RecurrenceFrequency.Biweekly,
            onClick = { onSelect(RecurrenceFrequency.Biweekly) },
        )
        FrequencyOptionCard(
            label = stringResource(R.string.recurring_freq_monthly_label),
            subline = stringResource(R.string.recurring_freq_monthly_subline),
            badgeLabel = null,
            selected = selected == RecurrenceFrequency.Monthly,
            onClick = { onSelect(RecurrenceFrequency.Monthly) },
        )
    }
}

@Composable
private fun FrequencyOptionCard(
    label: String,
    subline: String,
    badgeLabel: String?,
    selected: Boolean,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = if (selected) 2.dp else 1.dp,
                color = if (selected) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(12.dp),
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = label,
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = if (selected) MaterialTheme.colorScheme.primary
                        else MaterialTheme.colorScheme.onSurface,
                )
                if (badgeLabel != null) {
                    Spacer(Modifier.width(8.dp))
                    Text(
                        text = badgeLabel,
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier
                            .clip(RoundedCornerShape(6.dp))
                            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
                            .padding(horizontal = 6.dp, vertical = 2.dp),
                    )
                }
            }
            Spacer(Modifier.height(2.dp))
            Text(
                text = subline,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        SelectionBadge(selected = selected)
    }
}

/**
 * Day-of-week strip — 7 fixed-width chips in Mon→Sun order using 2-letter
 * abbreviations (Mo Tu We Th Fr Sa Su) so users can distinguish Tue/Thu and
 * Sat/Sun at a glance. Backend dayOfWeek follows .NET DayOfWeek
 * (Sun=0..Sat=6); we map back at click.
 */
/**
 * Day-of-week strip — Mon→Fri rendered tightly, then a slightly larger gap
 * before Sat/Sun. The visual break tells the user "weekend lives here"
 * without needing labels. Weekend chips also use a subtler background tint
 * when unselected so they're recognizable without being shouty.
 *
 * Backend dayOfWeek follows .NET DayOfWeek (Sun=0..Sat=6); we map at click.
 */
@Composable
private fun DayOfWeekChips(selected: Int, onSelect: (Int) -> Unit) {
    // Mon=1..Fri=5 in display order, then weekend Sat=6, Sun=0.
    val weekdays = listOf(1, 2, 3, 4, 5)
    val weekend = listOf(6, 0)
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        weekdays.forEach { dow ->
            DayChip(
                modifier = Modifier.weight(1f),
                dow = dow,
                isWeekend = false,
                isSelected = selected == dow,
                onClick = { onSelect(dow) },
            )
        }
        // Weekend separator — 14dp gap (vs 6dp inter-chip) groups Sat/Sun
        // apart visually without a divider line.
        Spacer(Modifier.width(8.dp))
        weekend.forEach { dow ->
            DayChip(
                modifier = Modifier.weight(1f),
                dow = dow,
                isWeekend = true,
                isSelected = selected == dow,
                onClick = { onSelect(dow) },
            )
        }
    }
}

@Composable
private fun DayChip(
    modifier: Modifier,
    dow: Int,
    isWeekend: Boolean,
    isSelected: Boolean,
    onClick: () -> Unit,
) {
    val javaDow = if (dow == 0) 7 else dow
    val full = java.time.DayOfWeek.of(javaDow)
        .getDisplayName(TextStyle.FULL, Locale.getDefault())
    val label = (if (full.length >= 2) full.substring(0, 2) else full)
        .replaceFirstChar { it.titlecase(Locale.getDefault()) }

    val bg = when {
        isSelected -> MaterialTheme.colorScheme.surface
        isWeekend -> MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f)
        else -> MaterialTheme.colorScheme.surface
    }
    val borderColor = if (isSelected) MaterialTheme.colorScheme.primary
        else MaterialTheme.colorScheme.outlineVariant
    val textColor = if (isSelected) MaterialTheme.colorScheme.primary
        else MaterialTheme.colorScheme.onSurface

    Box(
        modifier = modifier
            .height(48.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(bg)
            .border(
                width = if (isSelected) 2.dp else 1.dp,
                color = borderColor,
                shape = RoundedCornerShape(12.dp),
            )
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = textColor,
        )
    }
}

/**
 * Time-of-day picker — slots grouped into Morning / Afternoon / Evening with
 * section labels and matching glyphs (sun rising / sun / moon). The grouping
 * gives users orientation ("ah, the cleaner comes in the morning") instead
 * of forcing them to mentally categorize a flat list of "08:00, 09:00…".
 */
@Composable
private fun TimeOfDayPicker(selected: String, onSelect: (String) -> Unit) {
    val morning = remember { (8..11).map { "%02d:00".format(it) } }
    val afternoon = remember { (12..16).map { "%02d:00".format(it) } }
    val evening = remember { (17..19).map { "%02d:00".format(it) } }

    Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        TimeSlotGroup(
            label = stringResource(R.string.recurring_time_period_morning),
            icon = Icons.Outlined.WbSunny,
            slots = morning,
            selected = selected,
            onSelect = onSelect,
        )
        TimeSlotGroup(
            label = stringResource(R.string.recurring_time_period_afternoon),
            icon = Icons.Outlined.LightMode,
            slots = afternoon,
            selected = selected,
            onSelect = onSelect,
        )
        TimeSlotGroup(
            label = stringResource(R.string.recurring_time_period_evening),
            icon = Icons.Outlined.NightlightRound,
            slots = evening,
            selected = selected,
            onSelect = onSelect,
        )
    }
}

@Composable
private fun TimeSlotGroup(
    label: String,
    icon: ImageVector,
    slots: List<String>,
    selected: String,
    onSelect: (String) -> Unit,
) {
    Column {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(14.dp),
            )
            Spacer(Modifier.width(6.dp))
            Text(
                text = label,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Spacer(Modifier.height(6.dp))
        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            items(slots) { slot ->
                OutlinedSelectableChip(
                    selected = slot == selected,
                    onClick = { onSelect(slot) },
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(
                        horizontal = 16.dp, vertical = 12.dp,
                    ),
                    content = {
                        Text(
                            text = slot,
                            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                            color = if (slot == selected) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.onSurface,
                        )
                    },
                )
            }
        }
    }
}

/**
 * Outlined selectable chip — matches the order booking time-slot pattern:
 * surface background, 2dp primary border + primary text when selected, plain
 * outlineVariant border otherwise. No filled state — keeps the visual
 * vocabulary consistent with the package/service cards on the next step.
 */
@Composable
private fun OutlinedSelectableChip(
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    contentPadding: androidx.compose.foundation.layout.PaddingValues =
        androidx.compose.foundation.layout.PaddingValues(0.dp),
    content: @Composable () -> Unit,
) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = if (selected) 2.dp else 1.dp,
                color = if (selected) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(12.dp),
            )
            .clickable(onClick = onClick)
            .padding(contentPadding),
        contentAlignment = Alignment.Center,
    ) {
        content()
    }
}

@Composable
private fun Stepper(value: Int, onChange: (Int) -> Unit) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(12.dp))
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(12.dp))
            .padding(horizontal = 4.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconButton(onClick = { onChange(value - 1) }, enabled = value > 0) {
            Icon(Icons.Outlined.Remove, contentDescription = null)
        }
        Text(
            text = value.toString(),
            modifier = Modifier.padding(horizontal = 12.dp),
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
        )
        IconButton(onClick = { onChange(value + 1) }) {
            Icon(Icons.Outlined.Add, contentDescription = null)
        }
    }
}

@Composable
private fun SavedAddressPicker(
    selectedId: String,
    onSelect: (String) -> Unit,
    onAddNew: () -> Unit,
) {
    val context = LocalContext.current
    // TODO(W3.3): refactor to VM injection — leaf private composable, would
    // need parent screen to lift the addresses flow + add-new callback.
    val addressRepo = remember {
        EntryPointAccessors
            .fromApplication(context, RecurringAddressEntryPoint::class.java)
            .addressRepository()
    }
    val addresses by addressRepo.addresses.collectAsState(initial = emptyList())

    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        addresses.forEach { addr ->
            val id = addr.serverId ?: return@forEach
            val selected = id == selectedId
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(12.dp))
                    .border(
                        width = if (selected) 2.dp else 1.dp,
                        color = if (selected) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.outlineVariant,
                        shape = RoundedCornerShape(12.dp),
                    )
                    .clickable { onSelect(id) }
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = addr.label.ifBlank { addr.oneLine },
                        style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                    )
                    Text(
                        text = addr.oneLine,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                if (addr.isDefault) {
                    Text(
                        text = stringResource(R.string.recurring_create_address_default),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier
                            .clip(RoundedCornerShape(6.dp))
                            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
                            .padding(horizontal = 6.dp, vertical = 2.dp),
                    )
                }
            }
        }

        // "+ Add new address" — always visible so users with zero addresses
        // can still proceed without leaving the form.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(12.dp))
                .border(
                    width = 1.dp,
                    color = MaterialTheme.colorScheme.primary.copy(alpha = 0.4f),
                    shape = RoundedCornerShape(12.dp),
                )
                .clickable(onClick = onAddNew)
                .padding(horizontal = 12.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                Icons.Outlined.Add,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                text = stringResource(R.string.recurring_create_address_add_new),
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

/**
 * Services + packages picker — full-width selectable cards. Package cards
 * additionally show a bulleted "Includes:" list of services contained in
 * the package, so users can compare like-for-like before tapping. Service
 * cards stay simple (title + optional description).
 */
@Composable
private fun ServicesPackagesPicker(
    selectedServiceIds: Set<String>,
    selectedPackageIds: Set<String>,
    onToggleService: (String) -> Unit,
    onTogglePackage: (String) -> Unit,
) {
    val context = LocalContext.current
    // TODO(W3.3): refactor to VM injection — leaf private composable, would
    // need parent screen to lift services/packages flows down as parameters.
    val catalog = remember {
        EntryPointAccessors
            .fromApplication(context, cz.cleansia.customer.core.catalog.CatalogRepositoryEntryPoint::class.java)
            .catalogRepository()
    }
    val services by catalog.services.collectAsState(initial = emptyList())
    val packages by catalog.packages.collectAsState(initial = emptyList())

    if (services.isEmpty() && packages.isEmpty()) {
        Text(
            text = stringResource(R.string.recurring_create_services_loading),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        return
    }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        if (packages.isNotEmpty()) {
            Text(
                text = stringResource(R.string.recurring_create_section_packages),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            packages.forEach { pkg ->
                val id = pkg.id ?: return@forEach
                val includedNames = pkg.includedServices
                    ?.mapNotNull { it.name?.takeIf { n -> n.isNotBlank() } }
                    ?.takeIf { it.isNotEmpty() }
                PackageCard(
                    title = pkg.name.orEmpty(),
                    description = pkg.description,
                    includedServices = includedNames,
                    selected = id in selectedPackageIds,
                    onClick = { onTogglePackage(id) },
                )
            }
        }
        if (services.isNotEmpty()) {
            if (packages.isNotEmpty()) Spacer(Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.recurring_create_section_services),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            services.forEach { svc ->
                val id = svc.id ?: return@forEach
                ServiceCard(
                    title = svc.name.orEmpty(),
                    description = svc.description,
                    selected = id in selectedServiceIds,
                    onClick = { onToggleService(id) },
                )
            }
        }
    }
}

@Composable
private fun PackageCard(
    title: String,
    description: String?,
    includedServices: List<String>?,
    selected: Boolean,
    onClick: () -> Unit,
) {
    Row(
        modifier = selectableCardModifier(selected = selected, onClick = onClick),
        verticalAlignment = Alignment.Top,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (!description.isNullOrBlank()) {
                Spacer(Modifier.height(2.dp))
                Text(
                    text = description,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (!includedServices.isNullOrEmpty()) {
                Spacer(Modifier.height(10.dp))
                Text(
                    text = stringResource(R.string.recurring_create_package_includes),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(4.dp))
                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    includedServices.forEach { name ->
                        Row(verticalAlignment = Alignment.Top) {
                            Text(
                                text = "• ",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Text(
                                text = name,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurface,
                            )
                        }
                    }
                }
            }
        }
        SelectionBadge(selected = selected)
    }
}

@Composable
private fun ServiceCard(
    title: String,
    description: String?,
    selected: Boolean,
    onClick: () -> Unit,
) {
    Row(
        modifier = selectableCardModifier(selected = selected, onClick = onClick),
        verticalAlignment = Alignment.Top,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (!description.isNullOrBlank()) {
                Spacer(Modifier.height(2.dp))
                Text(
                    text = description,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        SelectionBadge(selected = selected)
    }
}

/**
 * Shared modifier for the package + service selectable cards. Builds the
 * border + tinted background + click handler so both card variants stay
 * visually consistent without an extra wrapper composable.
 */
@Composable
private fun selectableCardModifier(selected: Boolean, onClick: () -> Unit): Modifier {
    val bg = if (selected) MaterialTheme.colorScheme.primary.copy(alpha = 0.06f)
        else MaterialTheme.colorScheme.surface
    val borderColor = if (selected) MaterialTheme.colorScheme.primary
        else MaterialTheme.colorScheme.outlineVariant
    return Modifier
        .fillMaxWidth()
        .clip(RoundedCornerShape(12.dp))
        .background(bg)
        .border(
            width = if (selected) 2.dp else 1.dp,
            color = borderColor,
            shape = RoundedCornerShape(12.dp),
        )
        .clickable(onClick = onClick)
        .padding(horizontal = 14.dp, vertical = 12.dp)
}

@Composable
private fun SelectionBadge(selected: Boolean) {
    if (!selected) return
    Spacer(Modifier.width(10.dp))
    Box(
        modifier = Modifier
            .size(22.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primary),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            Icons.Outlined.Check,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onPrimary,
            modifier = Modifier.size(14.dp),
        )
    }
}

/**
 * Payment type — two side-by-side cards with icon + label. Replaces the
 * old FilterChip pair so the visual weight matches the service/package
 * cards on Step 2 and the affordance is more obviously tappable.
 */
@Composable
private fun PaymentTypePicker(selected: Int, onSelect: (Int) -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        PaymentCard(
            modifier = Modifier.weight(1f),
            icon = Icons.Outlined.Payments,
            label = stringResource(R.string.recurring_create_pay_cash),
            selected = selected == 1,
            onClick = { onSelect(1) },
        )
        PaymentCard(
            modifier = Modifier.weight(1f),
            icon = Icons.Outlined.CreditCard,
            label = stringResource(R.string.recurring_create_pay_card),
            selected = selected == 2,
            onClick = { onSelect(2) },
        )
    }
}

@Composable
private fun PaymentCard(
    modifier: Modifier = Modifier,
    icon: ImageVector,
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(
                if (selected) MaterialTheme.colorScheme.primary.copy(alpha = 0.06f)
                else MaterialTheme.colorScheme.surface,
            )
            .border(
                width = if (selected) 2.dp else 1.dp,
                color = if (selected) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(12.dp),
            )
            .clickable(onClick = onClick)
            .padding(vertical = 16.dp, horizontal = 12.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(
                    if (selected) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.surfaceVariant,
                ),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = if (selected) MaterialTheme.colorScheme.onPrimary
                    else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(22.dp),
            )
        }
        Spacer(Modifier.height(10.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/**
 * Material3 DatePickerDialog — the starts-on date is anchored at midnight
 * in the user's local timezone; the actual time of day comes from Step 1.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun StartsOnPicker(isoValue: String, onChange: (String) -> Unit) {
    val tz = TimeZone.currentSystemDefault()
    val today = remember { Clock.System.now().toLocalDateTime(tz).date }
    val parsed = remember(isoValue) {
        runCatching { Instant.parse(isoValue).toLocalDateTime(tz).date }.getOrNull()
    }
    val displayDate = parsed ?: today

    var dialogOpen by remember { mutableStateOf(false) }

    val javaDate = java.time.LocalDate.of(displayDate.year, displayDate.monthNumber, displayDate.dayOfMonth)
    val pretty = remember(javaDate) {
        DateTimeFormatter
            .ofLocalizedDate(FormatStyle.FULL)
            .withLocale(Locale.getDefault())
            .format(javaDate)
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(12.dp))
            .clickable { dialogOpen = true }
            .padding(horizontal = 14.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Outlined.CalendarMonth,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(12.dp))
        Text(
            text = pretty,
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        Text(
            text = stringResource(R.string.recurring_create_starts_change),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
    }

    if (dialogOpen) {
        val initialMillis = displayDate.atStartOfDayIn(TimeZone.UTC).toEpochMilliseconds()
        val todayUtcMs = today.atStartOfDayIn(TimeZone.UTC).toEpochMilliseconds()
        val selectableDates = NotInPastSelectableDates(todayUtcMs)
        val pickerState = rememberDatePickerState(
            initialSelectedDateMillis = initialMillis,
            selectableDates = selectableDates,
        )
        DatePickerDialog(
            onDismissRequest = { dialogOpen = false },
            confirmButton = {
                TextButton(onClick = {
                    val ms = pickerState.selectedDateMillis
                    if (ms != null) {
                        val utcDate = Instant.fromEpochMilliseconds(ms).toLocalDateTime(TimeZone.UTC).date
                        onChange(utcDate.atStartOfDayIn(tz).toString())
                    }
                    dialogOpen = false
                }) { Text(stringResource(R.string.common_ok)) }
            },
            dismissButton = {
                TextButton(onClick = { dialogOpen = false }) {
                    Text(stringResource(R.string.common_back))
                }
            },
        ) {
            DatePicker(state = pickerState)
        }
    }
}

/**
 * SelectableDates impl that blocks any UTC-day before [todayUtcMs]. Hoisted
 * out of the composable so it sits in plain code — keeps the compose
 * compiler plugin from flagging an inline `object :` literal.
 */
@OptIn(ExperimentalMaterial3Api::class)
private class NotInPastSelectableDates(
    private val todayUtcMs: Long,
) : androidx.compose.material3.SelectableDates {
    override fun isSelectableDate(utcTimeMillis: Long): Boolean = utcTimeMillis >= todayUtcMs
}

/* ── Hilt entry points for non-VM Compose contexts ── */

@dagger.hilt.EntryPoint
@dagger.hilt.InstallIn(dagger.hilt.components.SingletonComponent::class)
interface RecurringSnackbarEntryPoint {
    fun snackbarController(): SnackbarController
}

@dagger.hilt.EntryPoint
@dagger.hilt.InstallIn(dagger.hilt.components.SingletonComponent::class)
interface RecurringAddressEntryPoint {
    fun addressRepository(): cz.cleansia.customer.core.data.AddressRepository
}
