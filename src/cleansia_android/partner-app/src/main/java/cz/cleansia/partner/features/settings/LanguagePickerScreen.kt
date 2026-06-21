package cz.cleansia.partner.features.settings

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.core.settings.LanguagePreference

/**
 * Dedicated language picker — opened from the Preferences row on the
 * profile landing. List of options with a check mark on the current
 * selection; tapping a row commits the change to [SettingsViewModel]
 * and pops back. Matches the customer profile's per-preference
 * screen pattern.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LanguagePickerScreen(
    onNavigateBack: () -> Unit,
    viewModel: SettingsViewModel = hiltViewModel(),
) {
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val systemLabel = stringResource(R.string.language_system)
    val options = remember(systemLabel) {
        listOf(
            LanguagePreference.System to systemLabel,
            LanguagePreference.English to "English",
            LanguagePreference.Czech to "Čeština",
            LanguagePreference.Slovak to "Slovenčina",
            LanguagePreference.Ukrainian to "Українська",
            LanguagePreference.Russian to "Русский",
        )
    }
    PickerScaffold(
        title = stringResource(R.string.language),
        onNavigateBack = onNavigateBack,
        options = options.map { (lang, label) -> lang.name to label },
        selectedId = settings.language.name,
        onSelected = { id ->
            LanguagePreference.values().firstOrNull { it.name == id }?.let { pref ->
                // Persist the choice (so it survives restarts) AND apply
                // it immediately. setLanguage() alone only wrote to
                // DataStore — nothing fed it back into the resource
                // Configuration, so stringResource() kept resolving the
                // old locale. setApplicationLocales recreates the
                // Activity in the new locale right away.
                viewModel.setLanguage(pref)
                applyAppLocale(pref)
            }
            onNavigateBack()
        },
    )
}

/**
 * Applies the chosen language to the whole app via the AndroidX
 * per-app-language API. An empty locale list = "follow system".
 * Backed by appcompat (works API 24+); MainActivity extends
 * AppCompatActivity so the delegate is available.
 */
private fun applyAppLocale(pref: LanguagePreference) {
    val locales = if (pref.tag == null) {
        LocaleListCompat.getEmptyLocaleList()
    } else {
        LocaleListCompat.forLanguageTags(pref.tag)
    }
    AppCompatDelegate.setApplicationLocales(locales)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ThemePickerScreen(
    onNavigateBack: () -> Unit,
    viewModel: SettingsViewModel = hiltViewModel(),
) {
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val options = listOf(
        cz.cleansia.partner.core.settings.ThemePreference.System to stringResource(R.string.theme_system),
        cz.cleansia.partner.core.settings.ThemePreference.Light to stringResource(R.string.theme_light),
        cz.cleansia.partner.core.settings.ThemePreference.Dark to stringResource(R.string.theme_dark),
    )
    PickerScaffold(
        title = stringResource(R.string.theme),
        onNavigateBack = onNavigateBack,
        options = options.map { (t, label) -> t.name to label },
        selectedId = settings.theme.name,
        onSelected = { id ->
            cz.cleansia.partner.core.settings.ThemePreference.values()
                .firstOrNull { it.name == id }
                ?.let(viewModel::setTheme)
            onNavigateBack()
        },
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun PickerScaffold(
    title: String,
    onNavigateBack: () -> Unit,
    options: List<Pair<String, String>>,
    selectedId: String?,
    onSelected: (String) -> Unit,
) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = title,
                        style = MaterialTheme.typography.titleLarge,
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
        containerColor = MaterialTheme.colorScheme.background,
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = Spacing.M),
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(18.dp))
                    .background(MaterialTheme.colorScheme.surface),
            ) {
                options.forEachIndexed { index, (id, label) ->
                    PickerRow(
                        label = label,
                        isSelected = id == selectedId,
                        onClick = { onSelected(id) },
                    )
                    if (index < options.lastIndex) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(start = Spacing.M)
                                .height(1.dp)
                                .background(
                                    MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f),
                                ),
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun PickerRow(
    label: String,
    isSelected: Boolean,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = Spacing.M, vertical = Spacing.M),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyLarge.copy(
                fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal,
            ),
            color = if (isSelected) MaterialTheme.colorScheme.primary
            else MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        if (isSelected) {
            Box(
                modifier = Modifier
                    .size(24.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = Icons.Outlined.Check,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onPrimary,
                    modifier = Modifier.size(16.dp),
                )
            }
        }
    }
}
