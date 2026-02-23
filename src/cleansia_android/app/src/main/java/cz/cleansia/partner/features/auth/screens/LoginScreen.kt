package cz.cleansia.partner.features.auth.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Fingerprint
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.ui.platform.LocalContext
import androidx.fragment.app.FragmentActivity
import cz.cleansia.partner.core.security.BiometricHelper
import cz.cleansia.partner.core.security.BiometricAvailability
import cz.cleansia.partner.core.security.BiometricResult
import androidx.compose.material3.Checkbox
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.features.auth.viewmodels.LoginViewModel
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.ui.graphics.Color
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.CleansiaButtonStyle
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.ui.components.CleansiaTextField
import cz.cleansia.partner.ui.components.DynamicCleaningBackground

@Composable
fun LoginScreen(
    onNavigateToRegister: () -> Unit,
    onNavigateToForgotPassword: () -> Unit,
    onNavigateToConfirmEmail: (String) -> Unit,
    onLoginSuccess: () -> Unit,
    viewModel: LoginViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Biometric setup
    val context = LocalContext.current
    val biometricHelper = remember { BiometricHelper(context) }
    val biometricAvailable = remember {
        biometricHelper.checkBiometricAvailability() == BiometricAvailability.AVAILABLE
    }
    val canShowBiometric = uiState.biometricEnabled && biometricAvailable

    // Handle biometric prompt
    LaunchedEffect(uiState.showBiometricPrompt) {
        if (uiState.showBiometricPrompt) {
            val activity = context as? FragmentActivity
            if (activity != null) {
                biometricHelper.showBiometricPrompt(
                    activity = activity,
                    title = "Biometric Login",
                    subtitle = "Use your fingerprint to login",
                    negativeButtonText = "Cancel"
                ) { result ->
                    when (result) {
                        is BiometricResult.Success -> viewModel.onBiometricSuccess()
                        is BiometricResult.Cancelled -> viewModel.onBiometricPromptDismissed()
                        is BiometricResult.Error -> {
                            viewModel.onBiometricPromptDismissed()
                        }
                        else -> viewModel.onBiometricPromptDismissed()
                    }
                }
            } else {
                viewModel.onBiometricPromptDismissed()
            }
        }
    }

    // Handle login success
    LaunchedEffect(uiState.isLoginSuccessful) {
        if (uiState.isLoginSuccessful) {
            if (uiState.requiresEmailConfirmation) {
                onNavigateToConfirmEmail(uiState.email)
            } else {
                onLoginSuccess()
            }
        }
    }

    // Show error in snackbar
    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    val isDark = isSystemInDarkTheme()

    Scaffold { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            DynamicCleaningBackground(
                iconColor = if (isDark) Color(0xFF38BDF8) else Color(0xFF0EA5E9),
                iconAlpha = if (isDark) 0.08f else 0.06f
            )

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(24.dp)
                    .verticalScroll(rememberScrollState()),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Spacer(modifier = Modifier.height(48.dp))

                // Header
                Text(
                    text = stringResource(R.string.welcome_back),
                    style = MaterialTheme.typography.headlineLarge,
                    color = MaterialTheme.colorScheme.onBackground
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = stringResource(R.string.login_subtitle),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(48.dp))

                // Email field
                CleansiaTextField(
                    value = uiState.email,
                    onValueChange = viewModel::onEmailChange,
                    label = stringResource(R.string.email),
                    leadingIcon = Icons.Default.Email,
                    keyboardType = KeyboardType.Email,
                    imeAction = ImeAction.Next,
                    isError = uiState.emailError != null,
                    errorMessage = uiState.emailError,
                    enabled = !uiState.isLoading
                )

                Spacer(modifier = Modifier.height(16.dp))

                // Password field
                CleansiaTextField(
                    value = uiState.password,
                    onValueChange = viewModel::onPasswordChange,
                    label = stringResource(R.string.password),
                    leadingIcon = Icons.Default.Lock,
                    isPassword = true,
                    imeAction = ImeAction.Done,
                    onImeAction = { viewModel.login() },
                    isError = uiState.passwordError != null,
                    errorMessage = uiState.passwordError,
                    enabled = !uiState.isLoading
                )

                Spacer(modifier = Modifier.height(16.dp))

                // Remember me and Forgot password row
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.clickable(enabled = !uiState.isLoading) {
                            viewModel.onRememberMeChange(!uiState.rememberMe)
                        }
                    ) {
                        Checkbox(
                            checked = uiState.rememberMe,
                            onCheckedChange = viewModel::onRememberMeChange,
                            enabled = !uiState.isLoading
                        )
                        Text(
                            text = stringResource(R.string.remember_me),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    Text(
                        text = stringResource(R.string.forgot_password),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable(enabled = !uiState.isLoading) {
                            onNavigateToForgotPassword()
                        }
                    )
                }

                Spacer(modifier = Modifier.height(32.dp))

                // Login button with optional biometric
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    CleansiaButton(
                        text = stringResource(R.string.login),
                        onClick = { viewModel.login() },
                        isLoading = uiState.isLoading,
                        enabled = !uiState.isLoading,
                        modifier = Modifier.weight(1f)
                    )

                    // Biometric button (only show if enabled and available)
                    if (canShowBiometric) {
                        IconButton(
                            onClick = { viewModel.onBiometricLoginRequested() },
                            enabled = !uiState.isLoading,
                            colors = IconButtonDefaults.iconButtonColors(
                                containerColor = MaterialTheme.colorScheme.primaryContainer,
                                contentColor = MaterialTheme.colorScheme.onPrimaryContainer
                            ),
                            modifier = Modifier.size(56.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Fingerprint,
                                contentDescription = stringResource(R.string.biometric_login),
                                modifier = Modifier.size(32.dp)
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.weight(1f))

                // Register link
                Row(
                    horizontalArrangement = Arrangement.Center,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(
                        text = stringResource(R.string.dont_have_account) + " ",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = stringResource(R.string.sign_up_here),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable(enabled = !uiState.isLoading) {
                            onNavigateToRegister()
                        }
                    )
                }

                Spacer(modifier = Modifier.height(24.dp))
            }

            CleansiaSnackbarHost(hostState = snackbarHostState)
        }
    }
}
