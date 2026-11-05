package cz.cleansia.core.ui.components

import android.content.res.Resources
import android.graphics.Color as AndroidColor
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.viewinterop.AndroidView
import cz.cleansia.core.R
import cz.cleansia.core.ui.theme.primaryText
import java.util.Base64

/**
 * Renders a server-rendered HTML fragment (a legal text) in-app, in the current theme's ink.
 *
 * A `WebView` with everything off: no JavaScript, no file or content access, and every navigation
 * refused — the fragment is the platform's own markdown output, and a link inside it must not turn
 * the sheet into a browser. Styling is injected here rather than sent by the server so the text
 * follows the app's light/dark scheme and reads in the app's body face.
 */
@Composable
fun HtmlContentView(
    html: String,
    modifier: Modifier = Modifier,
) {
    val ink = MaterialTheme.colorScheme.onSurface
    val accent = MaterialTheme.colorScheme.primary
    val link = primaryText()
    val resources = LocalContext.current.resources
    val document = remember(html, ink, accent, link) {
        HtmlDocument.wrap(html, ink, accent, link, HtmlDocument.bundledFontFaces(resources))
    }

    AndroidView(
        modifier = modifier,
        factory = { context ->
            WebView(context).apply {
                settings.javaScriptEnabled = false
                settings.allowFileAccess = false
                settings.allowContentAccess = false
                settings.setSupportZoom(false)
                isHorizontalScrollBarEnabled = false
                setBackgroundColor(AndroidColor.TRANSPARENT)
                webViewClient = object : WebViewClient() {
                    override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean = true
                }
            }
        },
        update = { view -> view.loadDataWithBaseURL(null, document, "text/html", "utf-8", null) },
    )
}

internal object HtmlDocument {

    private val bundledWeights = listOf(
        400 to R.font.nunito_regular,
        600 to R.font.nunito_semibold,
        700 to R.font.nunito_bold,
    )

    @Volatile
    private var bundled: String? = null

    /** A web view cannot see the fonts the app bundles, so each weight of the body face rides inside the document. */
    fun bundledFontFaces(resources: Resources): String = bundled ?: fontFaces(
        bundledWeights.map { (weight, font) -> weight to resources.openRawResource(font).use { it.readBytes() } },
    ).also { bundled = it }

    fun fontFaces(faces: List<Pair<Int, ByteArray>>): String = faces.joinToString("\n") { (weight, bytes) ->
        "@font-face { font-family: Nunito; font-weight: $weight; " +
            "src: url(data:font/ttf;base64,${Base64.getEncoder().encodeToString(bytes)}) format('truetype'); }"
    }

    /** [accent] draws the quote rule; [link] is the text blue a link reads in. */
    fun wrap(fragment: String, ink: Color, accent: Color, link: Color, fontFaces: String = ""): String = """
        <!doctype html>
        <html><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          $fontFaces
          body { margin: 0; padding: 0 0 16px; color: ${ink.cssHex()}; background: transparent;
                 font-family: Nunito, sans-serif; font-size: 15px; line-height: 1.55; }
          h1, h2, h3 { font-size: 16px; font-weight: 600; margin: 20px 0 6px; }
          p, li { margin: 0 0 10px; }
          blockquote { margin: 0 0 12px; padding: 8px 12px; border-left: 3px solid ${accent.cssHex()}; opacity: 0.85; }
          a { color: ${link.cssHex()}; }
        </style></head>
        <body>$fragment</body></html>
    """.trimIndent()

    private fun Color.cssHex(): String = "#%06X".format(toArgb() and 0xFFFFFF)
}
