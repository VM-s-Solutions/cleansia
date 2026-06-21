package cz.cleansia.partner.features.auth

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
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaCheckbox
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.PasswordRuleList
import cz.cleansia.partner.R

/**
 * Partner sign-up screen — mascot → title → first/last name row → email →
 * password + rule list → confirm password + match rule → terms checkbox →
 * register button → footer link. No Google OAuth, no referral code.
 *
 * Success routes back to Login — the user receives a verification email and
 * signs in there; after the first successful sign-in with unconfirmed email
 * the Login flow forwards them to ConfirmEmailScreen.
 */
@Composable
fun RegisterScreen(
    onNavigateToLogin: () -> Unit,
    onRegisterSuccess: () -> Unit,
    viewModel: RegisterViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }

    val hasPasswordInput = uiState.password.isNotEmpty()
    val hasConfirmInput = uiState.confirmPassword.isNotEmpty()

    val formValid = uiState.firstName.isNotBlank() &&
        uiState.lastName.isNotBlank() &&
        uiState.email.isNotBlank() &&
        uiState.passwordHasMinLength && uiState.passwordHasLetter && uiState.passwordHasNumber &&
        uiState.passwordsMatch &&
        uiState.acceptTerms

    LaunchedEffect(uiState.isRegistrationSuccessful) {
        if (uiState.isRegistrationSuccessful) onRegisterSuccess()
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
                modifier = Modifier.size(140.dp),
            )

            Spacer(Modifier.height(20.dp))

            Text(
                text = stringResource(R.string.create_account),
                style = MaterialTheme.typography.displayMedium,
                color = MaterialTheme.colorScheme.onBackground,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.register_subtitle),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )

            Spacer(Modifier.height(24.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                CleansiaTextField(
                    value = uiState.firstName,
                    onValueChange = viewModel::onFirstNameChange,
                    label = stringResource(R.string.first_name),
                    modifier = Modifier.weight(1f),
                    errorText = uiState.firstNameError,
                    enabled = !uiState.isLoading,
                )
                CleansiaTextField(
                    value = uiState.lastName,
                    onValueChange = viewModel::onLastNameChange,
                    label = stringResource(R.string.last_name),
                    modifier = Modifier.weight(1f),
                    errorText = uiState.lastNameError,
                    enabled = !uiState.isLoading,
                )
            }

            Spacer(Modifier.height(8.dp))

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

            PasswordRuleList(
                rules = listOf(
                    stringResource(R.string.register_pw_min_length) to uiState.passwordHasMinLength,
                    stringResource(R.string.register_pw_letter) to uiState.passwordHasLetter,
                    stringResource(R.string.register_pw_number) to uiState.passwordHasNumber,
                ),
                hasInput = hasPasswordInput,
            )

            Spacer(Modifier.height(8.dp))

            CleansiaTextField(
                value = uiState.confirmPassword,
                onValueChange = viewModel::onConfirmPasswordChange,
                label = stringResource(R.string.confirm_password),
                isPassword = true,
                errorText = uiState.confirmPasswordError,
                enabled = !uiState.isLoading,
            )

            PasswordRuleList(
                rules = listOf(
                    stringResource(R.string.register_pw_match) to uiState.passwordsMatch,
                ),
                hasInput = hasConfirmInput,
            )

            Spacer(Modifier.height(12.dp))

            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
                CleansiaCheckbox(
                    checked = uiState.acceptTerms,
                    onCheckedChange = viewModel::onAcceptTermsChange,
                    label = stringResource(R.string.accept_terms),
                )
            }
            uiState.termsError?.let { err ->
                Text(
                    text = err,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(start = 4.dp, top = 2.dp),
                )
            }

            Spacer(Modifier.height(16.dp))

            CleansiaPrimaryButton(
                text = stringResource(R.string.register),
                onClick = { viewModel.register() },
                loading = uiState.isLoading,
                enabled = formValid && !uiState.isLoading,
            )

            Spacer(Modifier.height(24.dp))

            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = stringResource(R.string.already_have_account),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                CleansiaTextLink(
                    text = stringResource(R.string.sign_in_here),
                    onClick = onNavigateToLogin,
                )
            }
        }
    }
}
