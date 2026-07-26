package co.heva.piro.android.util

import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/**
 * Formats an API timestamp (ISO-8601, e.g. "2026-07-25T02:20:00.06+00:00") into the device's local
 * time zone for display — the backend sends UTC/offset times, and an on-call engineer wants to see
 * "when did this fire" in their own clock, not raw UTC.
 */
object DateFormat {
    private val display = DateTimeFormatter.ofPattern("MMM d, yyyy • h:mm a", Locale.getDefault())

    fun localDateTime(isoString: String?): String {
        if (isoString.isNullOrBlank()) return "—"
        return try {
            OffsetDateTime.parse(isoString)
                .atZoneSameInstant(ZoneId.systemDefault())
                .format(display)
        } catch (e: Exception) {
            isoString // fall back to the raw value rather than crash on an unexpected format
        }
    }
}
