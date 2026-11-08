package cz.cleansia.core.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.defaultMinSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.takeOrElse
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.LastBaseline
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.ui.theme.NunitoCapHeight
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.capBandLine
import cz.cleansia.core.ui.theme.disabledContainer
import cz.cleansia.core.ui.theme.onDisabledContainer
import cz.cleansia.core.ui.theme.primaryText
import kotlin.math.ceil

enum class CleansiaButtonSize(
    val minHeight: androidx.compose.ui.unit.Dp,
    val horizontalPadding: androidx.compose.ui.unit.Dp,
) {
    Small(40.dp, 16.dp),
    Medium(48.dp, 24.dp),
    Large(56.dp, 32.dp),
}

/**
 * Solid primary CTA — sky-600 fill, white text, pill shape. A disabled one takes the caller's [disabledContainerColor]
 * and [disabledContentColor]; left out, it draws grey with a dark label.
 */
@Composable
fun CleansiaPrimaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: CleansiaButtonSize = CleansiaButtonSize.Large,
    leadingIcon: ImageVector? = null,
    trailingIcon: ImageVector? = null,
    loading: Boolean = false,
    enabled: Boolean = true,
    disabledContainerColor: Color = Color.Unspecified,
    disabledContentColor: Color = Color.Unspecified,
) {
    val scheme = MaterialTheme.colorScheme
    val offFill = disabledContainerColor.takeOrElse { scheme.disabledContainer }
    val offInk = disabledContentColor.takeOrElse { scheme.onDisabledContainer }
    Button(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .defaultMinSize(minHeight = size.minHeight),
        enabled = enabled && !loading,
        shape = CircleShape,
        contentPadding = PaddingValues(horizontal = size.horizontalPadding, vertical = 12.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = MaterialTheme.colorScheme.primary,
            contentColor = MaterialTheme.colorScheme.onPrimary,
            disabledContainerColor = offFill,
            disabledContentColor = offInk,
        ),
    ) {
        if (loading) {
            CircularProgressIndicator(
                modifier = Modifier.size(20.dp),
                color = offInk,
                strokeWidth = 2.dp,
            )
        } else {
            if (leadingIcon != null) {
                Icon(leadingIcon, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(8.dp))
            }
            Text(
                text = text,
                style = MaterialTheme.typography.titleMedium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            if (trailingIcon != null) {
                Spacer(Modifier.width(8.dp))
                Icon(trailingIcon, contentDescription = null, modifier = Modifier.size(18.dp))
            }
        }
    }
}

/**
 * Irreversible-action CTA (delete account, permanently discard, …).
 *
 * Uses a FIXED red container in both themes, deliberately NOT
 * `colorScheme.error`. This is a visual-hierarchy fix, not a contrast fix —
 * `error` / `onError` is the correct M3 pairing and clears WCAG in both
 * themes, so anyone auditing contrast here will (rightly) find nothing wrong.
 * The problem is rank: in dark mode `error` is red-300 (0xFFFCA5A5), so a
 * container painted with it becomes the highest-luminance element on a
 * Slate-900 page — brighter than the Sky400 primary — and the destroy-account
 * button ends up reading as the most inviting affordance on screen. Danger
 * must not out-rank the primary; it must read as danger.
 *
 * The light/dark pair is picked from `surface`'s luminance rather than
 * `isSystemInDarkTheme()` so it tracks the theme actually in force —
 * `CleansiaTheme(darkTheme = …)` accepts an explicit override, and a
 * system-setting read would desynchronise the moment anyone uses it.
 *
 * Signature mirrors [CleansiaPrimaryButton] exactly so the two are drop-in
 * siblings.
 */
@Composable
fun CleansiaDestructiveButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: CleansiaButtonSize = CleansiaButtonSize.Large,
    leadingIcon: ImageVector? = null,
    loading: Boolean = false,
    enabled: Boolean = true,
) {
    // red-600 on a light page, red-700 on a dark one: deep enough in dark mode
    // to sit BELOW the primary in the luminance order while still reading red.
    val container = if (MaterialTheme.colorScheme.surface.luminance() < 0.5f) {
        Color(0xFFB91C1C)
    } else {
        Color(0xFFDC2626)
    }
    Button(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .defaultMinSize(minHeight = size.minHeight),
        enabled = enabled && !loading,
        shape = CircleShape,
        contentPadding = PaddingValues(horizontal = size.horizontalPadding, vertical = 12.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = container,
            contentColor = Color.White,
            // 38% is the M3 disabled convention; applied to our own colours
            // because ButtonDefaults' disabled tokens are surface-derived and
            // would drop the red entirely.
            disabledContainerColor = container.copy(alpha = 0.38f),
            disabledContentColor = Color.White.copy(alpha = 0.38f),
        ),
    ) {
        if (loading) {
            CircularProgressIndicator(
                modifier = Modifier.size(20.dp),
                color = Color.White,
                strokeWidth = 2.dp,
            )
        } else {
            if (leadingIcon != null) {
                Icon(leadingIcon, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(8.dp))
            }
            Text(
                text = text,
                style = MaterialTheme.typography.titleMedium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

/**
 * Outlined button — used for "Continue with Google", secondary CTAs. The label and its leading icon share
 * one ink, onSurface, as on iOS; Material's default would draw the icon in the primary beside a dark label.
 * A secondary move in the brand or a destructive one passes its own [contentColor] and [borderColor]. A label
 * allowed [maxLines] above 1 wraps rather than cuts, its lines centred.
 */
@Composable
fun CleansiaOutlinedButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: CleansiaButtonSize = CleansiaButtonSize.Large,
    leadingIcon: ImageVector? = null,
    enabled: Boolean = true,
    contentColor: Color = MaterialTheme.colorScheme.onSurface,
    borderColor: Color = MaterialTheme.colorScheme.outline,
    maxLines: Int = 1,
) {
    OutlinedButton(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .defaultMinSize(minHeight = size.minHeight),
        enabled = enabled,
        shape = CircleShape,
        border = BorderStroke(1.dp, borderColor),
        colors = ButtonDefaults.outlinedButtonColors(contentColor = contentColor),
        contentPadding = PaddingValues(horizontal = size.horizontalPadding, vertical = 12.dp),
    ) {
        if (leadingIcon != null) {
            Icon(leadingIcon, contentDescription = null, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(12.dp))
        }
        Text(
            text = text,
            style = MaterialTheme.typography.titleMedium,
            color = contentColor,
            maxLines = maxLines,
            overflow = TextOverflow.Ellipsis,
            textAlign = if (maxLines > 1) TextAlign.Center else null,
        )
    }
}

/**
 * Material's TextButton with its label — and any icon beside it — in the text blue, [primaryText],
 * rather than Material's default, the primary: sky-600 is 4.10:1 on white, and a dialog's surface or a
 * card's tint is lower still. The one text button both apps draw; a caller that needs another ink (a
 * destructive red) uses Material's TextButton with its own `colors`. The ripple follows the label.
 */
@Composable
fun CleansiaTextButton(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    contentPadding: PaddingValues = ButtonDefaults.TextButtonContentPadding,
    content: @Composable RowScope.() -> Unit,
) {
    TextButton(
        onClick = onClick,
        modifier = modifier,
        enabled = enabled,
        colors = ButtonDefaults.textButtonColors(contentColor = primaryText()),
        contentPadding = contentPadding,
        content = content,
    )
}

/**
 * Inline link — text-blue label, no chrome.
 *
 * Single-line and ellipsized like the other three variants. Most callers give a button the full width
 * so it never mattered, but a label in a `weight(1f)` slot wraps mid-word instead of truncating — cs
 * "Poznámka" is 76dp against a 74dp budget in the notes row. Truncating is the safe default for the
 * next constrained caller; the reference is the bottom nav, which has done this since it shipped.
 *
 * An icon takes the label's ink and size and sits on the cap band of its line, the visual centre of a capitalised
 * word: a one-line link centres it on the line, and a link allowed [maxLines] above 1 puts a leading icon on its first
 * line and a trailing one after the end of its last.
 */
@Composable
fun CleansiaTextLink(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    leadingIcon: ImageVector? = null,
    trailingIcon: ImageVector? = null,
    maxLines: Int = 1,
    enabled: Boolean = true,
) {
    CleansiaTextButton(
        onClick = onClick,
        modifier = modifier,
        enabled = enabled,
        contentPadding = PaddingValues(horizontal = 4.dp, vertical = 4.dp),
    ) {
        val style = MaterialTheme.typography.labelLarge
        if (leadingIcon == null && trailingIcon == null) {
            Text(
                text = text,
                style = style,
                maxLines = maxLines,
                overflow = TextOverflow.Ellipsis,
            )
        } else {
            when (linkGlyphPlacement(maxLines)) {
                LinkGlyphPlacement.LineCentre -> {
                    leadingIcon?.let {
                        LinkGlyph(it, Modifier)
                        Spacer(Modifier.width(Spacing.XXS))
                    }
                    Text(
                        text = text,
                        style = style,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false),
                    )
                    trailingIcon?.let {
                        Spacer(Modifier.width(Spacing.XXS))
                        LinkGlyph(it, Modifier)
                    }
                }
                LinkGlyphPlacement.CapBand -> Row(Modifier.weight(1f, fill = false)) {
                    leadingIcon?.let {
                        LinkGlyph(it, capBandAligned(style))
                        Spacer(Modifier.width(Spacing.XXS))
                    }
                    val label = Modifier.weight(1f, fill = false).alignBy(FirstBaseline)
                    if (trailingIcon == null) {
                        Text(text = text, style = style, maxLines = maxLines, overflow = TextOverflow.Ellipsis, modifier = label)
                    } else {
                        LabelThenGlyph(text, style, maxLines, trailingIcon, label)
                    }
                }
            }
        }
    }
}

internal enum class LinkGlyphPlacement { LineCentre, CapBand }

internal fun linkGlyphPlacement(maxLines: Int): LinkGlyphPlacement =
    if (maxLines > 1) LinkGlyphPlacement.CapBand else LinkGlyphPlacement.LineCentre

/**
 * A label that may wrap, its trailing glyph after the last line's end on that line's cap band, so it follows the last
 * word. The label is laid out as wide as its longest line, which breaks it as the full room does.
 */
@Composable
private fun LabelThenGlyph(text: String, style: TextStyle, maxLines: Int, icon: ImageVector, modifier: Modifier) {
    val measurer = rememberTextMeasurer()
    val capHeightPx = with(LocalDensity.current) { style.fontSize.toPx() } * NunitoCapHeight
    Layout(
        content = {
            Text(text = text, style = style, maxLines = maxLines, overflow = TextOverflow.Ellipsis)
            LinkGlyph(icon, Modifier)
        },
        modifier = modifier,
    ) { measurables, constraints ->
        val glyph = measurables[1].measure(Constraints())
        val gap = Spacing.XXS.roundToPx()
        val room = if (constraints.hasBoundedWidth) (constraints.maxWidth - gap - glyph.width).coerceAtLeast(0) else Constraints.Infinity
        val lines = measurer.measure(text, style, TextOverflow.Ellipsis, maxLines = maxLines, constraints = Constraints(maxWidth = room))
        val rights = List(lines.lineCount) { lines.getLineRight(it) }
        val label = measurables[0].measure(Constraints(maxWidth = (ceil(rights.maxOrNull() ?: 0f).toInt() + 1).coerceAtMost(room)))
        val (glyphX, drawn) = trailingGlyphPlacement(rights, gap, glyph.width)
        val width = maxOf(drawn, label.width)
        val baseline = label[LastBaseline]
        val glyphY = baseline - capBandLine(glyph.height, capHeightPx)
        val top = minOf(0, glyphY)
        val height = maxOf(label.height, glyphY + glyph.height) - top
        layout(
            width.coerceIn(constraints.minWidth, constraints.maxWidth),
            height.coerceIn(constraints.minHeight, constraints.maxHeight),
            mapOf(FirstBaseline to label[FirstBaseline] - top, LastBaseline to baseline - top),
        ) {
            label.place(0, -top)
            glyph.place(glyphX, glyphY - top)
        }
    }
}

/** A trailing glyph after a label that may wrap: [gapPx] past its last line's end; the label as wide as its longest line or the glyph's end. */
internal fun trailingGlyphPlacement(lineRightsPx: List<Float>, gapPx: Int, glyphWidthPx: Int): Pair<Int, Int> {
    val glyphX = ceil(lineRightsPx.lastOrNull() ?: 0f).toInt() + gapPx
    return glyphX to maxOf(ceil(lineRightsPx.maxOrNull() ?: 0f).toInt(), glyphX + glyphWidthPx)
}

/** Material's icon size beside a `labelLarge` button label, scaled with the text. */
private val LinkGlyphSize = 18.sp

@Composable
private fun LinkGlyph(icon: ImageVector, alignment: Modifier) {
    Icon(
        imageVector = icon,
        contentDescription = null,
        modifier = alignment.size(with(LocalDensity.current) { LinkGlyphSize.toDp() }),
    )
}
