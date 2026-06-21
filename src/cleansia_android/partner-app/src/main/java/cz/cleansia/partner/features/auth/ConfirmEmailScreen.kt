package cz.cleansia.partner.features.auth

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
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
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CodeInput
import cz.cleansia.partner.R

private const val CODE_LENGTH = 6

/**
 * 6-digit email-verification entry. Auto-submits once the user reaches 6
 * digits via [LaunchedEffect] on code length. Resend re-issues the code with
 * the user's preferred locale. Back goes to login.
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
        if (uiState.code.length == CODE_LENGTH && !uiState.isLoading) {
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

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 24.dp)
                    .padding(bottom = 32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Image(
                    painter = painterResource(R.drawable.mascot_waving),
                    contentDescription = null,
                    modifier = Modifier.size(140.dp),
                )

                Spacer(Modifier.height(20.dp))

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
                    length = CODE_LENGTH,
                )

                Spacer(Modifier.height(24.dp))

                CleansiaPrimaryButton(
                    text = stringResource(R.string.verify),
                    onClick = { viewModel.confirmEmail() },
                    loading = uiState.isLoading,
                    enabled = uiState.code.length == CODE_LENGTH && !uiState.isLoading,
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
