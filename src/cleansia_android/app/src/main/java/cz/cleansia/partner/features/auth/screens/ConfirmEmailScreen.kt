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
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Email
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
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
import cz.cleansia.partner.features.auth.viewmodels.ConfirmEmailViewModel
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.ui.components.CleansiaTextField
import cz.cleansia.partner.ui.components.GlassBackButton

@Composable
fun ConfirmEmailScreen(
    onNavigateBack: () -> Unit,
    onConfirmationSuccess: () -> Unit,
    viewModel: ConfirmEmailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Handle confirmation success
    LaunchedEffect(uiState.isConfirmationSuccessful) {
        if (uiState.isConfirmationSuccessful) {
            onConfirmationSuccess()
        }
    }

    // Show error in snackbar
    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Show resend success message
    LaunchedEffect(uiState.resendSuccessMessage) {
        uiState.resendSuccessMessage?.let { message ->
            snackbarHostState.showSnackbar(message)
            viewModel.clearResendSuccessMessage()
        }
    }

    Scaffold { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .statusBarsPadding()
                    .padding(top = 56.dp)
                    .padding(horizontal = 24.dp)
                    .imePadding()
                    .verticalScroll(rememberScrollState()),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Spacer(modifier = Modifier.height(32.dp))

                // Email icon
                Icon(
                    imageVector = Icons.Outlined.Email,
                    contentDescription = null,
                    modifier = Modifier.size(80.dp),
                    tint = MaterialTheme.colorScheme.primary
                )

                Spacer(modifier = Modifier.height(24.dp))

                // Header
                Text(
                    text = stringResource(R.string.verify_email),
                    style = MaterialTheme.typography.headlineLarge,
                    color = MaterialTheme.colorScheme.onBackground
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = stringResource(R.string.verify_email_subtitle),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(8.dp))

                // Display the email
                Text(
                    text = uiState.email,
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.primary,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(32.dp))

                // Verification code field
                CleansiaTextField(
                    value = uiState.code,
                    onValueChange = viewModel::onCodeChange,
                    label = stringResource(R.string.verification_code),
                    keyboardType = KeyboardType.Number,
                    imeAction = ImeAction.Done,
                    onImeAction = { viewModel.confirmEmail() },
                    isError = uiState.codeError != null,
                    errorMessage = uiState.codeError,
                    enabled = !uiState.isLoading
                )

                Spacer(modifier = Modifier.height(24.dp))

                // Verify button
                CleansiaButton(
                    text = stringResource(R.string.verify),
                    onClick = { viewModel.confirmEmail() },
                    isLoading = uiState.isLoading,
                    enabled = !uiState.isLoading && uiState.code.length == 6
                )

                Spacer(modifier = Modifier.height(24.dp))

                // Resend code link
                Row(
                    horizontalArrangement = Arrangement.Center,
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(
                        text = stringResource(R.string.didnt_receive_code) + " ",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    if (uiState.isResending) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(16.dp),
                            strokeWidth = 2.dp
                        )
                    } else {
                        Text(
                            text = stringResource(R.string.resend_code),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.clickable(enabled = !uiState.isLoading) {
                                viewModel.resendCode()
                            }
                        )
                    }
                }

                Spacer(modifier = Modifier.height(24.dp))
            }

            GlassBackButton(onNavigateBack = onNavigateBack)

            CleansiaSnackbarHost(hostState = snackbarHostState)
        }
    }
}
