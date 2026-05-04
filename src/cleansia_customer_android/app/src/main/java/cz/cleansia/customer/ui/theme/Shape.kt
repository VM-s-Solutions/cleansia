package cz.cleansia.customer.ui.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Shapes
import androidx.compose.ui.unit.dp

val CleansiaShapes = Shapes(
    extraSmall = RoundedCornerShape(6.dp),
    small = RoundedCornerShape(12.dp),
    medium = RoundedCornerShape(16.dp), // default card
    large = RoundedCornerShape(24.dp),
    extraLarge = RoundedCornerShape(32.dp), // bottom sheets, hero cards
)
