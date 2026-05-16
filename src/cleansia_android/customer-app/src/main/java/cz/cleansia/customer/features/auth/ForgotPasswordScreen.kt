package cz.cleansia.customer.features.auth

import cz.cleansia.core.ui.components.CleansiaTextLink

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
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.RadioButtonUnchecked
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.ErrorText
import cz.cleansia.customer.ui.theme.SuccessText

/**
 * Forgot Password — mirrors the web's [`forgot-password.component.html`].
 * Two-phase flow: email input → code + new password.
 */
@Composable
fun ForgotPasswordScreen(
    onSendCode: (email: String) -> Unit = {},
    onChangePassword: (email: String, code: String, newPassword: String) -> Unit = { _, _, _ -> },
    onRegister: () -> Unit = {},
    onBackToLogin: () -> Unit = {},
    loading: Boolean = false,
) {
    var email by remember { mutableStateOf("") }
    var isEmailSent by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onBackToLogin) {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.common_back),
                )
            }
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp)
                .padding(top = 8.dp, bottom = 32.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Image(
                painter = painterResource(R.drawable.mascot_waving),
                contentDescription = null,
                modifier = Modifier.size(140.dp),
            )

            Spacer(Modifier.height(24.dp))

            if (!isEmailSent) {
                EmailStep(
                    email = email,
                    onEmailChange = { email = it },
                    loading = loading,
                    onSend = {
                        onSendCode(email)
                        isEmailSent = true
                    },
                    onRegister = onRegister,
                )
            } else {
                CodeStep(
                    email = email,
                    loading = loading,
                    onResend = { onSendCode(email) },
                    onSubmit = { code, newPassword -> onChangePassword(email, code, newPassword) },
                    onBackToLogin = onBackToLogin,
                )
            }
        }
    }
}

@Composable
private fun EmailStep(
    email: String,
    onEmailChange: (String) -> Unit,
    loading: Boolean,
    onSend: () -> Unit,
    onRegister: () -> Unit,
) {
    Text(
        text = stringResource(R.string.forgot_title),
        style = MaterialTheme.typography.displayMedium,
        color = MaterialTheme.colorScheme.onBackground,
        textAlign = TextAlign.Center,
    )
    Spacer(Modifier.height(8.dp))
    Text(
        text = stringResource(R.string.forgot_description),
        style = MaterialTheme.typography.bodyLarge,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        textAlign = TextAlign.Center,
    )
    Spacer(Modifier.height(32.dp))
    CleansiaTextField(
        value = email,
        onValueChange = onEmailChange,
        label = stringResource(R.string.forgot_email),
        keyboardType = KeyboardType.Email,
    )
    Spacer(Modifier.height(16.dp))
    CleansiaPrimaryButton(
        text = stringResource(R.string.forgot_send_code),
        onClick = onSend,
        loading = loading,
        enabled = email.isNotBlank(),
    )
    Spacer(Modifier.height(24.dp))
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(
            text = stringResource(R.string.forgot_dont_have_account),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        CleansiaTextLink(
            text = stringResource(R.string.login_register),
            onClick = onRegister,
        )
    }
}

@Composable
private fun CodeStep(
    email: String,
    loading: Boolean,
    onResend: () -> Unit,
    onSubmit: (code: String, newPassword: String) -> Unit,
    onBackToLogin: () -> Unit,
) {
    var code by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }

    val hasMinLength = newPassword.length >= 12
    val hasLetter = newPassword.any { it.isLetter() }
    val hasNumber = newPassword.any { it.isDigit() }
    val passwordsMatch = newPassword.isNotEmpty() && newPassword == confirmPassword
    val hasPasswordInput = newPassword.isNotEmpty()
    val hasConfirmInput = confirmPassword.isNotEmpty()

    val formValid = code.isNotBlank() &&
        hasMinLength && hasLetter && hasNumber &&
        passwordsMatch

    Text(
        text = stringResource(R.string.forgot_email_sent_title),
        style = MaterialTheme.typography.displayMedium,
        color = MaterialTheme.colorScheme.onBackground,
        textAlign = TextAlign.Center,
    )
    Spacer(Modifier.height(8.dp))
    Text(
        text = stringResource(R.string.forgot_email_sent_text, email),
        style = MaterialTheme.typography.bodyLarge,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        textAlign = TextAlign.Center,
    )

    Spacer(Modifier.height(24.dp))

    CleansiaTextField(
        value = code,
        onValueChange = { code = it.filter(Char::isDigit).take(8) },
        label = stringResource(R.string.forgot_code),
        keyboardType = KeyboardType.Number,
    )

    Spacer(Modifier.height(8.dp))

    CleansiaTextField(
        value = newPassword,
        onValueChange = { newPassword = it },
        label = stringResource(R.string.forgot_new_password),
        isPassword = true,
    )
    RuleList(
        rules = listOf(
            stringResource(R.string.register_pw_min_length) to hasMinLength,
            stringResource(R.string.register_pw_letter) to hasLetter,
            stringResource(R.string.register_pw_number) to hasNumber,
        ),
        hasInput = hasPasswordInput,
    )

    Spacer(Modifier.height(8.dp))

    CleansiaTextField(
        value = confirmPassword,
        onValueChange = { confirmPassword = it },
        label = stringResource(R.string.forgot_confirm_new_password),
        isPassword = true,
    )
    RuleList(
        rules = listOf(stringResource(R.string.register_pw_match) to passwordsMatch),
        hasInput = hasConfirmInput,
    )

    Spacer(Modifier.height(16.dp))

    CleansiaPrimaryButton(
        text = stringResource(R.string.forgot_change_password),
        onClick = { onSubmit(code, newPassword) },
        loading = loading,
        enabled = formValid,
    )

    Spacer(Modifier.height(8.dp))

    CleansiaOutlinedButton(
        text = stringResource(R.string.forgot_resend_code),
        onClick = onResend,
        leadingIcon = Icons.Outlined.Refresh,
    )

    Spacer(Modifier.height(24.dp))

    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(
            text = stringResource(R.string.forgot_remember_password),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        CleansiaTextLink(
            text = stringResource(R.string.register_login_link),
            onClick = onBackToLogin,
        )
    }
}

@Composable
private fun RuleList(rules: List<Pair<String, Boolean>>, hasInput: Boolean) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp, vertical = 4.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        rules.forEach { (text, valid) ->
            val (icon, tint) = when {
                valid -> Icons.Outlined.Check to SuccessText
                hasInput -> Icons.Outlined.Close to ErrorText
                else -> Icons.Outlined.RadioButtonUnchecked to MaterialTheme.colorScheme.onSurfaceVariant
            }
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = tint,
                    modifier = Modifier.size(14.dp),
                )
                Text(
                    text = text,
                    style = MaterialTheme.typography.bodySmall,
                    color = if (valid || !hasInput) MaterialTheme.colorScheme.onSurfaceVariant else ErrorText,
                )
            }
        }
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun ForgotPasswordPreview() {
    CleansiaTheme { ForgotPasswordScreen() }
}

@Preview(widthDp = 390, heightDp = 900, uiMode = android.content.res.Configuration.UI_MODE_NIGHT_YES)
@Composable
private fun ForgotPasswordPreviewDark() {
    CleansiaTheme(darkTheme = true) { ForgotPasswordScreen() }
}
