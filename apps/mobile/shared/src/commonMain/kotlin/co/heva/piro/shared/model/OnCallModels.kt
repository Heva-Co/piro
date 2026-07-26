package co.heva.piro.shared.model

import kotlinx.serialization.Serializable

/**
 * One resolved on-call slot (OnCallSlotDto) from GET /api/v1/oncall/schedules/me/slots — a concrete
 * shift with who is on call and when, already expanded from the schedule's rotation + overrides by the
 * backend. The mobile calendar renders these directly; it never replays the RRULE itself.
 *
 * [startsAt]/[endsAt] are ISO-8601 strings clipped to the requested window. [userColor] is the user's
 * avatar hex (e.g. "#ec4899"), used to tint the shift on the calendar.
 */
@Serializable
data class OnCallSlot(
    val layerId: Int = 0,
    val layerName: String = "",
    val userId: Int = 0,
    val userName: String = "",
    val userInitials: String = "",
    val userColor: String = "",
    val startsAt: String,
    val endsAt: String,
    val isOverride: Boolean = false,
    val replacesUserName: String? = null,
    val scheduleId: Int? = null,
    val scheduleName: String? = null,
    val layerOrder: Int = 0,
    val isPrimarySchedule: Boolean = true,
    /** True for all-day shifts — the UI shows the date + "All day" instead of a time range. */
    val isAllDay: Boolean = false,
)
