package cz.cleansia.customer.features.profile

import android.content.res.Configuration
import android.view.ContextThemeWrapper
import android.view.ViewGroup
import android.widget.DatePicker
import androidx.annotation.StringRes
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.ime
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.ChatBubbleOutline
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.Map
import androidx.compose.material.icons.outlined.PersonOutline
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import cz.cleansia.core.ui.components.CleansiaPhoneInput
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.components.formatPhoneAsTyped
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.LightTextSecondary
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.Locale
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.launch

/** Gathers, once per user, what the registration form skips and a booking needs. */
@Composable
fun ProfileOnboardingScreen(
    user: CurrentUser? = null,
    saving: Boolean = false,
    onSkip: () -> Unit = {},
    onSave: (phone: String, birthDate: String?) -> Unit = { _, _ -> },
) {
    var phone by remember(user?.id) { mutableStateOf(user?.phoneNumber.orEmpty()) }
    var birthDate by remember(user?.id) { mutableStateOf(user?.birthDate.orEmpty()) }
    var showBirthSheet by remember { mutableStateOf(false) }
    val checklist = user?.let { OnboardingChecklist.of(it, phone) }
    val ready = checklist?.isReady == true
    val keyboardUp = WindowInsets.ime.getBottom(LocalDensity.current) > 0
    val scroll = rememberScrollState()
    // The phone is the screen's one text field, so while the keyboard rises the card's end is kept above the footer.
    LaunchedEffect(keyboardUp) {
        if (keyboardUp) snapshotFlow { scroll.maxValue }.filter { it != Int.MAX_VALUE }.collect { scroll.scrollTo(it) }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .statusBarsPadding()
            .imePadding(),
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .weight(1f)
                .verticalScroll(scroll)
                .padding(horizontal = Spacing.ML),
        ) {
            Spacer(Modifier.height(Spacing.XXS))
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                val firstName = user?.firstName.orEmpty()
                Text(
                    text = if (firstName.isNotBlank()) {
                        stringResource(R.string.onboarding_greeting_named, firstName)
                    } else {
                        stringResource(R.string.onboarding_greeting)
                    },
                    style = MaterialTheme.typography.headlineLarge,
                    color = MaterialTheme.colorScheme.onBackground,
                    modifier = Modifier.weight(1f),
                )
                Image(
                    painter = painterResource(if (ready) R.drawable.mascot_thumbs_up else R.drawable.mascot_waving),
                    contentDescription = null,
                    modifier = Modifier.size(76.dp),
                )
            }
            if (user != null && checklist != null) {
                Spacer(Modifier.height(Spacing.S))
                Surface(
                    shape = MaterialTheme.shapes.large,
                    color = MaterialTheme.colorScheme.surface,
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
                ) {
                    Column {
                        Column(
                            Modifier
                                .fillMaxWidth()
                                .padding(horizontal = Spacing.M, vertical = Spacing.S)
                                .semantics(mergeDescendants = true) {},
                        ) {
                            Text(
                                text = if (ready) {
                                    stringResource(R.string.onboarding_ready)
                                } else {
                                    stringResource(R.string.onboarding_progress, checklist.doneCount, 3)
                                },
                                style = MaterialTheme.typography.headlineSmall,
                                color = if (ready) primaryText() else MaterialTheme.colorScheme.onSurface,
                            )
                            Spacer(Modifier.height(Spacing.Hair))
                            // shortcut: the prompt names only the phone, as every account that signs in here has a name;
                            // add the name prompts with a name step.
                            Text(
                                text = stringResource(if (ready) R.string.onboarding_ready_sub else R.string.onboarding_left_phone),
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        HorizontalDivider(thickness = 1.dp, color = MaterialTheme.colorScheme.outlineVariant)
                        StepRow(
                            label = stringResource(R.string.onboarding_row_name),
                            value = user.fullName,
                            mark = if (checklist.name) Mark.Done else Mark.Waiting,
                        ) { RowValue(user.fullName) }
                        RowRule()
                        val hidden = isAppleRelayEmail(user.email)
                        val email = if (hidden) stringResource(R.string.onboarding_email_hidden) else user.email
                        StepRow(
                            label = stringResource(R.string.profile_edit_email),
                            value = email,
                            mark = if (checklist.email) Mark.Done else Mark.Waiting,
                        ) {
                            if (hidden) RowValue(email, MaterialTheme.colorScheme.onSurfaceVariant) else EmailValue(email)
                        }
                        RowRule()
                        Column(Modifier.padding(horizontal = Spacing.M, vertical = Spacing.S)) {
                            val phoneLabel = stringResource(R.string.onboarding_phone_label)
                            OpenRowHead(label = phoneLabel, mark = if (checklist.phone) Mark.Done else Mark.Current)
                            Spacer(Modifier.height(Spacing.XS))
                            // shortcut: Czech numbers only, the one market served today; pass the market's country when a
                            // second one opens.
                            CleansiaPhoneInput(value = phone, onValueChange = { phone = it }, label = phoneLabel, countryRegion = "CZ")
                            Spacer(Modifier.height(Spacing.XS))
                            CleanerExample(name = user.fullName, phone = phone, reachable = checklist.phone)
                        }
                        RowRule()
                        val birthShown = birthDate.takeIf { it.isNotBlank() }?.let {
                            LocalDate.parse(it).format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(Locale.getDefault()))
                        }
                        StepRow(
                            label = stringResource(R.string.onboarding_birthdate_label),
                            value = birthShown,
                            mark = if (birthShown != null) Mark.Done else Mark.Waiting,
                            note = stringResource(R.string.onboarding_birthdate_helper),
                            onClick = { showBirthSheet = true },
                        ) {
                            if (birthShown != null) {
                                RowValue(birthShown)
                            } else {
                                RowValue(stringResource(R.string.onboarding_not_set), MaterialTheme.colorScheme.onSurfaceVariant, Modifier.clearAndSetSemantics {})
                            }
                        }
                    }
                }
            }
            Spacer(Modifier.height(Spacing.XS))
        }

        if (keyboardUp) HorizontalDivider(thickness = 1.dp, color = MaterialTheme.colorScheme.outline)
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.background)
                .navigationBarsPadding()
                .padding(horizontal = Spacing.ML, vertical = Spacing.S),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            CleansiaPrimaryButton(
                text = stringResource(R.string.onboarding_save),
                onClick = { onSave(phone.trim(), birthDate.trim().takeIf { it.isNotBlank() }) },
                enabled = ready && !saving,
                loading = saving,
            )
            if (!keyboardUp) {
                Spacer(Modifier.height(Spacing.XXS))
                CleansiaTextButton(
                    onClick = onSkip,
                    enabled = !saving,
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 44.dp),
                ) {
                    Text(
                        text = stringResource(R.string.onboarding_skip),
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }

    if (showBirthSheet) BirthDateWheelSheet(birthDate, onBirthDate = { birthDate = it }, onDismiss = { showBirthSheet = false })
}

