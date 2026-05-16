package cz.cleansia.customer.core.location

/**
 * Map style URIs.
 *
 * **Current state:** using Mapbox's stock light/dark styles. They look clean and muted,
 * close enough to the Cleansia palette for launch.
 *
 * **Next step (when you have time):** create a custom style in Mapbox Studio
 * (https://studio.mapbox.com/), apply Cleansia palette (Sky-200 water, Slate-50 land,
 * Slate-300 roads, hide POI noise), publish, and replace the URIs below with
 * `mapbox://styles/{username}/{styleId}` for light + dark variants.
 *
 * Docs for style customisation:
 *  - https://docs.mapbox.com/studio-manual/reference/styles/
 *  - Use the "Monochrome" or "Basic" template as a starting point
 */
object MapStyles {
    const val LIGHT = "mapbox://styles/mapbox/light-v11"
    const val DARK = "mapbox://styles/mapbox/dark-v11"
}
