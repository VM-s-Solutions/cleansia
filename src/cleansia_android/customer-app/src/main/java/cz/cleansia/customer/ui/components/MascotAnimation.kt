package cz.cleansia.customer.ui.components

import androidx.annotation.RawRes
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.Dp
import coil3.compose.AsyncImage
import coil3.gif.repeatCount
import coil3.request.CachePolicy
import coil3.request.ImageRequest
import cz.cleansia.customer.BuildConfig

/**
 * Renders an animated WebP mascot from `res/raw/`.
 *
 * Backed by Coil 3 with the animated-image decoder registered in
 * [cz.cleansia.customer.CleansiaApp]. Animated WebP keeps full alpha so the
 * mascot floats over any background — no compositing rectangle, no chroma-key
 * artifacts, no ExoPlayer overhead.
 *
 * Loop count is overridden at the request level rather than relying on the
 * WebP container's baked-in loop metadata, because Android's `ImageDecoder`
 * is inconsistent about respecting it across vendors. With [loop] = false
 * the animation plays exactly once and freezes on the final frame.
 *
 * Cache keys are namespaced with the version code so a new build always
 * decodes fresh from the bundled raw resource. Without this, Coil's disk
 * cache holds the decoded animation across app updates and you'd see the
 * old asset until the user manually clears storage. Disk cache is also
 * disabled outright — we're loading from APK resources, not the network,
 * so disk caching adds nothing useful here.
 */
@Composable
fun MascotAnimation(
    @RawRes resId: Int,
    size: Dp,
    modifier: Modifier = Modifier,
    contentDescription: String? = null,
    loop: Boolean = true,
) {
    val context = LocalContext.current
    val request = ImageRequest.Builder(context)
        .data(resId)
        .memoryCacheKey("mascot:$resId:${BuildConfig.VERSION_CODE}")
        .diskCachePolicy(CachePolicy.DISABLED)
        .repeatCount(if (loop) -1 else 0)
        .build()

    AsyncImage(
        model = request,
        contentDescription = contentDescription,
        modifier = modifier.size(size),
        contentScale = ContentScale.Fit,
    )
}
