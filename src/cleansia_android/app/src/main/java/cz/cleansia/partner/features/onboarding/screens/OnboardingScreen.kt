package cz.cleansia.partner.features.onboarding.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.slideInVertically
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.PagerState
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.TrendingUp
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.AttachMoney
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CleaningServices
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Handshake
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.Star
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.features.onboarding.components.FeatureHighlight
import cz.cleansia.partner.features.onboarding.components.OnboardingIllustration
import cz.cleansia.partner.features.onboarding.components.PageIndicator
import cz.cleansia.partner.features.onboarding.viewmodels.OnboardingViewModel
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.DynamicCleaningBackground
import cz.cleansia.partner.ui.theme.CleansiaColors
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

data class OnboardingPage(
    val titleRes: Int,
    val descriptionRes: Int,
    val icon: ImageVector,
    val backgroundColor: Color,
    val highlights: List<OnboardingHighlight> = emptyList()
)

data class OnboardingHighlight(
    val icon: ImageVector,
    val textRes: Int
)

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun OnboardingScreen(
    onComplete: () -> Unit,
    onSkip: () -> Unit,
    viewModel: OnboardingViewModel = hiltViewModel()
) {
    val handleComplete: () -> Unit = {
        viewModel.completeOnboarding()
        onComplete()
    }

    val handleSkip: () -> Unit = {
        viewModel.completeOnboarding()
        onSkip()
    }

    val pages = listOf(
        OnboardingPage(
            titleRes = R.string.onboarding_welcome_title,
            descriptionRes = R.string.onboarding_welcome_desc,
            icon = Icons.Default.Handshake,
            backgroundColor = CleansiaColors.Primary,
            highlights = listOf(
                OnboardingHighlight(Icons.Default.CleaningServices, R.string.onboarding_highlight_orders),
                OnboardingHighlight(Icons.Default.AttachMoney, R.string.onboarding_highlight_earnings),
                OnboardingHighlight(Icons.Default.Schedule, R.string.onboarding_highlight_schedule)
            )
        ),
        OnboardingPage(
            titleRes = R.string.onboarding_orders_title,
            descriptionRes = R.string.onboarding_orders_desc,
            icon = Icons.Default.CleaningServices,
            backgroundColor = CleansiaColors.Secondary,
            highlights = listOf(
                OnboardingHighlight(Icons.Default.Notifications, R.string.onboarding_highlight_realtime),
                OnboardingHighlight(Icons.Default.LocationOn, R.string.onboarding_highlight_location),
                OnboardingHighlight(Icons.Default.CheckCircle, R.string.onboarding_highlight_tracking)
            )
        ),
        OnboardingPage(
            titleRes = R.string.onboarding_earnings_title,
            descriptionRes = R.string.onboarding_earnings_desc,
            icon = Icons.Default.Assessment,
            backgroundColor = CleansiaColors.success,
            highlights = listOf(
                OnboardingHighlight(Icons.Default.Receipt, R.string.onboarding_highlight_invoices),
                OnboardingHighlight(Icons.AutoMirrored.Filled.TrendingUp, R.string.onboarding_highlight_analytics),
                OnboardingHighlight(Icons.Default.Description, R.string.onboarding_highlight_reports)
            )
        ),
        OnboardingPage(
            titleRes = R.string.onboarding_profile_title,
            descriptionRes = R.string.onboarding_profile_desc,
            icon = Icons.Default.Person,
            backgroundColor = CleansiaColors.info,
            highlights = listOf(
                OnboardingHighlight(Icons.Default.CalendarMonth, R.string.onboarding_highlight_availability),
                OnboardingHighlight(Icons.Default.Security, R.string.onboarding_highlight_documents),
                OnboardingHighlight(Icons.Default.Star, R.string.onboarding_highlight_ratings)
            )
        )
    )

    val pagerState = rememberPagerState(pageCount = { pages.size })
    val coroutineScope = rememberCoroutineScope()
    val isLastPage by remember { derivedStateOf { pagerState.currentPage == pages.lastIndex } }
    val isDark = isSystemInDarkTheme()

    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background
    ) {
        Box(modifier = Modifier.fillMaxSize()) {
            // Floating cleaning icons background
            DynamicCleaningBackground(
                iconColor = if (isDark) Color(0xFF38BDF8) else Color(0xFF0EA5E9),
                iconAlpha = if (isDark) 0.04f else 0.03f
            )

            Column(
                modifier = Modifier.fillMaxSize()
            ) {
                // Skip button
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .statusBarsPadding()
                        .padding(horizontal = 16.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.End
                ) {
                    TextButton(onClick = handleSkip) {
                        Text(
                            text = stringResource(R.string.onboarding_skip),
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                // Pager
                HorizontalPager(
                    state = pagerState,
                    modifier = Modifier.weight(1f)
                ) { page ->
                    OnboardingPageContent(
                        page = pages[page],
                        pageIndex = page,
                        pagerState = pagerState
                    )
                }

                // Bottom section
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    // Page indicators
                    Row(
                        modifier = Modifier.padding(bottom = 32.dp),
                        horizontalArrangement = Arrangement.Center
                    ) {
                        pages.forEachIndexed { index, _ ->
                            PageIndicator(
                                isSelected = index == pagerState.currentPage,
                                color = pages[pagerState.currentPage].backgroundColor
                            )
                            if (index < pages.lastIndex) {
                                Spacer(modifier = Modifier.width(8.dp))
                            }
                        }
                    }

                    // Action button
                    CleansiaButton(
                        text = if (isLastPage) {
                            stringResource(R.string.onboarding_get_started)
                        } else {
                            stringResource(R.string.onboarding_next)
                        },
                        onClick = {
                            if (isLastPage) {
                                handleComplete()
                            } else {
                                coroutineScope.launch {
                                    pagerState.animateScrollToPage(pagerState.currentPage + 1)
                                }
                            }
                        },
                        modifier = Modifier.fillMaxWidth()
                    )

                    Spacer(modifier = Modifier.height(32.dp))
                }
            }
        }
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun OnboardingPageContent(
    page: OnboardingPage,
    pageIndex: Int,
    pagerState: PagerState
) {
    // Animate content entrance when page becomes current
    var isVisible by remember { mutableStateOf(false) }
    LaunchedEffect(pagerState.currentPage) {
        if (pagerState.currentPage == pageIndex) {
            isVisible = false
            delay(100)
            isVisible = true
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        // Visual illustration card
        AnimatedVisibility(
            visible = isVisible || pagerState.currentPage == pageIndex,
            enter = fadeIn(tween(400)) + slideInVertically(tween(500)) { -40 }
        ) {
            OnboardingIllustration(page = page, pageIndex = pageIndex)
        }

        Spacer(modifier = Modifier.height(32.dp))

        // Title
        AnimatedVisibility(
            visible = isVisible || pagerState.currentPage == pageIndex,
            enter = fadeIn(tween(400, delayMillis = 150)) + slideInVertically(tween(400, delayMillis = 150)) { 30 }
        ) {
            Text(
                text = stringResource(page.titleRes),
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center,
                color = MaterialTheme.colorScheme.onBackground
            )
        }

        Spacer(modifier = Modifier.height(12.dp))

        // Description
        AnimatedVisibility(
            visible = isVisible || pagerState.currentPage == pageIndex,
            enter = fadeIn(tween(400, delayMillis = 250)) + slideInVertically(tween(400, delayMillis = 250)) { 30 }
        ) {
            Text(
                text = stringResource(page.descriptionRes),
                style = MaterialTheme.typography.bodyLarge,
                textAlign = TextAlign.Center,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = 8.dp)
            )
        }

        Spacer(modifier = Modifier.height(20.dp))

        // Feature highlights
        AnimatedVisibility(
            visible = isVisible || pagerState.currentPage == pageIndex,
            enter = fadeIn(tween(400, delayMillis = 350)) + slideInVertically(tween(400, delayMillis = 350)) { 30 }
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                page.highlights.forEach { highlight ->
                    FeatureHighlight(
                        icon = highlight.icon,
                        text = stringResource(highlight.textRes),
                        color = page.backgroundColor
                    )
                }
            }
        }
    }
}
