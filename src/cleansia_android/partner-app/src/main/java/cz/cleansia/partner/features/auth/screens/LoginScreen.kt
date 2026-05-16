package cz.cleansia.partner.features.auth.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Fingerprint
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.fragment.app.FragmentActivity
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.core.ui.components.CleansiaCheckbox
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.partner.R
import cz.cleansia.partner.core.security.BiometricAvailability
import cz.cleansia.partner.core.security.BiometricHelper
import cz.cleansia.partner.core.security.BiometricResult
import cz.cleansia.partner.features.auth.viewmodels.LoginViewModel

/**
 * Partner sign-in screen — visual parity with customer-app's SignInScreen
 * (mascot → title/subtitle → email + password → remember-me + forgot-password
 * row → primary button → footer link). Adds a biometric outlined button below
 * the primary button when the device supports it and the user previously
 * opted in — partner-specific feature customer doesn't have. NO Google OAuth
 * (partner doesn't currently issue Google client IDs).
 */
@Composable
fun LoginScreen(
    onNavigateToRegister: () -> Unit,
    onNavigateToForgotPassword: () -> Unit,
    onNavigateToConfirmEmail: (String) -> Unit,
    onLoginSuccess: () -> Unit,
    viewModel: LoginViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Biometric setup — partner-specific. Customer-app has no biometric path.
    val context = LocalContext.current
    val biometricHelper = remember { BiometricHelper(context) }
    val biometricAvailable = remember {
        biometricHelper.checkBiometricAvailability() == BiometricAvailability.AVAILABLE
    }
    val canShowBiometric = uiState.biometricEnabled && biometricAvailable

    LaunchedEffect(uiState.showBiometricPrompt) {
        if (uiState.showBiometricPrompt) {
            val activity = context as? FragmentActivity
            if (activity != null) {
                biometricHelper.showBiometricPrompt(
                    activity = activity,
                    title = context.getString(R.string.biometric_prompt_title),
                    subtitle = context.getString(R.string.biometric_prompt_subtitle),
                    negativeButtonText = context.getString(R.string.biometric_prompt_negative),
                ) { result ->
                    when (result) {
                        is BiometricResult.Success -> viewModel.onBiometricSuccess()
                        else -> viewModel.onBiometricPromptDismissed()
                    }
                }
            } else {
                viewModel.onBiometricPromptDismissed()
            }
        }
    }

    LaunchedEffect(uiState.isLoginSuccessful) {
        if (uiState.isLoginSuccessful) {
            if (uiState.requiresEmailConfirmation) {
                onNavigateToConfirmEmail(uiState.email)
            } else {
                onLoginSuccess()
            }
        }
    }

    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    Scaffold(snackbarHost = { SnackbarHost(hostState = snackbarHostState) }) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .background(MaterialTheme.colorScheme.background)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp)
                .padding(top = 64.dp, bottom = 32.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Image(
                painter = painterResource(R.drawable.mascot_waving),
                contentDescription = null,
                modifier = Modifier.size(160.dp),
            )

            Spacer(Modifier.height(24.dp))

            Text(
                text = stringResource(R.string.welcome_back),
                style = MaterialTheme.typography.displayMedium,
                color = MaterialTheme.colorScheme.onBackground,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.login_subtitle),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )

            Spacer(Modifier.height(32.dp))

            CleansiaTextField(
                value = uiState.email,
                onValueChange = viewModel::onEmailChange,
                label = stringResource(R.string.email),
                keyboardType = KeyboardType.Email,
                errorText = uiState.emailError,
                enabled = !uiState.isLoading,
            )

            Spacer(Modifier.height(8.dp))

            CleansiaTextField(
                value = uiState.password,
                onValueChange = viewModel::onPasswordChange,
                label = stringResource(R.string.password),
                isPassword = true,
                errorText = uiState.passwordError,
                enabled = !uiState.isLoading,
            )

            Spacer(Modifier.height(8.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                CleansiaCheckbox(
                    checked = uiState.rememberMe,
                    onCheckedChange = viewModel::onRememberMeChange,
                    label = stringResource(R.string.remember_me),
                )
                CleansiaTextLink(
                    text = stringResource(R.string.forgot_password),
                    onClick = onNavigateToForgotPassword,
                )
            }

            Spacer(Modifier.height(16.dp))

            CleansiaPrimaryButton(
                text = stringResource(R.string.login),
                onClick = { viewModel.login() },
                loading = uiState.isLoading,
                enabled = uiState.email.isNotBlank() && uiState.password.isNotBlank(),
            )

            // Biometric login is shown below the primary button as a secondary
            // option, only when the device supports it AND the user opted in
            // (settings toggle persisted from prior sessions). Customer-app
            // skips this entire block.
            if (canShowBiometric) {
                Spacer(Modifier.height(8.dp))
                CleansiaOutlinedButton(
                    text = stringResource(R.string.biometric_login),
                    onClick = { viewModel.onBiometricLoginRequested() },
                    leadingIcon = Icons.Outlined.Fingerprint,
                    enabled = !uiState.isLoading,
                )
            }

            Spacer(Modifier.height(24.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = stringResource(R.string.dont_have_account),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                CleansiaTextLink(
                    text = stringResource(R.string.sign_up_here),
                    onClick = onNavigateToRegister,
                )
            }
        }
    }
}
