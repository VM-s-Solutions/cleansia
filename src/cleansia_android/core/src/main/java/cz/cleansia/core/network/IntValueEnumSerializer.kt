package cz.cleansia.core.network

import kotlinx.serialization.KSerializer
import kotlinx.serialization.descriptors.PrimitiveKind
import kotlinx.serialization.descriptors.PrimitiveSerialDescriptor
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder

/**
 * Workaround for an OpenAPI Generator quirk: every int-valued enum in the
 * backend's spec (`[SwaggerEnumAsInt]` on the .NET side) emits a Kotlin
 * enum that serializes as a *string* — `@SerialName("1")` on each entry.
 * The generator's template doesn't honour `type: integer` from the spec.
 *
 * Symptom: `POST /api/Order/CreateOrder` body shows `"paymentType":"1"`
 * (string) and the .NET deserializer rejects it as not-a-valid-int-enum.
 *
 * Fix: register one of [IntValueEnumSerializer] for each affected enum via
 * the JSON `serializersModule` config. The codec reads/writes the enum's
 * `.value: Int` property and overrides whatever `@SerialName` the generator
 * wrote.
 *
 * App-specific lists of int-enum bindings (which classes to register +
 * how to map int↔enum) live in each app's `IntEnumSerializers.kt`. This
 * class is the only shared piece.
 */
class IntValueEnumSerializer<T : Enum<T>>(
    serialName: String,
    private val toInt: (T) -> Int,
    private val fromInt: (Int) -> T?,
) : KSerializer<T> {
    override val descriptor: SerialDescriptor =
        PrimitiveSerialDescriptor(serialName, PrimitiveKind.INT)

    override fun serialize(encoder: Encoder, value: T) {
        encoder.encodeInt(toInt(value))
    }

    override fun deserialize(decoder: Decoder): T {
        val raw = decoder.decodeInt()
        return fromInt(raw)
            ?: error("Unknown ${descriptor.serialName} int value: $raw")
    }
}
