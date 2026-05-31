package cz.cleansia.partner.features.onboarding.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
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
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.snapshotFlow
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.core.settings.AppSettingsRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.launch

/**
 * Simplified 2-page onboarding (welcome → CTA) per the rebuild plan
 * decision. The original 5-page intro (welcome/features/availability/
 * documents/terms) was found to be over-engineered — cleaners learn the
 * app by using it, not by tapping through slides.
 *
 * "Skip" jumps straight to login; primary CTA does the same after page 2.
 */
@HiltViewModel
class OnboardingViewModel @Inject constructor(
    private val appSettingsRepository: AppSettingsRepository,
) : ViewModel() {
    fun markSeen() {
        viewModelScope.launch { appSettingsRepository.markOnboardingSeen() }
    }
}

@Composable
fun OnboardingScreen(
    onFinished: () -> Unit,
    viewModel: OnboardingViewModel = hiltViewModel(),
) {
    val pageCount = 2
    val pagerState = rememberPagerState(pageCount = { pageCount })
    val scope = rememberCoroutineScope()
    var currentPage by remember { mutableStateOf(0) }

    val finishOnce: () -> Unit = {
        viewModel.markSeen()
        onFinished()
    }

    LaunchedEffect(pagerState) {
        snapshotFlow { pagerState.currentPage }.collect { currentPage = it }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .padding(Spacing.M),
            contentAlignment = Alignment.CenterEnd,
        ) {
            CleansiaTextLink(
                text = stringResource(R.string.onboarding_skip),
                onClick = finishOnce,
            )
        }

        HorizontalPager(
            state = pagerState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
        ) { page ->
            when (page) {
                0 -> OnboardingPage(
                    titleRes = R.string.onboarding_welcome_title,
                    bodyRes = R.string.onboarding_welcome_body,
                )
                1 -> OnboardingPage(
                    titleRes = R.string.onboarding_ready_title,
                    bodyRes = R.string.onboarding_ready_body,
                )
            }
        }

        PageIndicator(currentPage = currentPage, pageCount = pageCount)

        Spacer(Modifier.height(Spacing.S))

        CleansiaPrimaryButton(
            modifier = Modifier.padding(horizontal = Spacing.M),
            text = if (currentPage == pageCount - 1)
                stringResource(R.string.onboarding_get_started)
            else
                stringResource(R.string.onboarding_next),
            onClick = {
                if (currentPage == pageCount - 1) finishOnce()
                else scope.launch { pagerState.animateScrollToPage(currentPage + 1) }
            },
        )
        Spacer(Modifier.height(Spacing.L))
    }
}

@Composable
private fun OnboardingPage(titleRes: Int, bodyRes: Int) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = Spacing.L),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Image(
            painter = painterResource(R.drawable.mascot_waving),
            contentDescription = null,
            modifier = Modifier.size(180.dp),
        )
        Spacer(Modifier.height(Spacing.L))
        Text(
            text = stringResource(titleRes),
            style = MaterialTheme.typography.displaySmall,
            color = MaterialTheme.colorScheme.onBackground,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(Spacing.M))
        Text(
            text = stringResource(bodyRes),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun PageIndicator(currentPage: Int, pageCount: Int) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = Spacing.M),
        horizontalArrangement = Arrangement.Center,
    ) {
        repeat(pageCount) { index ->
            val color = if (index == currentPage)
                MaterialTheme.colorScheme.primary
            else
                MaterialTheme.colorScheme.outlineVariant
            Box(
                modifier = Modifier
                    .padding(horizontal = 4.dp)
                    .size(8.dp)
                    .clip(CircleShape)
                    .background(color),
            )
        }
    }
}
