package co.heva.piro.android.schedule

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.model.OnCallSlot
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate
import java.time.OffsetDateTime
import java.time.YearMonth
import java.time.ZoneId
import java.time.format.DateTimeFormatter

data class ScheduleUiState(
    val month: YearMonth = YearMonth.now(),
    val loading: Boolean = true,
    val error: String? = null,
    /** Resolved shifts intersecting the visible month. */
    val slots: List<OnCallSlot> = emptyList(),
    /** Local dates in the month that have at least one shift → the user's own color for that day. */
    val onCallDays: Map<LocalDate, String> = emptyMap(),
)

/**
 * Backs the Schedule calendar: loads the signed-in user's resolved on-call shifts for the visible month
 * and projects them onto local calendar days so the grid can mark on-call dates.
 */
class ScheduleViewModel(private val api: PiroApiClient) : ViewModel() {

    private val _state = MutableStateFlow(ScheduleUiState())
    val state: StateFlow<ScheduleUiState> = _state.asStateFlow()

    init {
        load(YearMonth.now())
    }

    fun previousMonth() = load(_state.value.month.minusMonths(1))
    fun nextMonth() = load(_state.value.month.plusMonths(1))

    private fun load(month: YearMonth) {
        viewModelScope.launch {
            _state.update { it.copy(month = month, loading = true, error = null) }
            // Pad the query by a day on each side so shifts straddling the month boundary come back.
            val from = month.atDay(1).atStartOfDay(ZoneId.systemDefault()).minusDays(1).toOffsetDateTime()
            val to = month.atEndOfMonth().atTime(23, 59, 59).atZone(ZoneId.systemDefault()).plusDays(1).toOffsetDateTime()
            try {
                val slots = api.getMyOnCallSlots(from.format(ISO), to.format(ISO))
                _state.update { it.copy(loading = false, slots = slots, onCallDays = projectDays(slots, month)) }
            } catch (e: Exception) {
                _state.update { it.copy(loading = false, error = "Could not load the schedule.") }
            }
        }
    }

    /** Marks each local date the user is on call, keyed to their avatar color for the calendar dots. */
    private fun projectDays(slots: List<OnCallSlot>, month: YearMonth): Map<LocalDate, String> {
        val days = mutableMapOf<LocalDate, String>()
        val zone = ZoneId.systemDefault()
        for (slot in slots) {
            val start = runCatching { OffsetDateTime.parse(slot.startsAt).atZoneSameInstant(zone).toLocalDate() }.getOrNull() ?: continue
            val end = runCatching { OffsetDateTime.parse(slot.endsAt).atZoneSameInstant(zone).toLocalDate() }.getOrNull() ?: continue
            var d = start
            while (!d.isAfter(end)) {
                if (YearMonth.from(d) == month) days[d] = slot.userColor.ifBlank { "#3D96FE" }
                d = d.plusDays(1)
            }
        }
        return days
    }

    private companion object {
        val ISO: DateTimeFormatter = DateTimeFormatter.ISO_OFFSET_DATE_TIME
    }
}
