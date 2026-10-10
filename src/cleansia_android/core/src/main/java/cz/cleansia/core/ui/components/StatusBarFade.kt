package cz.cleansia.core.ui.components

import android.annotation.SuppressLint
import android.app.Activity
import android.graphics.RectF
import android.os.Build
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.lazy.LazyListState
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
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import androidx.lifecycle.compose.LifecycleResumeEffect
import kotlin.math.ceil
import kotlin.math.roundToInt

/**
 * Fades scrolled content out under the status bar on a screen that draws edge to edge with no top
 * bar (Home, Profile, Subscribe Plus), as iOS's `StatusBarFadeScrollView` does. Place it directly
 * before `verticalScroll(scrollState)` so it draws over the viewport, not over the scrolled content.
 *
 * The fade is one solid colour, the colour actually behind the status bar: the page background, or —
 * on a screen whose scroll content starts with a full-bleed hero ([heroTint], its top colour, and
 * [heroHeight], its measured height in px) — the hero's colour while the hero is under the status
 * bar, cross-faded into the page background as the hero's bottom passes up through it, so a dark hero
 * never wears a pale band. The cross-fade steps over the shades on which neither the light nor the dark
 * icons read 4.5:1 ([statusBarFadeLegibleShare]), so the clock stays legible through the whole scroll.
 * It is held at 90 % behind the clock and icons and eased out to clear by the clock's line
 * ([statusBarFadeHeight]), with no tail below it. While a hero is on the screen the status bar's icons
 * are set light or dark to read on that colour ([statusBarIconsLight]).
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
    val illegible = remember(heroTint, page) { heroTint?.let { statusBarFadeIllegibleShares(page, it) } }
    val behind = remember(scrollState, heroTint, page, height, illegible) {
        derivedStateOf {
            statusBarFadeColor(
                page = page,
                heroTint = heroTint,
                heroShare = statusBarFadeLegibleShare(
                    statusBarFadeHeroShare(currentHeroHeight() - scrollState.value, height),
                    illegible,
                ),
            )
        }
    }
    if (heroTint != null) {
        val light by remember(behind) { derivedStateOf { statusBarIconsLight(behind.value) } }
        StatusBarIcons(light)
    }

    drawWithContent {
        drawContent()
        drawStatusBarFade(alpha, height, ease, behind.value)
    }
}

/**
 * The same fade over a lazy list with no hero, such as the partner dashboard: the page colour, shown once
 * the list has left its top. Chain it on the list itself, so it draws over the viewport.
 */
fun Modifier.statusBarFade(listState: LazyListState): Modifier = composed {
    val scrolled by remember(listState) { derivedStateOf { statusBarFadeScrolled(listState) } }
    val alpha by animateFloatAsState(if (scrolled) 1f else 0f, label = "statusBarFade")
    val height = statusBarFadeHeight(WindowInsets.statusBars.getTop(LocalDensity.current), cutoutExtent())
    val ease = with(LocalDensity.current) { FadeEase.toPx() }
    val page = MaterialTheme.colorScheme.background
    drawWithContent {
        drawContent()
        drawStatusBarFade(alpha, height, ease, page)
    }
}

/** Whether [listState] has left its top: past its first item, or scrolled into it. */
internal fun statusBarFadeScrolled(listState: LazyListState): Boolean =
    listState.firstVisibleItemIndex > 0 || listState.firstVisibleItemScrollOffset > 0

private fun DrawScope.drawStatusBarFade(alpha: Float, height: Int, ease: Float, color: Color) {
    if (alpha > 0f && height > 0) {
        val bottom = height.toFloat()
        drawRect(
            brush = Brush.verticalGradient(*statusBarFadeStops(bottom, ease, color), endY = bottom),
            size = Size(size.width, bottom),
            alpha = alpha,
        )
    }
}

/** How far above the fade's bottom the 90 % hold starts easing out to clear: iOS's `StatusBarFade.falloff`. */
internal val FadeEase = 5.dp

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

/** The display's cutout, top to bottom, read for the API level the device runs ([cutoutExtentFor]). */
@SuppressLint("NewApi") // cutoutExtentFor reads each only on the API level that has it.
@Composable
private fun cutoutExtent(): ClosedFloatingPointRange<Float>? {
    val insets = LocalView.current.rootWindowInsets
    return cutoutExtentFor(
        sdk = Build.VERSION.SDK_INT,
        pathBounds = {
            insets?.displayCutout?.cutoutPath
                ?.let { path -> RectF().also { path.computeBounds(it, true) } }
                ?.let { it.top..it.bottom }
        },
        boundingRects = { insets?.displayCutout?.boundingRects?.map { it.top.toFloat()..it.bottom.toFloat() } },
    )
}