private enum class Mark(@StringRes val state: Int) {
    Done(R.string.onboarding_row_done_a11y),
    Current(R.string.onboarding_row_current_a11y),
    Waiting(R.string.onboarding_row_waiting_a11y),
}

/** The mock's meta and ring grey: slate-500 in a light scheme, where no role holds it, and onSurfaceVariant in a dark one. */
private val ColorScheme.quiet: Color
    get() = if (surface.luminance() < 0.5f) onSurfaceVariant else LightTextSecondary

@Composable
private fun MarkDot(mark: Mark) {
    val scheme = MaterialTheme.colorScheme
    val disc = Modifier.size(24.dp)
    when (mark) {
        Mark.Done -> Box(disc.background(scheme.primary, CircleShape), contentAlignment = Alignment.Center) {
            Icon(Icons.Rounded.Check, contentDescription = null, modifier = Modifier.size(16.dp), tint = scheme.onPrimary)
        }
        Mark.Current -> Box(disc.border(2.5.dp, scheme.primary, CircleShape), contentAlignment = Alignment.Center) {
            Box(
                Modifier
                    .size(8.dp)
                    .background(scheme.primary, CircleShape),
            )
        }
        Mark.Waiting -> Box(disc.border(2.dp, scheme.quiet, CircleShape))
    }
}

/** A done or waiting row, read by TalkBack as one description that ends with its state; with [onClick], a button. */
@Composable
private fun StepRow(
    label: String,
    value: String?,
    mark: Mark,
    note: String? = null,
    onClick: (() -> Unit)? = null,
    shownValue: @Composable RowScope.() -> Unit,
) {
    val state = stringResource(mark.state)
    val quiet = MaterialTheme.colorScheme.quiet
    Column(
        modifier = Modifier
            .then(if (onClick != null) Modifier.clickable(role = Role.Button, onClick = onClick) else Modifier)
            .clearAndSetSemantics { contentDescription = listOfNotNull(label, value, note, state).joinToString(", ") }
            .fillMaxWidth()
            .heightIn(min = 44.dp)
            .padding(horizontal = Spacing.M, vertical = Spacing.XS),
        verticalArrangement = Arrangement.Center,
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            MarkDot(mark)
            Spacer(Modifier.width(Spacing.S))
            Text(label, style = MaterialTheme.typography.labelLarge, color = quiet)
            Spacer(Modifier.width(Spacing.S))
            shownValue()
            if (onClick != null) {
                Spacer(Modifier.width(Spacing.XS))
                Icon(
                    Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                    tint = quiet,
                )
            }
        }
        note?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodySmall,
                color = quiet,
                modifier = Modifier.padding(start = 36.dp, top = Spacing.Hair),
            )
        }
    }
}

