package cz.cleansia.core.ui.components

import androidx.annotation.RawRes
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.painter.Painter
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.Dp
import androidx.core.content.pm.PackageInfoCompat
import coil3.compose.AsyncImage
import coil3.decode.BitmapFactoryDecoder
import coil3.gif.repeatCount
import coil3.request.CachePolicy
import coil3.request.ImageRequest

/**
 * Canvas size, in pixels, of every animated mascot in each app's `res/raw/`.
 *
 * Pinned as the decode size because otherwise Coil resolves it from the
 * layout — 140.dp is 420 px at 3x — and `ImageDecoder` then rescales all
 * 125 frames on the CPU to buy no extra detail the source doesn't have.
 * Decoding at native size lets the GPU do the upscale once per draw
 * instead, via [ContentScale.Fit].
 */
const val MASCOT_CANVAS_PX = 360

/**
 * Animated WebP mascot. Animated WebP keeps full alpha so the mascot floats over any background — no
 * compositing rectangle, no chroma-key artifacts, no video-player overhead. The files are each app's own.
 *
 * **Loop count is overridden at the request level rather than trusting the container's baked-in
 * metadata**, because Android's decoder is inconsistent about respecting it across vendors.
 *
 * [still] decodes the first frame alone, for a reader who turned animations off.
 */
@Composable
fun MascotAnimation(
    @RawRes resId: Int,
    size: Dp,
    modifier: Modifier = Modifier,
    contentDescription: String? = null,
    loop: Boolean = true,
    placeholder: Painter? = null,
    still: Boolean = false,
) {
    val context = LocalContext.current
    val request = remember(context, resId, loop, still) {
        val version = runCatching {
            PackageInfoCompat.getLongVersionCode(context.packageManager.getPackageInfo(context.packageName, 0))
        }.getOrDefault(0L)
        ImageRequest.Builder(context)
            .data(resId)
            .size(MASCOT_CANVAS_PX)
            .memoryCacheKey("mascot:$resId:$version:$loop:$still")
            .diskCachePolicy(CachePolicy.DISABLED)
            .repeatCount(if (loop) -1 else 0)
            .apply { if (still) decoderFactory(BitmapFactoryDecoder.Factory()) }
            .build()
    }

    AsyncImage(
        model = request,
        contentDescription = contentDescription,
        modifier = modifier.size(size),
        placeholder = placeholder,
        contentScale = ContentScale.Fit,
    )
}
