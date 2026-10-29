package cz.cleansia.core.format

data class CleaningEstimateProgress(val fraction: Float, val remainingMinutes: Long, val overrunMinutes: Long)

fun cleaningEstimateProgress(startedAtEpoch: Long?, estimatedMinutes: Int, nowEpoch: Long): CleaningEstimateProgress? {
    val start = startedAtEpoch ?: return null
    if (estimatedMinutes <= 0 || start > nowEpoch) return null
    val estimate = estimatedMinutes * 60L
    val elapsed = nowEpoch - start
    return CleaningEstimateProgress(
        fraction = (elapsed.toFloat() / estimate).coerceIn(0f, 1f),
        remainingMinutes = ((estimate - elapsed).coerceAtLeast(0) + 59) / 60,
        overrunMinutes = ((elapsed - estimate).coerceAtLeast(0) + 59) / 60,
    )
}
