package cz.cleansia.core.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import cz.cleansia.core.R
import org.junit.Assert.assertNotNull
import java.io.File
import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * Reads the real `res/font` binaries off disk so a coverage claim about them is measured rather
 * than remembered. Unit tests have no `Resources`, so the bundle is reached by file path and
 * `R.font` is used only for the id, whose field name is the file's base name.
 */
internal object BundledFontFiles {

    /** `U+0400`–`U+045F`, plus the four Ukrainian pairs that sit outside that block. */
    val cyrillic: Set<Int> = (0x0400..0x045F).toSet() + "ҐґЄєІіЇї".map { it.code }

    val byResId: Map<Int, File> by lazy {
        R.font::class.java.fields
            .filter { it.type == Int::class.javaPrimitiveType }
            .associate { it.getInt(null) to File(fontDir, "${it.name}.ttf") }
    }

    fun file(resId: Int): File =
        requireNotNull(byResId[resId]) { "resource id $resId is not one of ${byResId.values}" }

    fun codePoints(file: File): Set<Int> = coverage.getOrPut(file) { parseCmap(file) }

    /** `OS/2.usWeightClass` — what the renderer reads, as opposed to what Compose declares. */
    fun declaredWeight(file: File): Int {
        val bytes = file.readBytes()
        val buffer = bytes.bigEndian()
        val os2 = tableOffset(bytes, buffer, "OS/2")
        assertNotNull("${file.name} has no OS/2 table", os2)
        return buffer.u16(os2!! + 4)
    }

    fun typeScaleSlots(typography: Typography): List<Pair<String, TextStyle>> =
        Typography::class.java.methods
            .filter { it.parameterCount == 0 && it.returnType == TextStyle::class.java }
            .sortedBy { it.name }
            .map { slot ->
                val name = slot.name.removePrefix("get").replaceFirstChar(Char::lowercaseChar)
                name to slot.invoke(typography) as TextStyle
            }

    private val coverage = mutableMapOf<File, Set<Int>>()

    private val fontDir: File by lazy { File(androidRoot(), "core/src/main/res/font") }

    private fun androidRoot(): File {
        var dir: File? = File("").absoluteFile
        while (dir != null && !File(dir, "settings.gradle.kts").isFile) {
            dir = dir.parentFile
        }
        assertNotNull("could not locate the cleansia_android Gradle root", dir)
        return dir!!
    }

    private fun parseCmap(file: File): Set<Int> {
        val bytes = file.readBytes()
        val buffer = bytes.bigEndian()
        val cmap = tableOffset(bytes, buffer, "cmap")
        assertNotNull("${file.name} has no cmap table", cmap)
        val covered = mutableSetOf<Int>()
        repeat(buffer.u16(cmap!! + 2)) { index ->
            val subtable = cmap + buffer.u32(cmap + 4 + index * 8 + 4)
            when (buffer.u16(subtable)) {
                4 -> covered += segmentMapping(buffer, subtable)
                12 -> covered += segmentedCoverage(buffer, subtable)
            }
        }
        return covered
    }

    private fun segmentMapping(buffer: ByteBuffer, subtable: Int): Set<Int> {
        val segmentBytes = buffer.u16(subtable + 6)
        val ends = subtable + 14
        val starts = ends + segmentBytes + 2
        val deltas = starts + segmentBytes
        val rangeOffsets = deltas + segmentBytes
        val covered = mutableSetOf<Int>()
        for (segment in 0 until segmentBytes / 2) {
            val start = buffer.u16(starts + segment * 2)
            if (start == END_OF_SEGMENTS) continue
            val end = buffer.u16(ends + segment * 2)
            val delta = buffer.getShort(deltas + segment * 2).toInt()
            val rangeOffset = buffer.u16(rangeOffsets + segment * 2)
            for (code in start..end) {
                val glyph = if (rangeOffset == 0) {
                    (code + delta) and 0xFFFF
                } else {
                    val at = rangeOffsets + segment * 2 + rangeOffset + (code - start) * 2
                    if (at + 2 > buffer.limit()) continue
                    buffer.u16(at).let { if (it == 0) 0 else (it + delta) and 0xFFFF }
                }
                if (glyph != 0) covered += code
            }
        }
        return covered
    }

    private fun segmentedCoverage(buffer: ByteBuffer, subtable: Int): Set<Int> {
        val covered = mutableSetOf<Int>()
        repeat(buffer.u32(subtable + 12)) { group ->
            val at = subtable + 16 + group * 12
            covered += buffer.u32(at)..buffer.u32(at + 4)
        }
        return covered
    }

    private fun tableOffset(bytes: ByteArray, buffer: ByteBuffer, tag: String): Int? {
        repeat(buffer.u16(4)) { index ->
            val record = 12 + index * 16
            if (String(bytes, record, 4, Charsets.ISO_8859_1) == tag) return buffer.u32(record + 8)
        }
        return null
    }

    private const val END_OF_SEGMENTS = 0xFFFF

    private fun ByteArray.bigEndian(): ByteBuffer = ByteBuffer.wrap(this).order(ByteOrder.BIG_ENDIAN)

    private fun ByteBuffer.u16(at: Int): Int = getShort(at).toInt() and 0xFFFF

    private fun ByteBuffer.u32(at: Int): Int = (getInt(at).toLong() and 0xFFFFFFFFL).toInt()
}
