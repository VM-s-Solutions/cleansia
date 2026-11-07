package cz.cleansia.partner.features.orders

import android.graphics.RenderEffect
import android.graphics.Shader
import android.graphics.drawable.ColorDrawable
import android.os.Build
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.GpsFixed
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.mapbox.geojson.Point
import com.mapbox.maps.CameraOptions
import com.mapbox.maps.EdgeInsets
import com.mapbox.maps.MapView
import com.mapbox.maps.ViewAnnotationAnchor
import com.mapbox.maps.extension.compose.ComposeMapInitOptions
import com.mapbox.maps.extension.compose.MapEffect
import com.mapbox.maps.extension.compose.MapboxMap
import com.mapbox.maps.extension.compose.animation.viewport.MapViewportState
import com.mapbox.maps.extension.compose.animation.viewport.rememberMapViewportState
import com.mapbox.maps.extension.compose.rememberMapState
import com.mapbox.maps.extension.compose.annotation.ViewAnnotation
import com.mapbox.maps.plugin.gestures.generated.GesturesSettings
import com.mapbox.maps.viewannotation.annotationAnchor
import com.mapbox.maps.viewannotation.geometry
import com.mapbox.maps.viewannotation.viewAnnotationOptions
import cz.cleansia.core.location.CleansiaMapPin
import cz.cleansia.core.location.CleansiaMapStyle
import cz.cleansia.core.location.canShowOrderMap
import cz.cleansia.core.ui.components.FloatingCircleButton
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.hypot
import kotlin.math.ln
import kotlin.math.pow
import kotlin.math.sin

data class MapPoint(val latitude: Double, val longitude: Double)

/** What the band behind the job detail shows. */
internal sealed interface JobMapMode {
    /** Whether she can pan and zoom it: only the door of a job she has taken. */
    val pans: Boolean get() = this is Pin

    /** The job's area, unreadable; [locked] while the job is not hers, so the chip says why. */
    data class Area(val locked: Boolean) : JobMapMode

    /** The door she is going to. */
    data class Pin(val point: MapPoint) : JobMapMode
}

/** Before the take the area only, whatever arrived; once taken the door when its geocode is usable, else the area. */
internal fun jobMapMode(taken: Boolean, latitude: Double?, longitude: Double?): JobMapMode = when {
    !taken -> JobMapMode.Area(locked = true)
    else -> usablePoint(latitude, longitude)?.let { JobMapMode.Pin(it) } ?: JobMapMode.Area(locked = false)
}

/**
 * Whether the camera has left the job's framing: the [job]'s point more than 8 dp from where the framing put it, or the
 * scale more than 5 % off [framedZoom]. The camera's centre sits where the framing put the point, so the distance is the
 * centre's, at the current [zoom], on the 512 dp world of zoom 0.
 */
internal fun movedOffJob(job: MapPoint, framedZoom: Double, centre: MapPoint, zoom: Double): Boolean {
    val world = 512.0 * 2.0.pow(zoom)
    val dx = (mercatorX(centre.longitude) - mercatorX(job.longitude)) * world
    val dy = (mercatorY(centre.latitude) - mercatorY(job.latitude)) * world
    return hypot(dx, dy) > 8.0 || abs(2.0.pow(zoom - framedZoom) - 1.0) > 0.05
}

private fun mercatorX(longitude: Double) = (longitude + 180.0) / 360.0

private fun mercatorY(latitude: Double): Double {
    val sin = sin(Math.toRadians(latitude))
    return 0.5 - ln((1 + sin) / (1 - sin)) / (4 * PI)
}

internal fun usablePoint(latitude: Double?, longitude: Double?): MapPoint? =
    if (latitude != null && longitude != null && canShowOrderMap(latitude, longitude)) MapPoint(latitude, longitude) else null

/** The Job taken panel's strip: only from the taken row's own coordinates, else no strip. */
internal fun takenStripPoint(row: OrderListItem?): MapPoint? = row?.let { usablePoint(it.customerAddressLatitude, it.customerAddressLongitude) }

/**
 * Where the door's pin tip sits on a map [height] tall: on the band's pin [line], or mid-map when the map is no taller
 * than the line, because framing it on the line would then leave the camera no room and put the door off the map.
 */
internal fun pinTip(line: Dp, height: Dp): Dp = if (height > line) line else height / 2

