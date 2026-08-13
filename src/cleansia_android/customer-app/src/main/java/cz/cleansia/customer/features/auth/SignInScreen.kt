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
import androidx.compose.material.icons.outlined.Mail
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
import cz.cleansia.core.ui.components.LabelledDivider
import cz.cleansia.customer.ui.theme.CleansiaTheme

/**
 * Sign In — mirrors the web's [`login.component.html`], minus the remember-me checkbox.
 * Layout: mascot above the form → brand wordmark → title → email + password → forgot-password row →
 *         primary Log in button → OR divider → Google button → "Don't have an account? Register" footer.
 *
 * The remember-me box is deliberately absent. It defaulted to *unchecked*, so the common case
 * asked for a 24-hour refresh token — and on a personal handset the only thing that bought was
 * a forced re-login after a day of not opening the app. Worse, on this app it was decorative:
 * `MobileLogin` discards `rememberMe` and forces the long lifetime server-side regardless, so
 * the box never did anything a customer could observe. The session is now uniformly 30 days
 * across every mobile surface; the web keeps its checkbox, where the shared-computer case is
 * real. See [AuthViewModel.signIn].
 */
@Composable
fun SignInScreen(
    onSignInClick: (email: String, password: String) -> Unit = { _, _ -> },
    onForgotPassword: () -> Unit = {},
    onCreateAccount: () -> Unit = {},
    onGoogleSignIn: () -> Unit = {},
    loading: Boolean = false,
) {
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp)
            .padding(top = 64.dp, bottom = 32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        // Mascot
        Image(
            painter = painterResource(R.drawable.mascot_waving),
            contentDescription = null,
            modifier = Modifier.size(160.dp),
        )

        Spacer(Modifier.height(24.dp))

        // Title + subtitle
        Text(
            text = stringResource(R.string.login_title),
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

        // Email
        CleansiaTextField(
            value = email,
            onValueChange = { email = it },
            label = stringResource(R.string.login_email),
            keyboardType = KeyboardType.Email,
        )

        Spacer(Modifier.height(8.dp))

        // Password
        CleansiaTextField(
            value = password,
            onValueChange = { password = it },
            label = stringResource(R.string.login_password),
            isPassword = true,
        )

        Spacer(Modifier.height(8.dp))

        // Arrangement.End, not SpaceBetween: the remember-me checkbox that used to hold the left of
        // this row is gone, and SpaceBetween with a lone child would flip the link to the left edge.
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.End,
        ) {
            CleansiaTextLink(
                text = stringResource(R.string.login_forgot_password),
                onClick = onForgotPassword,
            )
        }

        Spacer(Modifier.height(16.dp))

        // Primary login
        CleansiaPrimaryButton(
            text = stringResource(R.string.login_login),
            onClick = { onSignInClick(email, password) },
            loading = loading,
            enabled = email.isNotBlank() && password.isNotBlank(),
        )

        Spacer(Modifier.height(8.dp))

        LabelledDivider(label = stringResource(R.string.login_or))

        Spacer(Modifier.height(8.dp))

        // Google button (placeholder — uses mail icon until real Google logo asset is added)
        CleansiaOutlinedButton(
            text = stringResource(R.string.login_continue_with_google),
            onClick = onGoogleSignIn,
            leadingIcon = Icons.Outlined.Mail,
            enabled = !loading,
        )

        Spacer(Modifier.height(24.dp))

        // Footer link
        Row(
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                text = stringResource(R.string.login_dont_have_account),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            CleansiaTextLink(
                text = stringResource(R.string.login_register),
                onClick = onCreateAccount,
            )
        }
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun SignInPreview() {
    CleansiaTheme { SignInScreen() }
}

@Preview(widthDp = 390, heightDp = 844, uiMode = android.content.res.Configuration.UI_MODE_NIGHT_YES)
@Composable
private fun SignInPreviewDark() {
    CleansiaTheme(darkTheme = true) { SignInScreen() }
}
