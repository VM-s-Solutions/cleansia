package cz.cleansia.partner.features.orders

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.Cancel
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.HourglassEmpty
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Mail
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.VerifiedUser
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.Lifecycle
import cz.cleansia.core.ui.components.SudsRefreshIndicator
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.navigation.NavRoute

/**
 * App-wide gate for cleaners who haven't finished onboarding or aren't
 * approved yet. Mirrors partner-web's `<cleansia-registration-lock>`
 * modal: hero lock icon, 3 category rows (Profile /
 * Documents / Approval), each row with a "Fix" arrow that routes to the
 * exact section that owns that step.
 *
 * Auto-refreshes on resume (cleaner saves a section → comes back → row
 * flips to Done → progress bar advances). Once all 4 are Done the parent
 * NavHost pops this destination and lands them on Main.
 *
 * Visual language matches [EarningsSummaryScreen]: flat 16dp cards with
 * a 1dp outline-variant border (no elevation), [IconHalo] for category
 * icons (44dp primaryContainer circle, 22dp icon), primary-color
 * labelMedium micro-headers. Pull-to-refresh + inline error banner
 * give the cleaner a way to recover from transient failures without
 * relying on the snackbar (the lock screen has no scaffold/host).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RegistrationLockScreen(
    onFixStep: (NavRoute) -> Unit,
    onCompleted: () -> Unit,
    onSignedOut: () -> Unit,
    viewModel: RegistrationLockViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val navBarBottom = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()

    // Re-check on every return — covers the "save a section and come back"
    // loop without explicit save callbacks. ON_RESUME fires on first
    // composition AND on every back-to-screen. Routes through onResume()
    // (silent-stale path) so a quick round-trip inside the 15s window
    // skips the network entirely. Invariant #3: ON_RESUME MUST NOT call
    // userRefresh() — that would flash the suds rosette on every return.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onResume()
    }

    // Server is the source of truth: once isComplete=true, lift the gate.
    // The parent NavHost watches this callback to navigate to Main.
    LaunchedEffect(uiState.status) {
        if (uiState.status?.isRegistrationComplete() == true) onCompleted()
    }

    val steps = remember(uiState.status) {
        RegistrationLockViewModel.buildSteps(uiState.status)
    }
    val completedSteps = steps.count { it.status == StepStatus.Done }
    val totalSteps = steps.size
    val progress = if (totalSteps == 0) 0f else completedSteps.toFloat() / totalSteps

    // Initial spinner only appears on the very first entry, before any
    // fetch (user or silent-stale) has completed. After that hasLoadedOnce
    // is true forever and the cached categories render even while a
    // background refresh runs invisibly behind them.
    val isInitialLoading = !uiState.hasLoadedOnce && uiState.status == null
    val pullState = rememberPullToRefreshState()

    PullToRefreshBox(
        // Invariant #1: this binds ONLY to isUserRefreshing. Background
        // refreshes (ensureFreshOrCachedAsync from init / onResume) must
        // never flash the suds rosette — they happen silently behind the
        // existing cached data. If you ever feel tempted to OR in
        // isBackgroundRefreshing or a generic isLoading here, stop and
        // re-read the spec — the whole two-flag split exists to keep
        // this exact line honest.
        isRefreshing = uiState.isUserRefreshing,
        onRefresh = { viewModel.userRefresh() },
        state = pullState,
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
        indicator = {
            SudsRefreshIndicator(
                state = pullState,
                isRefreshing = uiState.isUserRefreshing,
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .padding(top = statusBarTop + 8.dp),
            )
        },
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(
                    top = statusBarTop + Spacing.L,
                    start = Spacing.M,
                    end = Spacing.M,
                    bottom = navBarBottom + Spacing.L,
                ),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            LockHeroIcon()
            Spacer(Modifier.height(Spacing.M))

            Text(
                text = stringResource(R.string.registration_lock_title),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onBackground,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(Spacing.XS))
            Text(
                text = stringResource(R.string.registration_lock_subtitle),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )

            // Error banner: surfaces fetch failures inline so the
            // cleaner has a clear "something went wrong → Retry"
            // affordance. Sits above the progress card so it's the
            // first thing the cleaner sees on a failed refresh, but
            // the categories list still renders below at last-known
            // state (or all-Missing on initial failure).
            if (uiState.errorMessage != null) {
                Spacer(Modifier.height(Spacing.M))
                ErrorBanner(
                    message = uiState.errorMessage!!,
                    // Retry is an explicit user action — route through
                    // userRefresh() so the suds rosette spins and the
                    // user sees their tap was registered. The banner
                    // stays visible until the new fetch completes; on
                    // success the VM clears errorMessage and the banner
                    // disappears in the same recomposition.
                    onRetry = { viewModel.userRefresh() },
                )
            }

            Spacer(Modifier.height(Spacing.L))

            if (isInitialLoading) {
                CircularProgressIndicator(modifier = Modifier.padding(Spacing.L))
            } else {
                ProgressCard(
                    completedSteps = completedSteps,
                    totalSteps = totalSteps,
                    progress = progress,
                    steps = steps,
                    onFixStep = onFixStep,
                )
            }

            Spacer(Modifier.height(Spacing.L))

            SignOutLink(onSignOutClick = { viewModel.signOut(onSignedOut) })
        }
    }
}

@Composable
private fun LockHeroIcon() {
    Box(
        modifier = Modifier
            .size(72.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primaryContainer),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = Icons.Outlined.Lock,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(36.dp),
        )
    }
}

/**
 * Inline error surface for failed status fetches. Uses errorContainer
 * tint + 1dp error-toned border, matching the flat card language of
 * the rest of the screen but with the M3 error palette so it reads as
 * an alert distinct from the regular content cards.
 */
