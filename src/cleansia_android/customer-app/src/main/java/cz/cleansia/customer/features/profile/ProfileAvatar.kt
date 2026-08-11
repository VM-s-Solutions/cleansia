package cz.cleansia.customer.features.profile

import android.net.Uri
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.TextStyle
import coil3.compose.AsyncImage
import coil3.request.CachePolicy
import coil3.request.ImageRequest
import coil3.request.crossfade
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.CurrentUser

/**
 * What to draw inside a profile avatar disc.
 *
 * [Remote] separates the fetch target from the cache key on purpose: `url` is a
 * read SAS minted fresh on every profile fetch, so keying a cache on it misses
 * every time and pins a URL that dies within the hour. `fileName` is a GUID the
 * backend re-mints on every upload (`UpdateCurrentUser.cs:149`), which makes it
 * content-addressed — stable while the image is, different the moment it isn't.
 */
sealed interface AvatarPhoto {
    data class Remote(val url: String, val fileName: String?) : AvatarPhoto

    /** A just-picked local image, previewed before it has been uploaded. */
    data class Local(val uri: Uri) : AvatarPhoto
}

/**
 * What the avatar disc should show, given the saved profile and any pending edit.
 *
 * The pending draft wins over the saved profile in both directions: a pick shows
 * before it uploads, and a removal shows the initials before the save reaches the
 * server. Null means "no photo", which is also the "there is nothing to remove"
 * signal for the edit screen's options sheet.
 */
fun avatarPhotoFor(user: CurrentUser?, draft: AvatarDraft): AvatarPhoto? = when (draft) {
    is AvatarDraft.Picked -> AvatarPhoto.Local(draft.previewUri)
    AvatarDraft.Removed -> null
    AvatarDraft.Unchanged -> user?.avatarUrl?.let { AvatarPhoto.Remote(it, user.avatarFileName) }
}

/**
 * The contents of an avatar disc: the photo when there is one, the initials
 * otherwise.
 *
 * Deliberately fills its parent rather than sizing itself, so both call sites
 * keep the disc geometry they already had — the hero's 72dp circle was shaped so
 * an image could drop into it without re-laying-out the header.
 *
 * [onLoadFailed] fires on any load error. The caller refetches the profile once
 * for a fresh SAS; meanwhile this falls back to the initials, so a blob that is
 * genuinely gone shows a placeholder rather than a broken frame.
 */
@Composable
fun ProfileAvatarContent(
    initials: String,
    initialsStyle: TextStyle,
    initialsColor: Color,
    modifier: Modifier = Modifier,
    photo: AvatarPhoto? = null,
    onLoadFailed: () -> Unit = {},
    onLoadSucceeded: () -> Unit = {},
) {
    // Keyed on the whole photo, not just the name: a refetched profile carries the
    // same name with a fresh SAS, and that is precisely the attempt worth retrying.
    var failed by remember(photo) { mutableStateOf(false) }

    if (photo == null || failed) {
        Text(initials.uppercase(), style = initialsStyle, color = initialsColor, modifier = modifier)
        return
    }

    val context = LocalContext.current
    val request = remember(photo) {
        ImageRequest.Builder(context)
            .data(
                when (photo) {
                    is AvatarPhoto.Remote -> photo.url
                    is AvatarPhoto.Local -> photo.uri
                },
            )
            // Memory only. The bytes behind that SAS are a photograph of the
            // signed-in user, and the disc is small enough that re-fetching it
            // once per cold start is cheaper than deciding when to evict a face
            // from disk on a shared handset.
            .memoryCacheKey((photo as? AvatarPhoto.Remote)?.fileName)
            .diskCachePolicy(CachePolicy.DISABLED)
            .crossfade(true)
            .build()
    }

    AsyncImage(
        model = request,
        contentDescription = stringResource(R.string.profile_avatar_content_description),
        contentScale = ContentScale.Crop,
        modifier = modifier
            .fillMaxSize()
            .clip(CircleShape),
        onError = {
            failed = true
            onLoadFailed()
        },
        onSuccess = { onLoadSucceeded() },
    )
}
