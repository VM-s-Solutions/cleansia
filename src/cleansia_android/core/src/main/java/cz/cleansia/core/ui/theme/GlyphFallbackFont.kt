package cz.cleansia.core.ui.theme

import android.content.Context
import android.graphics.Typeface
import android.os.Build
import androidx.annotation.FontRes
import androidx.annotation.RequiresApi
import androidx.compose.ui.text.font.AndroidFont
import androidx.compose.ui.text.font.FontLoadingStrategy
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontVariation
import androidx.compose.ui.text.font.FontWeight
import androidx.core.content.res.ResourcesCompat
import java.io.IOException
import android.graphics.fonts.Font as PlatformFont
import android.graphics.fonts.FontFamily as PlatformFontFamily
import android.graphics.fonts.FontStyle as PlatformFontStyle

/**
 * A bundled face that hands the glyphs it cannot draw to a second bundled face, per glyph.
 *
 * Listing both faces as `Font` entries of one Compose [androidx.compose.ui.text.font.FontFamily]
 * does not do this and is the trap worth naming: `FontMatcher` picks entries by weight and style
 * only, never by coverage, and the winner becomes the single `android.graphics.Typeface` for the
 * whole run — so the second face is either ignored or takes over every character, including Latin.
 * Per-glyph substitution lives one level down, in the platform typeface's own family chain, which
 * `Typeface.CustomFallbackBuilder` is the only app-level way to write.
 *
 * That builder is API 29+. Below it the primary face loads exactly as a plain resource font does
 * and uncovered glyphs still come from the system face — legible, off-brand, unchanged from before.
 */
class GlyphFallbackFont(
    @FontRes val resId: Int,
    @FontRes val fallbackResId: Int,
    override val weight: FontWeight,
    override val style: FontStyle = FontStyle.Normal,
) : AndroidFont(FontLoadingStrategy.Blocking, GlyphFallbackTypefaceLoader, FontVariation.Settings()) {

    override fun equals(other: Any?): Boolean =
        other is GlyphFallbackFont &&
            resId == other.resId &&
            fallbackResId == other.fallbackResId &&
            weight == other.weight &&
            style == other.style

    override fun hashCode(): Int {
        var result = resId
        result = 31 * result + fallbackResId
        result = 31 * result + weight.hashCode()
        result = 31 * result + style.hashCode()
        return result
    }

    override fun toString(): String =
        "GlyphFallbackFont(resId=$resId, fallbackResId=$fallbackResId, weight=$weight, style=$style)"
}

private object GlyphFallbackTypefaceLoader : AndroidFont.TypefaceLoader {

    override fun loadBlocking(context: Context, font: AndroidFont): Typeface? {
        val glyphFallback = font as GlyphFallbackFont
        val chained = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            chainedTypeface(context, glyphFallback)
        } else {
            null
        }
        return chained ?: ResourcesCompat.getFont(context, glyphFallback.resId)
    }

    override suspend fun awaitLoad(context: Context, font: AndroidFont): Typeface? =
        loadBlocking(context, font)

    @RequiresApi(Build.VERSION_CODES.Q)
    private fun chainedTypeface(context: Context, font: GlyphFallbackFont): Typeface? =
        try {
            Typeface.CustomFallbackBuilder(platformFamily(context, font.resId))
                .addCustomFallback(platformFamily(context, font.fallbackResId))
                .setStyle(PlatformFontStyle(font.weight.weight, PlatformFontStyle.FONT_SLANT_UPRIGHT))
                .build()
        } catch (unreadable: IOException) {
            null
        }

    @RequiresApi(Build.VERSION_CODES.Q)
    private fun platformFamily(context: Context, @FontRes resId: Int): PlatformFontFamily =
        PlatformFontFamily.Builder(PlatformFont.Builder(context.resources, resId).build()).build()
}
