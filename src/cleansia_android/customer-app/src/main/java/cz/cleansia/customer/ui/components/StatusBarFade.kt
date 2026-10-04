package cz.cleansia.customer.ui.components

import android.app.Activity
import android.graphics.RectF
import android.os.Build
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.statusBars
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import androidx.lifecycle.compose.LifecycleResumeEffect
import cz.cleansia.customer.ui.theme.isDark
import kotlin.math.ceil

/**
 * Fades scrolled content out under the status bar on a screen that draws edge to edge with no top
 * bar (Home, Profile, Subscribe Plus), as iOS's `StatusBarFadeScrollView` does. Place it directly
 * before `verticalScroll(scrollState)` so it draws over the viewport, not over the scrolled content.
 *
 * The fade is one solid colour, the colour actually behind the status bar: the page background, or —
 * on a screen whose scroll content starts with a full-bleed hero ([heroTint], its top colour, and
 * [heroHeight], its measured height in px) — the hero's colour while the hero is under the status
 * bar, cross-faded into the page background as the hero's bottom passes up through it, so a dark hero
 * never wears a pale band. It is held at 90 % behind the clock and icons and eased out to clear by the
 * clock's line ([statusBarFadeHeight]), with no tail below it. While a hero is on the screen the status
 * bar's icons are set light or dark to read on that colour ([statusBarIconsLight]).
 *
 * It shows only once the content has left its top. At rest the full-bleed Profile and Plus heroes
 * paint the status-bar strip themselves, and a pull-to-refresh never moves the scroll value. Drawing
 * only, so it takes no touches and adds nothing to the semantics tree.
 *
 * → /mobile-app/patterns#status-bar-fade
 */
fun Modifier.statusBarFade(
    scrollState: ScrollState,
    heroTint: Color? = null,
    heroHeight: () -> Int = { 0 },
): Modifier = composed {
    val scrolled by remember(scrollState) { derivedStateOf { scrollState.value > 0 } }
    val alpha by animateFloatAsState(if (scrolled) 1f else 0f, label = "statusBarFade")
    val statusBar = WindowInsets.statusBars.getTop(LocalDensity.current)
    val height = statusBarFadeHeight(statusBar, cutoutExtent())
    val ease = with(LocalDensity.current) { FadeEase.toPx() }
    val page = MaterialTheme.colorScheme.background
    val currentHeroHeight by rememberUpdatedState(heroHeight)
    val behind = remember(scrollState, heroTint, page, height) {
        derivedStateOf {
            statusBarFadeColor(
                page = page,
                heroTint = heroTint,
                heroShare = statusBarFadeHeroShare(currentHeroHeight() - scrollState.value, height),
            )
        }
    }
    if (heroTint != null) {
        val light by remember(behind) { derivedStateOf { statusBarIconsLight(behind.value) } }
        StatusBarIcons(light)
    }

    drawWithContent {
        drawContent()
        if (alpha > 0f && height > 0) {
            val bottom = height.toFloat()
            drawRect(
                brush = Brush.verticalGradient(*statusBarFadeStops(bottom, ease, behind.value), endY = bottom),
                size = Size(size.width, bottom),
                alpha = alpha,
            )
        }
    }
}

/** How far above the fade's bottom the 90 % hold starts easing out to clear. */
private val FadeEase = 6.dp

/** The colour's strength behind the clock and icons, the same as iOS's. */
internal const val STATUS_BAR_FADE_OPACITY = 0.9f

/**
 * Where the fade ends, in px from the top of the screen: the bottom of the camera hole or notch at the
 * top of the screen, the line the system centres the clock and icons on, or the status bar's own bottom
 * on a screen with none. The status bar runs well past the hole (132 px against 102 on a Pixel 8), and
 * a fade to it read as a band under the clock. A cutout that is not inside the status bar (one on the
 * side in landscape) is not the clock's line.
 */
internal fun statusBarFadeHeight(statusBar: Int, cutout: ClosedFloatingPointRange<Float>?): Int =
    if (cutout != null && cutout.start < statusBar && cutout.endInclusive > 0f && cutout.endInclusive <= statusBar) {
        ceil(cutout.endInclusive).toInt()
    } else {
        statusBar
    }

