package cz.cleansia.core.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import cz.cleansia.core.R

/*
 * The six TTFs in res/font are the exact same binaries the iOS apps ship
 * (src/cleansia_ios/CleansiaCustomer/Resources/Fonts, byte-identical), so Android and iOS render
 * the brand metrically identically.
 *
 * They are bundled rather than fetched through the Play-Services downloadable-font provider on
 * purpose: that provider resolves only on devices with Google Play Services, so on a Huawei,
 * a de-Googled ROM or a grey-market handset it never resolved at all and EVERY string in both apps
 * fell back to Roboto. It also failed on a cold first launch without network. Bundling costs
 * ~868 KB per APK and removes both failure modes.
 *
 * Note there is deliberately no Nunito Medium(500) in the bundle — same as the provider family
 * before it. Compose's CSS-style weight matcher resolves a Medium request on Nunito down to
 * Regular(400); that is pre-existing behaviour, not a regression introduced here.
 */

// Poppins — headings (matches web app)
val Poppins = FontFamily(
    Font(R.font.poppins_medium, FontWeight.Medium),
    Font(R.font.poppins_semibold, FontWeight.SemiBold),
    Font(R.font.poppins_bold, FontWeight.Bold),
)

// Nunito — body (matches web app)
val Nunito = FontFamily(
    Font(R.font.nunito_regular, FontWeight.Normal),
    Font(R.font.nunito_semibold, FontWeight.SemiBold),
    Font(R.font.nunito_bold, FontWeight.Bold),
)

val CleansiaTypography = Typography(
    displayLarge = TextStyle(
        fontFamily = Poppins, fontWeight = FontWeight.Bold,
        fontSize = 32.sp, lineHeight = 40.sp, letterSpacing = (-0.5).sp,
    ),
    displayMedium = TextStyle(
        fontFamily = Poppins, fontWeight = FontWeight.Bold,
        fontSize = 28.sp, lineHeight = 36.sp, letterSpacing = (-0.4).sp,
    ),
    headlineLarge = TextStyle(
        fontFamily = Poppins, fontWeight = FontWeight.SemiBold,
        fontSize = 24.sp, lineHeight = 32.sp, letterSpacing = (-0.3).sp,
    ),
    headlineMedium = TextStyle(
        fontFamily = Poppins, fontWeight = FontWeight.SemiBold,
        fontSize = 22.sp, lineHeight = 28.sp, letterSpacing = (-0.2).sp,
    ),
    headlineSmall = TextStyle(
        fontFamily = Poppins, fontWeight = FontWeight.SemiBold,
        fontSize = 18.sp, lineHeight = 24.sp,
    ),
    titleLarge = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 16.sp, lineHeight = 22.sp,
    ),
    titleMedium = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 15.sp, lineHeight = 22.sp,
    ),
    bodyLarge = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Normal,
        fontSize = 16.sp, lineHeight = 24.sp,
    ),
    bodyMedium = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Normal,
        fontSize = 14.sp, lineHeight = 20.sp,
    ),
    labelLarge = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 14.sp, lineHeight = 20.sp,
    ),
    labelSmall = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 12.sp, lineHeight = 16.sp, letterSpacing = 0.6.sp,
    ),
)
