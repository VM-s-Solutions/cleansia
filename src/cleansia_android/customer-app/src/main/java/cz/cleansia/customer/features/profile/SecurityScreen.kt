package cz.cleansia.customer.features.profile

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
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.ErrorText
import cz.cleansia.customer.ui.theme.SuccessText
import cz.cleansia.core.ui.theme.Poppins

/**
 * Security — change-password via the email reset-code flow. Reuses the same
 * two-step flow as Forgot Password (request a code → enter code + new password),
 * but the email is known from the session so it's never typed.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SecurityScreen(
    onBack: () -> Unit = {},
    onSendCode: () -> Unit = {},
    onChangePassword: (code: String, newPassword: String) -> Unit = { _, _ -> },
    loading: Boolean = false,
) {
    var codeSent by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        TopAppBar(
            title = { Text(stringResource(R.string.profile_security_title), style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold)) },
            navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back)) } },
            colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.surface),
        )

        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(20.dp),
        ) {
            Text(stringResource(R.string.profile_security_change_password), style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onBackground)
            Spacer(Modifier.height(8.dp))
            Text(stringResource(R.string.security_intro), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(24.dp))

            if (!codeSent) {
                CleansiaPrimaryButton(
                    text = stringResource(R.string.security_request_code),
                    onClick = {
                        onSendCode()
                        codeSent = true
                    },
                    loading = loading,
                )
            } else {
                ChangePasswordForm(
                    loading = loading,
                    onResend = onSendCode,
                    onSubmit = onChangePassword,
                )
            }
        }
    }
}

@Composable
private fun ChangePasswordForm(
    loading: Boolean,
    onResend: () -> Unit,
    onSubmit: (code: String, newPassword: String) -> Unit,
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
        text = stringResource(R.string.security_code_helper),
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    Spacer(Modifier.height(12.dp))

    CleansiaTextField(
        value = code,
        onValueChange = { code = it.filter(Char::isDigit).take(8) },
        label = stringResource(R.string.security_code_label),
        keyboardType = KeyboardType.Number,
    )
    Spacer(Modifier.height(8.dp))

    CleansiaTextField(
        value = newPassword,
        onValueChange = { newPassword = it },
        label = stringResource(R.string.profile_security_new),
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
        label = stringResource(R.string.profile_security_confirm),
        isPassword = true,
    )
    RuleList(
        rules = listOf(stringResource(R.string.register_pw_match) to passwordsMatch),
        hasInput = hasConfirmInput,
    )
    Spacer(Modifier.height(16.dp))

    CleansiaPrimaryButton(
        text = stringResource(R.string.profile_security_update),
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
private fun SecurityPreview() {
    CleansiaTheme { SecurityScreen() }
}
