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
    // Poppins display ramp: 32/40/-0.5 -> 28/36/-0.4 -> [26/34/-0.35] -> headlineLarge 24/32/-0.3.
    // Sizes step -4,-2,-2; line heights -4,-2,-2; tracking -0.1,-0.05,-0.05 — sits exactly on the
    // curve. Note this is 10sp SMALLER than M3's un-rescaled 36sp baseline, which is why the two
    // avatar-initial call sites pin an explicit fontSize instead of riding the slot.
    displaySmall = TextStyle(
        fontFamily = Poppins, fontWeight = FontWeight.Bold,
        fontSize = 26.sp, lineHeight = 34.sp, letterSpacing = (-0.35).sp,
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
    // titleLarge 16/22 -> titleMedium 15/22 -> [14/20]. Metrics are identical to the M3 default it
    // replaces, so all 59 call sites change typeface only — zero reflow. It collides with
    // labelLarge (also 14/20 Nunito Bold); M3's own baseline does the same thing (titleSmall and
    // labelLarge are both 14/20 Medium 0.1), so the collision is authentic, not an oversight.
    titleSmall = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 14.sp, lineHeight = 20.sp, letterSpacing = 0.sp,
    ),
    bodyLarge = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Normal,
        fontSize = 16.sp, lineHeight = 24.sp,
    ),
    bodyMedium = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Normal,
        fontSize = 14.sp, lineHeight = 20.sp,
    ),
    // bodyLarge 16/24 -> bodyMedium 14/20 -> [12/16], the ramp's own -2sp/-4sp cadence, and again
    // identical to the M3 default metrics, so all 120 call sites see zero reflow.
    // letterSpacing 0 — not M3's 0.4, not labelSmall's 0.6: every Nunito BODY slot in this ramp
    // runs 0 tracking, and 0.6 is an all-caps eyebrow value that looks wrong on sentence-case copy.
    bodySmall = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Normal,
        fontSize = 12.sp, lineHeight = 16.sp, letterSpacing = 0.sp,
    ),
    labelLarge = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 14.sp, lineHeight = 20.sp,
    ),
    // labelLarge 14/20/0.0 -> [13/18/0.3] -> labelSmall 12/16/0.6 — every axis is the exact
    // midpoint. This is the ONLY one of the four new slots whose metrics differ from the M3
    // default it replaces (12/16 -> 13/18), so it is the only one that reflows.
    labelMedium = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 13.sp, lineHeight = 18.sp, letterSpacing = 0.3.sp,
    ),
    labelSmall = TextStyle(
        fontFamily = Nunito, fontWeight = FontWeight.Bold,
        fontSize = 12.sp, lineHeight = 16.sp, letterSpacing = 0.6.sp,
    ),
)
