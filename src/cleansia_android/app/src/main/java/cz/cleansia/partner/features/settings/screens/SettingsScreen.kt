package cz.cleansia.partner.features.settings.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.core.security.BiometricAvailability
import cz.cleansia.partner.core.security.BiometricHelper
import cz.cleansia.partner.features.profile.components.SettingsSection
import cz.cleansia.partner.features.profile.viewmodels.ProfileViewModel
import cz.cleansia.partner.ui.components.GlassBackButton

@Composable
fun SettingsScreen(
    onNavigateBack: () -> Unit,
    viewModel: ProfileViewModel = hiltViewModel()
) {
    val currentLanguage by viewModel.currentLanguage.collectAsState()
    val currentTheme by viewModel.currentTheme.collectAsState()
    val notificationsEnabled by viewModel.notificationsEnabled.collectAsState()
    val biometricEnabled by viewModel.biometricEnabled.collectAsState()

    val context = LocalContext.current
    val biometricHelper = remember { BiometricHelper(context) }
    val biometricAvailable = remember {
        biometricHelper.checkBiometricAvailability() == BiometricAvailability.AVAILABLE
    }

    Box(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .statusBarsPadding()
                .padding(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp)
        ) {
            SettingsSection(
                currentLanguage = currentLanguage,
                currentTheme = currentTheme,
                notificationsEnabled = notificationsEnabled,
                biometricEnabled = biometricEnabled,
                biometricAvailable = biometricAvailable,
                appVersion = "1.0.0",
                onLanguageChange = { viewModel.setLanguage(it) },
                onThemeChange = { viewModel.setTheme(it) },
                onNotificationsToggle = { viewModel.setNotificationsEnabled(it) },
                onBiometricToggle = { viewModel.setBiometricEnabled(it) }
            )
        }

        GlassBackButton(
            onNavigateBack = onNavigateBack,
            title = stringResource(R.string.settings),
            modifier = Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.background)
        )
    }
}