@Composable
private fun ErrorBanner(message: String, onRetry: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.errorContainer)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.error.copy(alpha = 0.4f),
                shape = RoundedCornerShape(16.dp),
            )
            .padding(Spacing.M),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = Icons.Outlined.ErrorOutline,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.error,
            modifier = Modifier.size(22.dp),
        )
        Spacer(Modifier.width(Spacing.S))
        Text(
            text = message,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onErrorContainer,
            modifier = Modifier.weight(1f),
        )
        Spacer(Modifier.width(Spacing.S))
        Row(
            modifier = Modifier
                .clip(RoundedCornerShape(50))
                .background(MaterialTheme.colorScheme.error)
                .clickable { onRetry() }
                .padding(horizontal = Spacing.M, vertical = Spacing.XS),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Outlined.Refresh,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onError,
                modifier = Modifier.size(14.dp),
            )
            Spacer(Modifier.width(4.dp))
            Text(
                text = stringResource(R.string.registration_lock_retry),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onError,
            )
        }
    }
}

@Composable
private fun ProgressCard(
    completedSteps: Int,
    totalSteps: Int,
    progress: Float,
    steps: List<StepRow>,
    onFixStep: (NavRoute) -> Unit,
) {
    // Flat card pattern (matches EarningsSummaryScreen): surface
    // background, 1dp outlineVariant border, 16dp corners, no shadow.
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .padding(Spacing.M),
    ) {
        Text(
            text = stringResource(
                R.string.registration_lock_progress,
                completedSteps,
                totalSteps,
            ),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            textAlign = TextAlign.Center,
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(Spacing.XS))
        LinearProgressIndicator(
            progress = { progress },
            modifier = Modifier
                .fillMaxWidth()
                .height(10.dp)
                .clip(RoundedCornerShape(50)),
            color = MaterialTheme.colorScheme.primary,
            trackColor = MaterialTheme.colorScheme.surfaceVariant,
            // Hide M3's stop-indicator dot — it shows even at 0% and
            // looks like a stray pixel against an otherwise empty track.
            drawStopIndicator = {},
        )

        Spacer(Modifier.height(Spacing.M))

        steps.forEachIndexed { index, step ->
            StepRowView(step = step, onFixStep = onFixStep)
            if (index < steps.lastIndex) Spacer(Modifier.height(Spacing.S))
        }
    }
}

/**
 * Brand-tinted icon halo — shared shape recipe with
 * [EarningsSummaryScreen.IconHalo]. Local copy keeps the lock screen
 * self-contained (no cross-feature dependency just for a 44dp circle)
 * while staying visually identical.
 */
@Composable
private fun IconHalo(icon: ImageVector) {
    Box(
        modifier = Modifier
            .size(44.dp)
            .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(22.dp),
        )
    }
}

