package cz.cleansia.partner.features.onboarding

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
import androidx.compose.foundation.clickable
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowDropDown
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
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
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
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
import cz.cleansia.partner.core.settings.LanguageLabels
import cz.cleansia.partner.core.settings.LanguagePreference
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

    /**
     * Persists the language chosen on the intro screen.
     *
     * Only persists — applying it to the running process is the caller's job
     * (see [AppLocale]), exactly as the profile picker does it. Keeping the
     * `AppCompatDelegate` call out of the ViewModel is what lets this be
     * covered by a plain-JVM unit test.
     *
     * The value has to land in DataStore rather than anywhere else because
     * that is what `AppSettingsRepository.emailLanguageTag()` reads when
     * `RegisterViewModel` stamps the confirmation email's language — routing
     * it through the store is also what runs `SupportedLanguages.resolve`, and
     * a raw device tag would fail the backend's `LanguageValidator` outright.
     */
    fun setLanguage(language: LanguagePreference) {
        viewModelScope.launch { appSettingsRepository.setLanguage(language) }
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
        // Language sits opposite Skip in the intro header rather than in the
        // post-signup RegistrationLock chain: that chain only renders after
        // RegisterEmployee has already stamped PreferredLanguageCode and queued
        // the confirmation email, so a choice made there would arrive too late
        // to affect the one mail that matters — and its "Step X of 4" progress
        // maths counts profile sections, which a display preference is not.
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

/**
 * Compact language dropdown for the pre-auth intro.
 *
 * A dropdown, not a pushed picker route: onboarding is reached before the
 * cleaner has an account and the intro deliberately has no app bar to go back
 * to, so a full screen would need a bespoke return path. The trigger shows the
 * language *the app is in right now* — the native name, or the translated
 * "System" label when following the device — so it is legible to someone who
 * cannot read the language currently on screen.
 */
@Composable
private fun LanguageChooser(
    selected: LanguagePreference,
    onSelect: (LanguagePreference) -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    val systemLabel = stringResource(R.string.language_system)
    val label = LanguageLabels.nativeName(selected) ?: systemLabel
    // The trigger has no visible caption — "Language" is what a screen reader
    // must announce, and that string already ships in all five locales.
    val accessibilityLabel = stringResource(R.string.language)

    Box {
        Row(
            modifier = Modifier
                .clip(CircleShape)
                .clickable { expanded = true }
                .padding(horizontal = Spacing.S, vertical = Spacing.XS)
                .semantics { contentDescription = accessibilityLabel },
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Outlined.Language,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
            Spacer(Modifier.size(Spacing.XS))
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.primary,
            )
            Icon(
                imageVector = Icons.Outlined.ArrowDropDown,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }

        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            LanguageLabels.ordered.forEach { option ->
                DropdownMenuItem(
                    text = {
                        Text(
                            text = LanguageLabels.nativeName(option) ?: systemLabel,
                            style = MaterialTheme.typography.bodyLarge.copy(
                                fontWeight = if (option == selected) FontWeight.SemiBold else FontWeight.Normal,
                            ),
                            color = if (option == selected) {
                                MaterialTheme.colorScheme.primary
                            } else {
                                MaterialTheme.colorScheme.onSurface
                            },
                        )
                    },
                    onClick = {
                        expanded = false
                        onSelect(option)
                    },
                )
            }
        }
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