/**
 * The detail's map behind the panel, at full height: before the take the job's area blurred and dimmed under the lock
 * chip, still; once taken the door with its pin and street, which she can pan and zoom (never rotate or tilt) while the
 * panel stays where it is. The point sits where the [band] (its height from the top of the screen) had it, or mid-map
 * on a map too short for that, at the band's zoom, and the wordmark and attribution ride the panel's live top. Once she
 * has moved the door off that framing, a button beside the back button brings it back; a new size of the map keeps her
 * view until then. [areaCentre] is null until the area is placed; the band keeps its colour until then.
 */
@Composable
internal fun JobBandMap(
    mode: JobMapMode,
    areaCentre: MapPoint?,
    street: String?,
    band: Dp,
    sheetTopPx: () -> Float,
    modifier: Modifier = Modifier,
) {
    BoxWithConstraints(modifier.fillMaxSize()) {
        val height = maxHeight
        val density = LocalDensity.current
        // A drag or a settle that runs through a resize keeps the panel's old top, which can sit below the map; negative padding throws.
        val ornaments = { with(density) { (height - sheetTopPx().toDp()).coerceAtLeast(0.dp) } + 8.dp }
        when (mode) {
            is JobMapMode.Pin -> {
                val tip = pinTip(line = band - SheetOverBand - 8.dp, height = height)
                val framing = rememberFraming(
                    centre = mode.point,
                    zoom = PinZoom,
                    top = (tip * 2 - height).coerceAtLeast(0.dp),
                    bottom = (height - tip * 2).coerceAtLeast(0.dp),
                    end = maxWidth * 0.12f,
                )
                val viewport = key(mode.point) { rememberMapViewportState { setCameraOptions(framing) } }
                val away by remember(viewport, mode.point) {
                    derivedStateOf {
                        viewport.cameraState?.let { camera ->
                            movedOffJob(mode.point, PinZoom, MapPoint(camera.center.latitude(), camera.center.longitude()), camera.zoom)
                        } == true
                    }
                }
                LaunchedEffect(viewport, framing) { if (!away) viewport.setCameraOptions(framing) }
                JobMap(
                    framing = framing,
                    viewport = viewport,
                    ornamentsBottom = ornaments,
                    pinLabel = street,
                    modifier = Modifier.fillMaxSize(),
                )
                if (away) {
                    FloatingCircleButton(
                        icon = Icons.Outlined.GpsFixed,
                        contentDescription = stringResource(R.string.orders_map_recenter),
                        onClick = { viewport.easeTo(framing) },
                        modifier = Modifier
                            .windowInsetsPadding(WindowInsets.statusBars)
                            .padding(start = RecentreStart, top = 12.dp),
                    )
                }
            }
            is JobMapMode.Area -> areaCentre?.let { centre ->
                JobMap(
                    framing = rememberFraming(centre = centre, zoom = 12.0, bottom = (height - band).coerceAtLeast(0.dp)),
                    ornamentsBottom = ornaments,
                    pinLabel = null,
                    veiled = true,
                    modifier = Modifier.fillMaxSize(),
                )
            }
        }
        if (!mode.pans) Box(Modifier.fillMaxSize().pointerInput(Unit) {})
        if (mode is JobMapMode.Area && mode.locked) {
            LockChip(
                Modifier
                    .windowInsetsPadding(WindowInsets.statusBars)
                    .padding(start = LockChipStart, end = 16.dp, top = 12.dp)
                    .height(48.dp)
                    .fillMaxWidth(),
            )
        }
    }
}

private const val PinZoom = 15.0

/** Clear of the back button: its 16 inset, its 48 and a 12 gap. */
private val LockChipStart = 76.dp

/** Beside the back button: its 16 inset, its 40 and an 8 gap. */
private val RecentreStart = 64.dp