/**
 * The top and bottom of the display's cutout path, from API 31, which is the camera hole itself rather
 * than the rectangle the status bar is sized by; null without one.
 */
@Composable
private fun cutoutExtent(): ClosedFloatingPointRange<Float>? {
    if (Build.VERSION.SDK_INT < Build.VERSION_CODES.S) return null
    val path = LocalView.current.rootWindowInsets?.displayCutout?.cutoutPath ?: return null
    val bounds = RectF().also { path.computeBounds(it, true) }
    return bounds.top..bounds.bottom
}

/**
 * How much of the fade wears the hero's colour, from the hero's bottom edge measured from the top of
 * the screen: all of it while the hero still reaches the fade's bottom ([height]), none once it has
 * passed above the screen's top, and in proportion between, so the page colour takes over with no jump.
 */
internal fun statusBarFadeHeroShare(heroBottom: Int, height: Int): Float =
    (heroBottom.toFloat() / height.coerceAtLeast(1)).coerceIn(0f, 1f)

/** The hero's colour laid over the page in [heroShare], as iOS lays it; the page alone without a hero. */
internal fun statusBarFadeColor(page: Color, heroTint: Color?, heroShare: Float): Color =
    heroTint?.copy(alpha = heroShare)?.compositeOver(page) ?: page

/**
 * The fade's stops over its [height]: [color] held at 90 % down to [ease] above the
 * bottom, then a smoothstep falloff to clear at the bottom itself, sampled at eight points so the eased
 * curve shows no seam. Every stop is [color] at some alpha, never Color.Transparent (transparent
 * black): the gradient interpolates unpremultiplied, so a fade to black greys the light theme.
 */
internal fun statusBarFadeStops(height: Float, ease: Float, color: Color): Array<Pair<Float, Color>> {
    val hold = ((height - ease) / height.coerceAtLeast(1f)).coerceIn(0f, 1f)
    val samples = 8
    val falloff = (0..samples).map { index ->
        val progress = index.toFloat() / samples
        val eased = progress * progress * (3 - 2 * progress)
        (hold + (1 - hold) * progress) to color.copy(alpha = STATUS_BAR_FADE_OPACITY * (1 - eased))
    }
    return (listOf(0f to color.copy(alpha = STATUS_BAR_FADE_OPACITY)) + falloff).toTypedArray()
}

/**
 * Whether the status bar's icons should be light over [color]. A light theme draws them in 60 %
 * black, which reads better than white only on a colour lighter than a relative luminance of about
 * 0.25 (sky-600 measures 0.21: white reads 4.1:1 on it, the dark icons 3.2:1).
 */
internal fun statusBarIconsLight(color: Color): Boolean = color.luminance() < 0.25f

/**
 * Holds the status bar's icons [light] or dark while this screen is resumed, and hands them back to
 * the theme's (dark on a light theme) when it pauses or leaves. The newest screen to set them owns
 * them: Plus opens over Profile and Profile leaves composition only once Plus is in, so a screen
 * hands them back only while it is still the one that set them.
 */
@Composable
private fun StatusBarIcons(light: Boolean) {
    val view = LocalView.current
    val window = (view.context as? Activity)?.window ?: return
    val themeDark = isDark()
    val owner = remember { Any() }
    fun set() {
        WindowCompat.getInsetsController(window, view).isAppearanceLightStatusBars = !light
        iconsOwner = owner
    }
    fun release() {
        if (iconsOwner !== owner) return
        WindowCompat.getInsetsController(window, view).isAppearanceLightStatusBars = !themeDark
        iconsOwner = null
    }
    // Set on entering composition too, not only on resume: a screen coming back into view composes
    // when its transition starts but resumes only when it ends.
    DisposableEffect(light, themeDark) {
        set()
        onDispose { release() }
    }
    LifecycleResumeEffect(light, themeDark) {
        set()
        onPauseOrDispose { release() }
    }
}

/** The screen whose [StatusBarIcons] last set the icons; main thread only. */
private var iconsOwner: Any? = null