/**
 * The top and bottom of the display's cutout; null without one. From API 31 it is the cutout's own path,
 * the camera hole itself rather than the rectangle the status bar is sized by. On API 28–30, which have
 * no path, it is the span of the cutout's bounding rectangles, so a phone there with a camera in its top
 * edge ends the fade on the hole's line too rather than below the clock. Below 28 there is no cutout API.
 */
internal fun cutoutExtentFor(
    sdk: Int,
    pathBounds: () -> ClosedFloatingPointRange<Float>?,
    boundingRects: () -> List<ClosedFloatingPointRange<Float>>?,
): ClosedFloatingPointRange<Float>? = when {
    sdk >= Build.VERSION_CODES.S -> pathBounds()
    sdk >= Build.VERSION_CODES.P ->
        boundingRects()?.takeIf { it.isNotEmpty() }?.let { rects -> rects.minOf { it.start }..rects.maxOf { it.endInclusive } }
    else -> null
}

/**
 * How much of the fade wears the hero's colour, from the hero's bottom edge measured from the top of
 * the screen: all of it while the hero still reaches the fade's bottom ([height]), none once it has
 * passed above the screen's top, and in proportion between. This is the raw share; the drawn one steps
 * over the shades the status-bar icons cannot be read on ([statusBarFadeLegibleShare]).
 */
internal fun statusBarFadeHeroShare(heroBottom: Int, height: Int): Float =
    (heroBottom.toFloat() / height.coerceAtLeast(1)).coerceIn(0f, 1f)

/** The hero's colour laid over the page in [heroShare], as iOS lays it; the page alone without a hero. */
internal fun statusBarFadeColor(page: Color, heroTint: Color?, heroShare: Float): Color =
    heroTint?.copy(alpha = heroShare)?.compositeOver(page) ?: page

/** The steps a hero's share is judged in: an sRGB colour keeps its alpha in eight bits. */
private const val SHARE_STEPS = 255

/**
 * The hero shares whose fade colour neither the light nor the dark icons read 4.5:1 on
 * ([statusBarClockReads]), from the first to the last; null when every share reads, as on a dark page.
 * A dark hero cross-faded into a light page passes through them: the clock dipped to 3.1:1 on Profile.
 */
internal fun statusBarFadeIllegibleShares(page: Color, heroTint: Color): ClosedFloatingPointRange<Float>? {
    val illegible = (0..SHARE_STEPS).map { it.toFloat() / SHARE_STEPS }.filterNot { share ->
        val color = statusBarFadeColor(page, heroTint, share)
        statusBarClockReads(color, light = true, heroTint) || statusBarClockReads(color, light = false, heroTint)
    }
    return if (illegible.isEmpty()) null else illegible.first()..illegible.last()
}

/**
 * [heroShare] on the eight-bit step the colour will hold, moved out of [illegible] to the legible step
 * past its nearer edge: the colour jumps across those shades in one pixel of scroll rather than passing
 * through them, and the icons flip with it ([statusBarIconsLight]). Every other share is left as it is.
 */
internal fun statusBarFadeLegibleShare(heroShare: Float, illegible: ClosedFloatingPointRange<Float>?): Float {
    val share = (heroShare * SHARE_STEPS).roundToInt().toFloat() / SHARE_STEPS
    if (illegible == null || share !in illegible) return share
    val step = 1f / SHARE_STEPS
    val darker = share - illegible.start >= illegible.endInclusive - share
    return (if (darker) illegible.endInclusive + step else illegible.start - step).coerceIn(0f, 1f)
}

/**
 * Whether the clock reads 4.5:1 in [light] icons over the fade in [color], whatever shows through the
 * fade's last 10 %: white under the light icons, the hero itself ([heroTint]) under the dark ones.
 */
internal fun statusBarClockReads(color: Color, light: Boolean, heroTint: Color): Boolean {
    val behind = color.copy(alpha = STATUS_BAR_FADE_OPACITY).compositeOver(if (light) Color.White else heroTint)
    val icons = if (light) Color.White else DarkStatusBarIcons.compositeOver(behind)
    val (lighter, darker) = listOf(icons.luminance(), behind.luminance()).sortedDescending()
    return (lighter + 0.05f) / (darker + 0.05f) >= 4.5f
}

/** The system's dark status-bar icons: black at 60 %. */
private val DarkStatusBarIcons = Color.Black.copy(alpha = 0.6f)

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
    val themeDark = MaterialTheme.colorScheme.surface.luminance() < 0.5f
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
