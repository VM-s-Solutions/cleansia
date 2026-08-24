package cz.cleansia.partner.features.onboarding

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.systemBars
import androidx.compose.foundation.layout.windowInsetsPadding
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
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.settings.AppLocale
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.LocalAppSettings
import cz.cleansia.partner.R
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.core.settings.LanguagePreferenceSync
import cz.cleansia.partner.features.settings.LanguageChooser
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
    private val languageSync: LanguagePreferenceSync,
) : ViewModel() {
    fun markSeen() {
        viewModelScope.launch { appSettingsRepository.markOnboardingSeen() }
    }

    /**
     * Persists the language chosen on the intro screen.
     *
     * **Applying it to the running process is the CALLER's job** — keeping that call out of the ViewModel
     * is what lets this be covered by a plain-JVM unit test. It must land in DataStore, because that is
     * what the confirmation-mail language tag reads.
     */
    fun setLanguage(language: LanguagePreference) {
        viewModelScope.launch {
            appSettingsRepository.setLanguage(language)
            languageSync.send(appSettingsRepository.emailLanguageTag())
        }
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
            .background(MaterialTheme.colorScheme.background)
            .windowInsetsPadding(WindowInsets.systemBars),
    ) {
        // The intro is where the language choice still reaches the confirmation
        // email: RegisterEmployee stamps PreferredLanguageCode from whatever is
        // stored by then. The RegistrationLock chain carries the same chooser,
        // but only as a display preference — by the time it renders the mail
        // has been queued.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(Spacing.M),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            LanguageChooser(
                selected = LocalAppSettings.current.language,
                onSelect = { language ->
                    // Persist first, apply second: apply() recreates the
                    // Activity on API < 33 and would otherwise race the write.
                    viewModel.setLanguage(language)
                    AppLocale.apply(language.tag)
                },
            )
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
                // Two slides, two mascots. Both pages drew mascot_waving, so the pager looked
                // stuck — the only thing that changed on swipe was the text, and the largest
                // element on screen did not move. The greeting keeps the wave; the second slide
                // is about being ready to work, so it gets the mascot that reads that way.
                0 -> OnboardingPage(
                    mascotRes = R.drawable.mascot_waving,
                    titleRes = R.string.onboarding_welcome_title,
                    bodyRes = R.string.onboarding_welcome_body,
                )
                1 -> OnboardingPage(
                    mascotRes = R.drawable.mascot_ready,
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
private fun OnboardingPage(mascotRes: Int, titleRes: Int, bodyRes: Int) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = Spacing.L),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Image(
            painter = painterResource(mascotRes),
            contentDescription = null,
            modifier = Modifier.size(180.dp),
        )
        Spacer(Modifier.height(Spacing.L))
        Text(
            text = stringResource(titleRes),
            // displayMedium, not displaySmall: this hero title was reaching for M3's un-rescaled
            // 36sp baseline, a size our Poppins ramp never offered (it tops out at 32sp).
            // displayMedium (28sp Poppins Bold) is the nearest real hero slot; the fontWeight
            // param below still wins, so it renders 28sp Poppins SemiBold.
            style = MaterialTheme.typography.displayMedium,
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