@Composable
private fun StepRowView(step: StepRow, onFixStep: (NavRoute) -> Unit) {
    val (categoryIcon, categoryLabelRes, ctaLabelRes) = when (step.category) {
        StepCategory.Profile -> Triple(
            Icons.Outlined.Person,
            R.string.registration_lock_category_profile,
            R.string.registration_lock_action_complete_profile,
        )
        StepCategory.Documents -> Triple(
            Icons.Outlined.Description,
            R.string.registration_lock_category_documents,
            R.string.registration_lock_action_upload_documents,
        )
        StepCategory.Approval -> Triple(
            Icons.Outlined.VerifiedUser,
            R.string.registration_lock_category_approval,
            R.string.registration_lock_action_contact_support,
        )
    }
    val (statusIcon, statusTint) = when (step.status) {
        StepStatus.Done ->
            Icons.Outlined.CheckCircle to MaterialTheme.colorScheme.primary
        StepStatus.Pending ->
            Icons.Outlined.HourglassEmpty to MaterialTheme.colorScheme.tertiary
        StepStatus.Missing ->
            Icons.Outlined.Cancel to MaterialTheme.colorScheme.error
    }
    val isActionable = step.status != StepStatus.Done &&
        (step.fixDestination != null ||
            // Rejected approval gets a mailto: support intent.
            step.detailKeys.contains("registration_lock.approval_rejected"))

    val context = LocalContext.current
    val rowClick = if (!isActionable) null else {
        {
            val dest = step.fixDestination
            if (dest != null) {
                onFixStep(dest)
            } else {
                // Approval rejected → open default mail app to support.
                val intent = Intent(Intent.ACTION_SENDTO).apply {
                    data = Uri.parse("mailto:support@cleansia.cz")
                    putExtra(
                        Intent.EXTRA_SUBJECT,
                        context.getString(R.string.registration_lock_support_subject),
                    )
                }
                runCatching { context.startActivity(intent) }
            }
        }
    }

    // Layout contract: every row reserves the same width on the right for
    // a 2-slot cluster (status icon + chevron). When the row isn't
    // actionable the chevron slot is still occupied by an empty Box, so
    // the status X stays in the same column across all rows.
    val statusIconSize = 22.dp
    val chevronSize = 20.dp
    val gapBetween = 6.dp

    // Flat row: surface background, 1dp outlineVariant border, 16dp
    // corners — same shape grammar as the parent ProgressCard so the
    // nested rows read as light "sub-cards" inside the main card.
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .let { if (rowClick != null) it.clickable { rowClick() } else it }
            .padding(horizontal = Spacing.S, vertical = Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // Leading category icon — brand-tinted halo matching the
        // earnings screen's IconHalo recipe (44dp primaryContainer
        // circle with a 22dp tinted glyph).
        IconHalo(icon = categoryIcon)
        Spacer(Modifier.size(Spacing.S))

        // Middle text column — title + CTA/status sub-label + optional
        // detail line (Profile missing fields or rejection note).
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(categoryLabelRes),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            when {
                isActionable -> Text(
                    text = stringResource(ctaLabelRes),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.primary,
                    fontWeight = FontWeight.Medium,
                )
                step.status == StepStatus.Pending -> Text(
                    text = stringResource(R.string.registration_lock_approval_awaiting_review),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.tertiary,
                )
                step.status == StepStatus.Done -> Text(
                    text = stringResource(R.string.registration_lock_step_complete),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.primary,
                )
            }

            if (step.category == StepCategory.Profile && step.detailKeys.isNotEmpty()) {
                Spacer(Modifier.height(Spacing.XS))
                // Show every missing field on its own line — the
                // cleaner has to fix all of them anyway, hiding 7
                // behind a "+7" was less useful than honest.
                step.detailKeys.forEach { rawKey ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "•",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        Spacer(Modifier.size(6.dp))
                        Text(
                            text = resolveDetail(context, rawKey),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }
            if (step.category == StepCategory.Approval &&
                step.detailKeys.contains("registration_lock.approval_rejected")) {
                Spacer(Modifier.height(Spacing.XXS))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Outlined.Mail,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.error,
                        modifier = Modifier.size(14.dp),
                    )
                    Spacer(Modifier.size(4.dp))
                    Text(
                        text = stringResource(R.string.registration_lock_approval_rejected),
                        style = MaterialTheme.typography.labelSmall.copy(fontSize = 12.sp),
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }

        Spacer(Modifier.size(Spacing.S))

        // Trailing fixed-width cluster: status icon + chevron slot.
        // Chevron slot is always rendered (Box of the same size) so
        // status icons line up at the same x across all rows, even
        // when only Approval lacks an actionable chevron.
        Icon(
            imageVector = statusIcon,
            contentDescription = null,
            tint = statusTint,
            modifier = Modifier.size(statusIconSize),
        )
        Spacer(Modifier.size(gapBetween))
        Box(
            modifier = Modifier.size(chevronSize),
            contentAlignment = Alignment.Center,
        ) {
            if (isActionable) {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(chevronSize),
                )
            }
        }
    }
}

@Composable
private fun SignOutLink(onSignOutClick: () -> Unit) {
    Text(
        text = stringResource(R.string.registration_lock_sign_out),
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier
            .clip(RoundedCornerShape(50))
            .clickable { onSignOutClick() }
            .padding(horizontal = Spacing.M, vertical = Spacing.S),
    )
}

private fun resolveDetail(context: android.content.Context, key: String): String {
    val resName = key.replace('.', '_')
    @Suppress("DiscouragedApi")
    val resId = context.resources.getIdentifier(
        resName,
        "string",
        context.packageName,
    )
    return if (resId != 0) context.getString(resId) else key
}
