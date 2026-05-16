package cz.cleansia.customer.features.auth

import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.auth.AuthInterceptor
import cz.cleansia.core.auth.TokenStore

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.CardGiftcard
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Mail
import androidx.compose.material.icons.outlined.RadioButtonUnchecked
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.referral.ReferralValidationError
import cz.cleansia.customer.features.booking.ReferralCodeBottomSheet
import cz.cleansia.customer.features.booking.ReferralCodeUiState
import cz.cleansia.core.ui.components.CleansiaCheckbox
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.LabelledDivider
import cz.cleansia.core.ui.components.PasswordRuleList
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.ErrorText
import cz.cleansia.customer.ui.theme.SuccessText
/**
 * Sign Up — mirrors the web's [`register.component.html`].
 * Layout: mascot → brand → title → first/last name row → email → password + rule list →
 *         confirm password + match hint → terms checkbox → Register → OR → Google → "Have account? Log in".
 */
@Composable
fun SignUpScreen(
    onRegisterClick: (firstName: String, lastName: String, email: String, password: String, referralCode: String?) -> Unit = { _, _, _, _, _ -> },
    onLoginClick: () -> Unit = {},
    onGoogleSignIn: () -> Unit = {},
    loading: Boolean = false,
    viewModel: SignUpViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    var firstName by remember { mutableStateOf("") }
    var lastName by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var acceptedTerms by remember { mutableStateOf(false) }
    // Authoritative applied referral code — only mutated when the dialog's
    // Apply path resolves to Valid. Cancel never overwrites it.
    var referralCode by rememberSaveable { mutableStateOf("") }
    // True iff [referralCode] above corresponds to a backend-validated code, so
    // we can render the "applied" state on the entry row vs the empty chevron.
    var referralValidated by rememberSaveable { mutableStateOf(false) }
    var referralSheetOpen by remember { mutableStateOf(false) }

    val hasMinLength = password.length >= 12
    val hasLetter = password.any { it.isLetter() }
    val hasNumber = password.any { it.isDigit() }
    val passwordsMatch = password.isNotEmpty() && password == confirmPassword
    val hasPasswordInput = password.isNotEmpty()
    val hasConfirmInput = confirmPassword.isNotEmpty()

    val formValid = firstName.isNotBlank() &&
        lastName.isNotBlank() &&
        email.isNotBlank() &&
        hasMinLength && hasLetter && hasNumber &&
        passwordsMatch &&
        acceptedTerms

    // Loyalty Phase C — referral repo via the holder VM. The validate call is
    // safe without a token; AuthInterceptor skips Authorization when the
    // TokenStore is empty and the backend endpoint is [AllowAnonymous].
    val referralRepo = viewModel.referralRepository

    Column(
        modifier = Modifier
            .fillMaxSize()
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
            text = stringResource(R.string.register_title),
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
                value = firstName,
                onValueChange = { firstName = it },
                label = stringResource(R.string.register_first_name),
                modifier = Modifier.weight(1f),
            )
            CleansiaTextField(
                value = lastName,
                onValueChange = { lastName = it },
                label = stringResource(R.string.register_last_name),
                modifier = Modifier.weight(1f),
            )
        }

        Spacer(Modifier.height(8.dp))

        CleansiaTextField(
            value = email,
            onValueChange = { email = it },
            label = stringResource(R.string.register_email),
            keyboardType = KeyboardType.Email,
        )

        Spacer(Modifier.height(8.dp))

        CleansiaTextField(
            value = password,
            onValueChange = { password = it },
            label = stringResource(R.string.register_password),
            isPassword = true,
        )

        PasswordRuleList(
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
            label = stringResource(R.string.register_confirm_password),
            isPassword = true,
        )

        PasswordRuleList(
            rules = listOf(
                stringResource(R.string.register_pw_match) to passwordsMatch,
            ),
            hasInput = hasConfirmInput,
        )

        Spacer(Modifier.height(8.dp))

        // ── Loyalty Phase C — referral code Wolt-style entry row ──
        // Tap to open the dialog; Cancel preserves the previously applied code.
        // Bad codes don't block submit — backend is fail-soft.
        ReferralCodeRow(
            appliedCode = if (referralValidated) referralCode else "",
            onClick = { referralSheetOpen = true },
            onClear = {
                referralCode = ""
                referralValidated = false
            },
        )

        Spacer(Modifier.height(12.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.Top,
        ) {
            CleansiaCheckbox(
                checked = acceptedTerms,
                onCheckedChange = { acceptedTerms = it },
                label = stringResource(R.string.register_terms_and_conditions),
            )
        }

        Spacer(Modifier.height(16.dp))

        CleansiaPrimaryButton(
            text = stringResource(R.string.register_submit),
            onClick = {
                onRegisterClick(
                    firstName,
                    lastName,
                    email,
                    password,
                    referralCode.trim().ifBlank { null },
                )
            },
            loading = loading,
            enabled = formValid,
        )

        Spacer(Modifier.height(8.dp))

        LabelledDivider(label = stringResource(R.string.login_or))

        Spacer(Modifier.height(8.dp))

        CleansiaOutlinedButton(
            text = stringResource(R.string.login_continue_with_google),
            onClick = onGoogleSignIn,
            leadingIcon = Icons.Outlined.Mail,
            enabled = !loading,
        )

        Spacer(Modifier.height(24.dp))

        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = stringResource(R.string.register_have_account),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            CleansiaTextLink(
                text = stringResource(R.string.register_login_link),
                onClick = onLoginClick,
            )
        }
    }

    // Reuse the same booking-feature dialog. The validate lambda calls the repo
    // directly; we own the Apply-success persistence here so onApplied flips
    // both the canonical code + validated flag.
    if (referralSheetOpen) {
        ReferralCodeBottomSheet(
            initialCode = referralCode,
            onDismiss = { referralSheetOpen = false },
            onValidate = { code ->
                val normalized = code.trim().uppercase()
                if (normalized.isBlank()) {
                    ReferralCodeUiState.Idle
                } else {
                    val resp = referralRepo.validate(normalized)
                    when {
                        resp == null -> ReferralCodeUiState.Invalid(null)
                        resp.isValid -> ReferralCodeUiState.Valid(resp.referrerFirstName)
                        else -> ReferralCodeUiState.Invalid(ReferralValidationError.fromString(resp.errorCode))
                    }
                }
            },
            onApplied = { validated, _ ->
                referralCode = validated
                referralValidated = true
            },
        )
    }
}