/** The open row's head; its field and the example below it stay nodes of their own. */
@Composable
private fun OpenRowHead(label: String, mark: Mark) {
    val state = stringResource(mark.state)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clearAndSetSemantics { contentDescription = "$label, $state" },
        verticalAlignment = Alignment.CenterVertically,
    ) {
        MarkDot(mark)
        Spacer(Modifier.width(Spacing.S))
        Text(label, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurface)
    }
}

@Composable
private fun RowRule() {
    HorizontalDivider(Modifier.padding(start = 52.dp), thickness = 1.dp, color = MaterialTheme.colorScheme.outlineVariant)
}

@Composable
private fun RowScope.RowValue(text: String, color: Color = MaterialTheme.colorScheme.onSurface, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyLarge,
        color = color,
        maxLines = 1,
        overflow = TextOverflow.Ellipsis,
        textAlign = TextAlign.End,
        modifier = modifier.weight(1f),
    )
}

/** A long address gives way in its local part, so the domain that names the account stays whole. */
@Composable
private fun RowScope.EmailValue(email: String) {
    val (local, domain) = emailParts(email)
    val style = MaterialTheme.typography.bodyLarge
    val color = MaterialTheme.colorScheme.onSurface
    Row(Modifier.weight(1f), horizontalArrangement = Arrangement.End) {
        Text(local, style = style, color = color, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f, fill = false))
        Text(domain, style = style, color = color, maxLines = 1, softWrap = false)
    }
}

/** The cleaner's card for this customer as it would read now: Call and SMS light up only for a reachable phone. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun CleanerExample(name: String, phone: String, reachable: Boolean) {
    val scheme = MaterialTheme.colorScheme
    val type = MaterialTheme.typography
    Column(
        Modifier
            .fillMaxWidth()
            .background(scheme.surfaceVariant, MaterialTheme.shapes.small)
            .padding(horizontal = Spacing.S, vertical = Spacing.XS)
            .semantics(mergeDescendants = true) {},
    ) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
            Text(stringResource(R.string.onboarding_card_title), style = type.labelLarge, color = scheme.onSurfaceVariant)
            Text(
                text = stringResource(R.string.onboarding_card_example),
                style = type.labelSmall.copy(letterSpacing = 0.sp),
                color = scheme.onSurfaceVariant,
                modifier = Modifier
                    .background(scheme.surface, CircleShape)
                    .heightIn(min = 20.dp)
                    .padding(horizontal = Spacing.XS),
            )
        }
        Spacer(Modifier.height(Spacing.XXS))
        Row(horizontalArrangement = Arrangement.spacedBy(Spacing.XS), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Outlined.PersonOutline, contentDescription = null, modifier = Modifier.size(16.dp), tint = scheme.onSurfaceVariant)
            if (name.isNotBlank()) Text(name, style = type.titleMedium, color = scheme.onSurface) else NotSetDash(type.titleMedium)
        }
        Spacer(Modifier.height(Spacing.XXS))
        Row(horizontalArrangement = Arrangement.spacedBy(Spacing.XS), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Outlined.LocationOn, contentDescription = null, modifier = Modifier.size(16.dp), tint = scheme.onSurfaceVariant)
            Text(stringResource(R.string.onboarding_card_sample_address), style = type.bodyMedium, color = scheme.onSurfaceVariant)
        }
        Spacer(Modifier.height(Spacing.XS))
        FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            ExampleChip(Icons.Outlined.Phone, stringResource(R.string.onboarding_card_call), lit = reachable)
            ExampleChip(Icons.Outlined.ChatBubbleOutline, stringResource(R.string.onboarding_card_sms), lit = reachable)
            ExampleChip(Icons.Outlined.Map, stringResource(R.string.onboarding_card_navigate), lit = true)
        }
        Spacer(Modifier.height(Spacing.XS))
        HorizontalDivider(thickness = 1.dp, color = scheme.outline)
        Spacer(Modifier.height(Spacing.XS))
        Row(horizontalArrangement = Arrangement.spacedBy(Spacing.XS)) {
            Text(
                text = stringResource(R.string.onboarding_card_dial),
                style = type.labelMedium,
                color = scheme.onSurfaceVariant,
                modifier = Modifier
                    .weight(1f)
                    .alignByBaseline(),
            )
            val dialStyle = type.titleMedium.copy(fontFeatureSettings = "tnum")
            if (phone.isEmpty()) {
                NotSetDash(dialStyle, Modifier.alignByBaseline())
            } else {
                Text(
                    text = formatPhoneAsTyped(phone),
                    style = dialStyle,
                    color = scheme.onSurface,
                    maxLines = 1,
                    softWrap = false,
                    modifier = Modifier.alignByBaseline(),
                )
            }
        }
    }
}

/** A picture of the cleaner's button, not a button: no tap, and a dashed one is silent. */
@Composable
private fun ExampleChip(icon: ImageVector, text: String, lit: Boolean) {
    val scheme = MaterialTheme.colorScheme
    val ring = scheme.quiet
    val ink = if (lit) primaryText() else scheme.onSurfaceVariant
    Row(
        modifier = Modifier
            .heightIn(min = 34.dp)
            .then(
                if (lit) {
                    Modifier.background(scheme.primary.copy(alpha = 0.12f), CircleShape)
                } else {
                    Modifier
                        .drawBehind {
                            drawRoundRect(
                                color = ring,
                                cornerRadius = CornerRadius(size.height / 2),
                                style = Stroke(1.dp.toPx(), pathEffect = PathEffect.dashPathEffect(floatArrayOf(4.dp.toPx(), 3.dp.toPx()))),
                            )
                        }
                        .clearAndSetSemantics {}
                },
            )
            .padding(horizontal = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, contentDescription = null, modifier = Modifier.size(16.dp), tint = ink)
        Spacer(Modifier.width(6.dp))
        Text(text, style = MaterialTheme.typography.labelLarge, color = ink)
    }
}

