package cz.cleansia.customer.features.splash

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import cz.cleansia.core.ui.components.WordmarkSplash
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.theme.CleansiaTheme
import kotlinx.coroutines.delay

private const val BRAND_HOLD_MILLIS = 1800L

@Composable
fun SplashScreen(onContinue: () -> Unit) {
    LaunchedEffect(Unit) {
        delay(BRAND_HOLD_MILLIS)
        onContinue()
    }

    WordmarkSplash(tagline = stringResource(R.string.splash_tagline))
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun SplashPreview() {
    CleansiaTheme { SplashScreen(onContinue = {}) }
}
