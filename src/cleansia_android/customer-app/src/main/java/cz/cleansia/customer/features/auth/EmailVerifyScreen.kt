package cz.cleansia.customer.features.auth

import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.autofill.ContentType
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentType
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.customer.R
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.components.CodeInput
import cz.cleansia.core.ui.components.PasswordRuleList
import cz.cleansia.core.validation.PasswordPolicy
import cz.cleansia.customer.features.auth.EmailVerifyForm.CODE_LENGTH
import cz.cleansia.customer.ui.theme.CleansiaTheme

/**
 * Email Verify — mirrors the web's [`confirm-email.component.html`].
 * 6-digit OTP code with resend + verify buttons, and the password the confirmation sets when the one
 * typed at registration is not held ([asksForPassword]). The code submits on completion only when no
 * password is asked.
 */
@Composable
fun EmailVerifyScreen(
    email: String? = null,
    asksForPassword: Boolean = true,
    onVerify: (code: String, password: String?) -> Unit = { _, _ -> },
    onResend: (email: String) -> Unit = {},
    onBack: () -> Unit = {},
    loading: Boolean = false,
) {
    var code by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }

    LaunchedEffect(code) {
        if (code.length == CODE_LENGTH && !asksForPassword) onVerify(code, null)
    }

    // The activity is edge-to-edge and this route consumes no insets: the back row pads below the
    // status bar, and the form above a 3-button nav bar and the keyboard (as SignInScreen does).
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .systemBarsPadding()
            .imePadding(),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onBack) {
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
            Text(
                text = stringResource(R.string.verify_title),
                style = MaterialTheme.typography.displayMedium,
                color = MaterialTheme.colorScheme.onBackground,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.verify_description),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )

            Spacer(Modifier.height(32.dp))

            CodeInput(
                code = code,
                onCodeChange = { new -> code = new.filter(Char::isDigit).take(CODE_LENGTH) },
            )

            if (asksForPassword) {
                Spacer(Modifier.height(24.dp))
                Text(
                    text = stringResource(R.string.verify_password_prompt),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(8.dp))
                CleansiaTextField(
                    value = password,
                    onValueChange = { password = it },
                    label = stringResource(R.string.register_password),
                    modifier = Modifier.semantics { contentType = ContentType.NewPassword },
                    isPassword = true,
                )
                PasswordRuleList(
                    rules = listOf(
                        stringResource(R.string.register_pw_min_length) to PasswordPolicy.hasMinLength(password),
                        stringResource(R.string.register_pw_letter) to PasswordPolicy.hasLetter(password),
                        stringResource(R.string.register_pw_number) to PasswordPolicy.hasNumber(password),
                    ),
                    hasInput = password.isNotEmpty(),
                )
                Spacer(Modifier.height(8.dp))
                CleansiaTextField(
                    value = confirmPassword,
                    onValueChange = { confirmPassword = it },
                    label = stringResource(R.string.register_confirm_password),
                    modifier = Modifier.semantics { contentType = ContentType.NewPassword },
                    isPassword = true,
                )
                PasswordRuleList(
                    rules = listOf(
                        stringResource(R.string.register_pw_match) to PasswordPolicy.passwordsMatch(password, confirmPassword),
                    ),
                    hasInput = confirmPassword.isNotEmpty(),
                )
            }

            Spacer(Modifier.height(24.dp))

            CleansiaPrimaryButton(
                text = stringResource(R.string.verify_submit),
                onClick = { onVerify(code, if (asksForPassword) password else null) },
                loading = loading,
                enabled = EmailVerifyForm.isSubmittable(code, asksForPassword, password, confirmPassword),
                trailingIcon = Icons.Outlined.CheckCircle,
            )

            Spacer(Modifier.height(8.dp))

            CleansiaOutlinedButton(
                text = stringResource(R.string.verify_resend_code),
                onClick = { email?.let(onResend) },
                leadingIcon = Icons.Outlined.Refresh,
            )
        }
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun EmailVerifyPreview() {
    CleansiaTheme { EmailVerifyScreen() }
}

@Preview(widthDp = 390, heightDp = 844, uiMode = android.content.res.Configuration.UI_MODE_NIGHT_YES)
@Composable
private fun EmailVerifyPreviewDark() {
    CleansiaTheme(darkTheme = true) { EmailVerifyScreen() }
}