@Composable
private fun NotSetDash(style: TextStyle, modifier: Modifier = Modifier) {
    val notSet = stringResource(R.string.onboarding_not_set)
    Text(
        text = "\u2014",
        style = style,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier.semantics { contentDescription = notSet },
    )
}

/** Thirty years back for an empty field, else the stored day. */
internal fun birthDateWheelSeed(stored: String, today: LocalDate): LocalDate =
    stored.takeIf { it.isNotBlank() }?.let { runCatching { LocalDate.parse(it) }.getOrNull() } ?: today.minusYears(30)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun BirthDateWheelSheet(
    birthDate: String,
    onBirthDate: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val scope = rememberCoroutineScope()
    val latest by rememberUpdatedState(onBirthDate)
    val dark = MaterialTheme.colorScheme.surface.luminance() < 0.5f
    val seed = remember { birthDateWheelSeed(birthDate, LocalDate.now()) }
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .fillMaxHeight(0.5f)
                .padding(horizontal = Spacing.ML)
                .padding(bottom = Spacing.XS),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = stringResource(R.string.onboarding_birthdate_label),
                    style = MaterialTheme.typography.headlineSmall,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                CleansiaTextButton(onClick = { scope.launch { sheetState.hide() }.invokeOnCompletion { onDismiss() } }) {
                    Text(stringResource(R.string.onboarding_birthdate_done), style = MaterialTheme.typography.titleLarge)
                }
            }
            Text(
                text = stringResource(R.string.onboarding_birthdate_helper),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(Spacing.S))
            AndroidView(
                factory = { context ->
                    // A View follows the system's night mode; the app may have picked the other one.
                    val themed = ContextThemeWrapper(context, R.style.Theme_Cleansia).apply {
                        applyOverrideConfiguration(
                            Configuration().apply {
                                uiMode = if (dark) Configuration.UI_MODE_NIGHT_YES else Configuration.UI_MODE_NIGHT_NO
                            },
                        )
                    }
                    DatePicker(themed, null, 0, R.style.Widget_Cleansia_BirthDateWheels).apply {
                        // A tap on a wheel's middle value would otherwise open a keyboard to type it.
                        descendantFocusability = ViewGroup.FOCUS_BLOCK_DESCENDANTS
                        maxDate = System.currentTimeMillis()
                        init(seed.year, seed.monthValue - 1, seed.dayOfMonth) { _, y, m, d -> latest(LocalDate.of(y, m + 1, d).toString()) }
                    }
                },
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

private fun previewUser(phone: String? = null, birthDate: String? = null) = CurrentUser(
    id = "preview",
    email = "jana.dvorakova@gmail.com",
    firstName = "Jana",
    lastName = "Dvořáková",
    phoneNumber = phone,
    birthDate = birthDate,
    preferredLanguageCode = "cs",
)

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun ProfileOnboardingEmptyPreview() {
    CleansiaTheme { ProfileOnboardingScreen(user = previewUser()) }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun ProfileOnboardingFilledPreview() {
    CleansiaTheme { ProfileOnboardingScreen(user = previewUser(phone = "+420777123456", birthDate = "1991-03-14")) }
}
