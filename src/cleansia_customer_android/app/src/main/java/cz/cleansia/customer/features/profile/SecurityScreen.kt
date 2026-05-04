package cz.cleansia.customer.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
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
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.components.CleansiaTextField
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.Poppins

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SecurityScreen(
    onBack: () -> Unit = {},
    onChangePassword: () -> Unit = {},
) {
    var currentPassword by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }

    val valid = currentPassword.isNotBlank() && newPassword.length >= 12 && newPassword == confirmPassword

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

        Column(modifier = Modifier.padding(20.dp)) {
            Text(stringResource(R.string.profile_security_change_password), style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold), color = MaterialTheme.colorScheme.onBackground)
            Spacer(Modifier.height(12.dp))
            CleansiaTextField(value = currentPassword, onValueChange = { currentPassword = it }, label = stringResource(R.string.profile_security_current), isPassword = true)
            Spacer(Modifier.height(8.dp))
            CleansiaTextField(value = newPassword, onValueChange = { newPassword = it }, label = stringResource(R.string.profile_security_new), isPassword = true)
            Spacer(Modifier.height(8.dp))
            CleansiaTextField(value = confirmPassword, onValueChange = { confirmPassword = it }, label = stringResource(R.string.profile_security_confirm), isPassword = true)
            Spacer(Modifier.height(24.dp))
            CleansiaPrimaryButton(text = stringResource(R.string.profile_security_update), onClick = onChangePassword, enabled = valid)
        }
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun SecurityPreview() {
    CleansiaTheme { SecurityScreen() }
}
