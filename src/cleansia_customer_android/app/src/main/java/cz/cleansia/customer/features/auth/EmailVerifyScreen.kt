package cz.cleansia.customer.features.auth

import androidx.compose.foundation.Image
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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
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
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.components.CleansiaOutlinedButton
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.Poppins

private const val CODE_LENGTH = 6

/**
 * Email Verify — mirrors the web's [`confirm-email.component.html`].
 * 6-digit OTP code with auto-submit on completion, resend + verify buttons.
 */
@Composable
fun EmailVerifyScreen(
    email: String? = null,
    onVerify: (code: String) -> Unit = {},
    onResend: (email: String) -> Unit = {},
    onBack: () -> Unit = {},
    loading: Boolean = false,
) {
    var code by remember { mutableStateOf("") }

    LaunchedEffect(code) {
        if (code.length == CODE_LENGTH) onVerify(code)
    }

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
            Image(
                painter = painterResource(R.drawable.mascot_waving),
                contentDescription = null,
                modifier = Modifier.size(140.dp),
            )

            Spacer(Modifier.height(24.dp))

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

            Spacer(Modifier.height(24.dp))

            CleansiaPrimaryButton(
                text = stringResource(R.string.verify_submit),
                onClick = { onVerify(code) },
                loading = loading,
                enabled = code.length == CODE_LENGTH,
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

@Composable
private fun CodeInput(code: String, onCodeChange: (String) -> Unit) {
    val focusRequester = remember { FocusRequester() }

    Box(modifier = Modifier.fillMaxWidth()) {
        BasicTextField(
            value = code,
            onValueChange = onCodeChange,
            modifier = Modifier
                .focusRequester(focusRequester)
                .size(1.dp),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            textStyle = TextStyle(color = androidx.compose.ui.graphics.Color.Transparent),
        )

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp, Alignment.CenterHorizontally),
        ) {
            repeat(CODE_LENGTH) { index ->
                val char = code.getOrNull(index)?.toString() ?: ""
                val focused = index == code.length
                val borderColor = if (focused) MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.outline
                Box(
                    modifier = Modifier
                        .size(width = 44.dp, height = 56.dp)
                        .border(
                            width = if (focused) 2.dp else 1.dp,
                            color = borderColor,
                            shape = RoundedCornerShape(12.dp),
                        ),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = char,
                        style = MaterialTheme.typography.headlineMedium.copy(
                            fontFamily = Poppins,
                            fontSize = 24.sp,
                        ),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }
    }

    LaunchedEffect(Unit) { focusRequester.requestFocus() }
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
