package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.LocationOff
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.mapbox.geojson.Point
import com.mapbox.maps.EdgeInsets
import com.mapbox.maps.ViewAnnotationAnchor
import com.mapbox.maps.extension.compose.MapboxMap
import com.mapbox.maps.extension.compose.animation.viewport.rememberMapViewportState
import com.mapbox.maps.extension.compose.annotation.ViewAnnotation
import com.mapbox.maps.extension.compose.style.MapStyle
import com.mapbox.maps.viewannotation.annotationAnchor
import com.mapbox.maps.viewannotation.geometry
import com.mapbox.maps.viewannotation.viewAnnotationOptions
import cz.cleansia.core.location.MapStyles
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.customer.R

/**
 * Whether the order detail backs its sheet with a real map.
 *
 * Purely a question of the geocode, in every status. Cancelled used to be excluded on the
 * reasoning that the visit never happened; the job still had a place, and the placeholder read
 * as a map that had failed to load. Owner ruling, 2026-08-27.
 *
 * The remaining guards are unrelated and stay: orders placed before the address geocoder existed
 * carry no coordinates, and a failed geocode is stored as null island (0, 0), which would
 * otherwise drop a pin in the Gulf of Guinea.
 */
internal fun canShowOrderMap(
    latitude: Double?,
    longitude: Double?,
): Boolean {
    if (latitude == null || longitude == null) return false
    if (latitude == 0.0 && longitude == 0.0) return false
    return latitude in -90.0..90.0 && longitude in -180.0..180.0
}

/**
 * Full-bleed map behind the sheet. The pin is a Mapbox ViewAnnotation rather
 * than a Compose overlay so it stays on its coordinate while the user pans the
 * map or drags the sheet across it.
 */
@Composable
internal fun OrderMapBackdrop(
    latitude: Double,
    longitude: Double,
    darkTheme: Boolean,
    sheetCoverHeight: Dp,
) {
    val point = remember(latitude, longitude) { Point.fromLngLat(longitude, latitude) }
    val density = LocalDensity.current
    // Camera padding tells Mapbox the bottom of the viewport is obscured, so
    // it centres the pin in the strip the sheet leaves visible at rest. Held
    // at the resting anchor rather than the live sheet offset: re-centring the
    // camera on every drag frame fights the gesture instead of following it.
    val bottomPaddingPx = with(density) { sheetCoverHeight.toPx().toDouble() }
    val viewportState = rememberMapViewportState {
        setCameraOptions {
            center(point)
            zoom(15.0)
            padding(EdgeInsets(0.0, 0.0, bottomPaddingPx, 0.0))
        }
    }
    val annotationOptions = remember(point) {
        viewAnnotationOptions {
            geometry(point)
            annotationAnchor { anchor(ViewAnnotationAnchor.BOTTOM) }
            allowOverlap(true)
        }
    }

    MapboxMap(
        modifier = Modifier.fillMaxSize(),
        mapViewportState = viewportState,
        style = { MapStyle(style = if (darkTheme) MapStyles.DARK else MapStyles.LIGHT) },
        scaleBar = {},
        compass = {},
        logo = {},
        attribution = {},
    ) {
        ViewAnnotation(options = annotationOptions) {
            MapPin()
        }
    }
}

@Composable
private fun MapPin() {
    Box(
        modifier = Modifier
            .size(40.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primary),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = Icons.Outlined.Place,
            contentDescription = null,
            tint = Color.White,
            modifier = Modifier.size(22.dp),
        )
    }
}

/**
 * Stand-in for the map when the order has no usable geocode or was cancelled.
 * Says so rather than leaving a blank tint the user has to interpret.
 */
@Composable
internal fun OrderMapPlaceholder() {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.primaryContainer),
    ) {
        Column(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .padding(top = Spacing.XXL, start = Spacing.ML, end = Spacing.ML),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Icon(
                imageVector = Icons.Outlined.LocationOff,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimaryContainer,
                modifier = Modifier.size(28.dp),
            )
            Spacer(Modifier.height(Spacing.XS))
            Text(
                text = stringResource(R.string.order_detail_map_unavailable),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onPrimaryContainer,
                textAlign = TextAlign.Center,
            )
        }
    }
}

/** Circular control that floats over the map — back, and the map/details toggle. */
@Composable
internal fun MapOverlayButton(
    icon: ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        onClick = onClick,
        modifier = modifier.size(40.dp),
        shape = CircleShape,
        color = MaterialTheme.colorScheme.surface,
        shadowElevation = 4.dp,
    ) {
        Box(contentAlignment = Alignment.Center) {
            Icon(
                imageVector = icon,
                contentDescription = contentDescription,
                tint = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}
