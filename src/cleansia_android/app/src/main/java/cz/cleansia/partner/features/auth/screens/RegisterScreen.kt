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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Lock
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
import cz.cleansia.partner.features.auth.viewmodels.RegisterViewModel
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.ui.graphics.Color
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.ui.components.CleansiaTextField
import cz.cleansia.partner.ui.components.DynamicCleaningBackground

@Composable
fun RegisterScreen(
    onNavigateBack: () -> Unit,
    onNavigateToLogin: () -> Unit,
    onRegistrationSuccess: (String) -> Unit,
    viewModel: RegisterViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Handle registration success
    LaunchedEffect(uiState.isRegistrationSuccessful) {
        if (uiState.isRegistrationSuccessful) {
            uiState.registeredEmail?.let { email ->
                onRegistrationSuccess(email)
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
                    text = stringResource(R.string.create_account),
                    style = MaterialTheme.typography.headlineLarge,
                    color = MaterialTheme.colorScheme.onBackground
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = stringResource(R.string.register_subtitle),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(32.dp))

                // First name + Last name side by side
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    CleansiaTextField(
                        value = uiState.firstName,
                        onValueChange = viewModel::onFirstNameChange,
                        label = stringResource(R.string.first_name),
                        imeAction = ImeAction.Next,
                        isError = uiState.firstNameError != null,
                        errorMessage = uiState.firstNameError,
                        enabled = !uiState.isLoading,
                        modifier = Modifier.weight(1f)
                    )

                    CleansiaTextField(
                        value = uiState.lastName,
                        onValueChange = viewModel::onLastNameChange,
                        label = stringResource(R.string.last_name),
                        imeAction = ImeAction.Next,
                        isError = uiState.lastNameError != null,
                        errorMessage = uiState.lastNameError,
                        enabled = !uiState.isLoading,
                        modifier = Modifier.weight(1f)
                    )
                }

                Spacer(modifier = Modifier.height(16.dp))

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
                    imeAction = ImeAction.Next,
                    isError = uiState.passwordError != null,
                    errorMessage = uiState.passwordError,
                    enabled = !uiState.isLoading
                )

                Spacer(modifier = Modifier.height(16.dp))

                // Confirm password field
                CleansiaTextField(
                    value = uiState.confirmPassword,
                    onValueChange = viewModel::onConfirmPasswordChange,
                    label = stringResource(R.string.confirm_password),
                    leadingIcon = Icons.Default.Lock,
                    isPassword = true,
                    imeAction = ImeAction.Done,
                    onImeAction = { viewModel.register() },
                    isError = uiState.confirmPasswordError != null,
                    errorMessage = uiState.confirmPasswordError,
                    enabled = !uiState.isLoading
                )

                Spacer(modifier = Modifier.height(16.dp))

                // Terms and conditions checkbox
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Checkbox(
                        checked = uiState.acceptTerms,
                        onCheckedChange = viewModel::onAcceptTermsChange,
                        enabled = !uiState.isLoading
                    )
                    Column {
                        Row {
                            Text(
                                text = stringResource(R.string.accept_terms_prefix) + " ",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Text(
                                text = stringResource(R.string.terms_and_conditions),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.clickable {
                                    // TODO: Open terms and conditions
                                }
                            )
                        }
                        if (uiState.termsError != null) {
                            Text(
                                text = uiState.termsError!!,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.error
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(32.dp))

                // Register button
                CleansiaButton(
                    text = stringResource(R.string.register),
                    onClick = { viewModel.register() },
                    isLoading = uiState.isLoading,
                    enabled = !uiState.isLoading
                )

                Spacer(modifier = Modifier.weight(1f))

                // Login link
                Row(
                    horizontalArrangement = Arrangement.Center,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(
                        text = stringResource(R.string.already_have_account) + " ",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = stringResource(R.string.sign_in),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.clickable(enabled = !uiState.isLoading) {
                            onNavigateToLogin()
                        }
                    )
                }

                Spacer(modifier = Modifier.height(24.dp))
            }

            CleansiaSnackbarHost(hostState = snackbarHostState)
        }
    }
}