/**
 * Wolt-style entry row for the signup referral dialog. Same layout shape as
 * the booking-step row; kept here as a private composable to avoid pulling
 * the booking row helper into a public surface.
 */
@Composable
private fun ReferralCodeRow(
    appliedCode: String,
    onClick: () -> Unit,
    onClear: () -> Unit,
) {
    val hasApplied = appliedCode.isNotBlank()
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(36.dp)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.15f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.CardGiftcard,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(
                stringResource(R.string.booking_referral_code_row_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (hasApplied) {
                Text(
                    stringResource(R.string.booking_referral_code_row_applied, appliedCode.trim().uppercase()),
                    style = MaterialTheme.typography.bodySmall,
                    color = SuccessText,
                )
            }
        }
        if (hasApplied) {
            IconButton(onClick = onClear) {
                Icon(
                    imageVector = Icons.Outlined.Close,
                    contentDescription = stringResource(R.string.booking_referral_code_row_clear),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
        } else {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}

@Preview(widthDp = 390, heightDp = 900)
@Composable
private fun SignUpPreview() {
    CleansiaTheme { SignUpScreen() }
}

@Preview(widthDp = 390, heightDp = 900, uiMode = android.content.res.Configuration.UI_MODE_NIGHT_YES)
@Composable
private fun SignUpPreviewDark() {
    CleansiaTheme(darkTheme = true) { SignUpScreen() }
}