/** "🔒 Exact address after you take the job" on a quiet capsule, centred on the back button's line. */
@Composable
private fun LockChip(modifier: Modifier = Modifier) {
    val scheme = MaterialTheme.colorScheme
    Box(modifier, contentAlignment = Alignment.CenterStart) {
        Row(
            modifier = Modifier
                .clip(CircleShape)
                .background(scheme.surface.copy(alpha = 0.9f))
                .border(1.dp, scheme.outlineVariant, CircleShape)
                .padding(horizontal = 14.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(Icons.Outlined.Lock, contentDescription = null, tint = scheme.onSurface, modifier = Modifier.size(16.dp))
            Spacer(Modifier.width(8.dp))
            Text(
                text = OrdersBoardRules.tie(stringResource(R.string.orders_map_exact_after_take)),
                style = MaterialTheme.typography.bodySmall,
                color = scheme.onSurface,
                maxLines = 2,
            )
        }
    }
}

/** The Job taken panel's strip: the door with its pin and street, 120 high, for looking only. */
@Composable
internal fun JobMapStrip(point: MapPoint, street: String?, modifier: Modifier = Modifier) {
    Box(
        modifier
            .fillMaxWidth()
            .height(StripHeight)
            .clip(RoundedCornerShape(16.dp)),
    ) {
        JobMap(
            framing = rememberFraming(centre = point, zoom = PinZoom, top = (StripPinTip * 2 - StripHeight).coerceAtLeast(0.dp)),
            ornamentsBottom = { 4.dp },
            pinLabel = street,
            modifier = Modifier.fillMaxSize(),
        )
        Box(Modifier.fillMaxSize().pointerInput(Unit) {})
    }
}

private val StripHeight = 120.dp
private val StripPinTip = 108.dp

/** The camera on [centre] at [zoom], the viewport's top, bottom and end inset so the point lands where the band leaves room. */
@Composable
private fun rememberFraming(centre: MapPoint, zoom: Double, top: Dp = 0.dp, bottom: Dp = 0.dp, end: Dp = 0.dp): CameraOptions {
    val density = LocalDensity.current
    return remember(centre, zoom, top, bottom, end, density) {
        with(density) {
            CameraOptions.Builder()
                .center(Point.fromLngLat(centre.longitude, centre.latitude))
                .zoom(zoom)
                .padding(EdgeInsets(top.toPx().toDouble(), 0.0, bottom.toPx().toDouble(), end.toPx().toDouble()))
                .build()
        }
    }
}

/**
 * One map of the job, in the app's map look: the camera on [framing], or on the caller's [viewport] when she may move it,
 * the pin with its street when [pinLabel] is not null, and the Mapbox wordmark and attribution lifted above what covers
 * the map's bottom edge. A [veiled] map is blurred 7 and dimmed 25 % (a 68 % dim where the system cannot blur) on the
 * map view itself, so the wordmark and attribution drawn over it stay sharp.
 */
@Composable
private fun JobMap(
    framing: CameraOptions,
    ornamentsBottom: () -> Dp,
    pinLabel: String?,
    modifier: Modifier = Modifier,
    veiled: Boolean = false,
    viewport: MapViewportState? = null,
) {
    val dark = isDarkScheme()
    val density = LocalDensity.current
    key(viewport ?: framing) {
        val mapState = rememberMapState {
            gesturesSettings = GesturesSettings {
                rotateEnabled = false
                pitchEnabled = false
            }
        }
        MapboxMap(
            modifier = modifier.clearAndSetSemantics {},
            composeMapInitOptions = ComposeMapInitOptions(density.density, true),
            mapViewportState = viewport ?: rememberMapViewportState { setCameraOptions(framing) },
            mapState = mapState,
            style = { CleansiaMapStyle(dark) },
            scaleBar = {},
            compass = {},
            logo = { Logo(contentPadding = PaddingValues(start = 8.dp, bottom = ornamentsBottom()), alignment = Alignment.BottomStart) },
            attribution = { Attribution(contentPadding = PaddingValues(end = 8.dp, bottom = ornamentsBottom()), alignment = Alignment.BottomEnd) },
        ) {
            if (veiled) MapEffect(Unit) { view -> view.veil() }
            val point = framing.center
            if (pinLabel != null && point != null) {
                val options = remember(point) {
                    viewAnnotationOptions {
                        geometry(point)
                        annotationAnchor { anchor(ViewAnnotationAnchor.BOTTOM) }
                        allowOverlap(true)
                    }
                }
                ViewAnnotation(options = options) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        if (pinLabel.isNotBlank()) PinLabel(pinLabel)
                        CleansiaMapPin(darkTheme = dark)
                    }
                }
            }
        }
    }
}

private fun MapView.veil() {
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
        val radius = 7f * resources.displayMetrics.density
        setRenderEffect(RenderEffect.createBlurEffect(radius, radius, Shader.TileMode.CLAMP))
    }
    val dim = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) 0.25f else 0.68f
    foreground = ColorDrawable(Color.Black.copy(alpha = dim).toArgb())
}

@Composable
private fun PinLabel(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.onSurface,
        maxLines = 1,
        textAlign = TextAlign.Center,
        modifier = Modifier
            .padding(bottom = 4.dp)
            .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.92f), RoundedCornerShape(8.dp))
            .padding(horizontal = 8.dp, vertical = 3.dp),
    )
}
