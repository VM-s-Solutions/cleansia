package cz.cleansia.partner.features.auth

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
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
import androidx.compose.ui.autofill.ContentType
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentType
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.components.CodeInput
import cz.cleansia.core.ui.components.PasswordRuleList
import cz.cleansia.partner.R

/**
 * 6-digit email-verification entry, and the password the confirmation sets when the one typed at
 * registration is not held. Auto-submits once the user reaches 6 digits via [LaunchedEffect] on code
 * length, unless it asks for a password. Resend re-issues the code with the user's preferred locale.
 * Back goes to login.
 */
@Composable
fun ConfirmEmailScreen(
    onNavigateBack: () -> Unit,
    onConfirmationSuccess: () -> Unit,
    viewModel: ConfirmEmailViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(uiState.isConfirmationSuccessful) {
        if (uiState.isConfirmationSuccessful) onConfirmationSuccess()
    }

    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    LaunchedEffect(uiState.resendSuccessMessage) {
        uiState.resendSuccessMessage?.let { msg ->
            snackbarHostState.showSnackbar(msg)
            viewModel.clearResendSuccessMessage()
        }
    }

    LaunchedEffect(uiState.code) {
        if (uiState.code.length == CONFIRMATION_CODE_LENGTH && !uiState.isLoading && uiState.submitsOnCodeEntry) {
            viewModel.confirmEmail()
        }
    }

    Scaffold(snackbarHost = { SnackbarHost(hostState = snackbarHostState) }) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .background(MaterialTheme.colorScheme.background),
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                IconButton(onClick = onNavigateBack) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                        contentDescription = stringResource(R.string.back),
                        tint = MaterialTheme.colorScheme.onBackground,
                    )
                }
            }

            // The back row stays at the top; the form centres in the space under it.
            CenteredAuthColumn(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.verify_email),
                    style = MaterialTheme.typography.displayMedium,
                    color = MaterialTheme.colorScheme.onBackground,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.verify_email_subtitle),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )

                Spacer(Modifier.height(32.dp))

                CodeInput(
                    code = uiState.code,
                    onCodeChange = viewModel::onCodeChange,
                    length = CONFIRMATION_CODE_LENGTH,
                )

                if (uiState.asksForPassword) {
                    Spacer(Modifier.height(24.dp))
                    Text(
                        text = stringResource(R.string.confirm_email_password_prompt),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        textAlign = TextAlign.Center,
                    )
                    Spacer(Modifier.height(8.dp))
                    CleansiaTextField(
                        value = uiState.password,
                        onValueChange = viewModel::onPasswordChange,
                        label = stringResource(R.string.password),
                        modifier = Modifier.semantics { contentType = ContentType.NewPassword },
                        isPassword = true,
                        enabled = !uiState.isLoading,
                    )
                    PasswordRuleList(
                        rules = listOf(
                            stringResource(R.string.register_pw_min_length) to uiState.passwordHasMinLength,
                            stringResource(R.string.register_pw_letter) to uiState.passwordHasLetter,
                            stringResource(R.string.register_pw_number) to uiState.passwordHasNumber,
                        ),
                        hasInput = uiState.password.isNotEmpty(),
                    )
                    Spacer(Modifier.height(8.dp))
                    CleansiaTextField(
                        value = uiState.confirmPassword,
                        onValueChange = viewModel::onConfirmPasswordChange,
                        label = stringResource(R.string.confirm_password),
                        modifier = Modifier.semantics { contentType = ContentType.NewPassword },
                        isPassword = true,
                        enabled = !uiState.isLoading,
                    )
                    PasswordRuleList(
                        rules = listOf(stringResource(R.string.register_pw_match) to uiState.passwordsMatch),
                        hasInput = uiState.confirmPassword.isNotEmpty(),
                    )
                }

                Spacer(Modifier.height(24.dp))

                CleansiaPrimaryButton(
                    text = stringResource(R.string.verify),
                    onClick = { viewModel.confirmEmail() },
                    loading = uiState.isLoading,
                    enabled = uiState.canSubmit && !uiState.isLoading,
                    trailingIcon = Icons.Outlined.CheckCircle,
                )

                Spacer(Modifier.height(8.dp))

                CleansiaOutlinedButton(
                    text = stringResource(R.string.resend_code),
                    onClick = { viewModel.resendCode() },
                    leadingIcon = Icons.Outlined.Refresh,
                    enabled = !uiState.isResending && !uiState.isLoading,
                )
            }
        }
    }
}
