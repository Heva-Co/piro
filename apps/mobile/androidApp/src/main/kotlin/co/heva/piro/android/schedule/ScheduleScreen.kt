package co.heva.piro.android.schedule

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import co.heva.piro.android.ui.SkeletonList
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import co.heva.piro.android.PiroApp
import co.heva.piro.android.ui.theme.PiroColors
import co.heva.piro.shared.model.OnCallSlot
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.TextStyle
import java.util.Locale

/**
 * The Schedule tab: a month calendar of the signed-in user's on-call rotation. Days the user is on call
 * are dotted in their avatar color; below the grid is the list of shifts for the month. Data comes
 * resolved from the backend (`getMyOnCallSlots`) — no client-side RRULE math.
 */
@Composable
fun ScheduleScreen(modifier: Modifier = Modifier) {
    val app = androidx.compose.ui.platform.LocalContext.current.applicationContext as PiroApp
    val vm: ScheduleViewModel = viewModel(factory = ScheduleViewModelFactory(app.services.api))
    val state by vm.state.collectAsStateWithLifecycle()

    Column(modifier = modifier.fillMaxSize().padding(horizontal = 20.dp)) {
        MonthHeader(
            title = state.month.month.getDisplayName(TextStyle.FULL, Locale.getDefault())
                .replaceFirstChar { it.uppercase() } + " " + state.month.year,
            onPrev = vm::previousMonth,
            onNext = vm::nextMonth,
        )

        WeekdayRow()
        CalendarGrid(
            firstOfMonth = state.month.atDay(1),
            daysInMonth = state.month.lengthOfMonth(),
            onCallDays = state.onCallDays,
        )

        when {
            state.loading -> SkeletonList(Modifier.padding(top = 16.dp), count = 5)
            state.error != null -> Text(
                state.error!!,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 24.dp),
            )
            state.slots.isEmpty() -> Text(
                "No on-call shifts this month.",
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 24.dp),
            )
            else -> ShiftList(state.slots)
        }
    }
}

@Composable
private fun MonthHeader(title: String, onPrev: () -> Unit, onNext: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().padding(vertical = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(title, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onBackground)
        Row {
            IconButton(onClick = onPrev) { Icon(Icons.AutoMirrored.Filled.KeyboardArrowLeft, "Previous month", tint = MaterialTheme.colorScheme.onSurfaceVariant) }
            IconButton(onClick = onNext) { Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Next month", tint = MaterialTheme.colorScheme.onSurfaceVariant) }
        }
    }
}

@Composable
private fun WeekdayRow() {
    // Monday-first week, matching the Piro web calendar (M T W T F S S).
    val days = listOf(DayOfWeek.MONDAY, DayOfWeek.TUESDAY, DayOfWeek.WEDNESDAY, DayOfWeek.THURSDAY, DayOfWeek.FRIDAY, DayOfWeek.SATURDAY, DayOfWeek.SUNDAY)
    Row(Modifier.fillMaxWidth()) {
        days.forEach { d ->
            Text(
                d.getDisplayName(TextStyle.NARROW, Locale.getDefault()),
                modifier = Modifier.weight(1f),
                textAlign = TextAlign.Center,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun CalendarGrid(firstOfMonth: LocalDate, daysInMonth: Int, onCallDays: Map<LocalDate, String>) {
    // Leading blanks so day 1 lands under its weekday (Monday-first).
    val lead = (firstOfMonth.dayOfWeek.value + 6) % 7
    val cells = lead + daysInMonth
    val rows = (cells + 6) / 7
    val today = LocalDate.now()

    Column(Modifier.fillMaxWidth().padding(top = 4.dp)) {
        for (row in 0 until rows) {
            Row(Modifier.fillMaxWidth()) {
                for (col in 0 until 7) {
                    val index = row * 7 + col
                    val dayNum = index - lead + 1
                    Box(Modifier.weight(1f).aspectRatio(1f), Alignment.Center) {
                        if (dayNum in 1..daysInMonth) {
                            val date = firstOfMonth.withDayOfMonth(dayNum)
                            DayCell(dayNum, date == today, onCallDays[date])
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DayCell(day: Int, isToday: Boolean, onCallColor: String?) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Box(Modifier.size(30.dp), Alignment.Center) {
            Text(
                day.toString(),
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = if (isToday) FontWeight.Bold else FontWeight.Normal,
                color = if (isToday) PiroColors.Blue else MaterialTheme.colorScheme.onBackground,
            )
        }
        // Dot marks an on-call day, tinted with the on-call user's color.
        Box(
            Modifier.size(6.dp).clip(CircleShape),
        ) {
            if (onCallColor != null) {
                Surface(color = parseColor(onCallColor), shape = CircleShape, modifier = Modifier.size(6.dp)) {}
            }
        }
    }
}

@Composable
private fun ShiftList(slots: List<OnCallSlot>) {
    LazyColumn(Modifier.fillMaxWidth().padding(top = 16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        items(slots) { slot -> ShiftRow(slot) }
    }
}

@Composable
private fun ShiftRow(slot: OnCallSlot) {
    Surface(color = MaterialTheme.colorScheme.surface, shape = RoundedCornerShape(10.dp), modifier = Modifier.fillMaxWidth()) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(color = parseColor(slot.userColor), shape = CircleShape, modifier = Modifier.size(10.dp)) {}
            Column(Modifier.padding(start = 12.dp).weight(1f)) {
                Text(
                    slot.userName.ifBlank { "On-call" } + (if (slot.isOverride) " (override)" else ""),
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Medium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(
                    shiftRange(slot),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (!slot.scheduleName.isNullOrBlank()) {
                Text(slot.scheduleName!!, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

private val rangeFmt = DateTimeFormatter.ofPattern("MMM d, HH:mm", Locale.getDefault())
private val dayFmt = DateTimeFormatter.ofPattern("MMM d", Locale.getDefault())

private fun shiftRange(slot: OnCallSlot): String {
    val zone = ZoneId.systemDefault()
    // All-day shifts read as a date (or date span) + "All day" — no drifting time range.
    if (slot.isAllDay) {
        val start = runCatching { OffsetDateTime.parse(slot.startsAt).atZoneSameInstant(zone).toLocalDate() }.getOrNull()
        // The end is exclusive local midnight; the last covered day is the day before it.
        val endExclusive = runCatching { OffsetDateTime.parse(slot.endsAt).atZoneSameInstant(zone).toLocalDate() }.getOrNull()
        val lastDay = endExclusive?.minusDays(1)
        return when {
            start == null -> "All day"
            // Only render a span when the last day is genuinely after start — never a backwards range.
            lastDay == null || !lastDay.isAfter(start) -> "${start.format(dayFmt)} · All day"
            else -> "${start.format(dayFmt)} – ${lastDay.format(dayFmt)} · All day"
        }
    }
    val s = runCatching { OffsetDateTime.parse(slot.startsAt).atZoneSameInstant(zone).format(rangeFmt) }.getOrDefault(slot.startsAt)
    val e = runCatching { OffsetDateTime.parse(slot.endsAt).atZoneSameInstant(zone).format(rangeFmt) }.getOrDefault(slot.endsAt)
    return "$s → $e"
}

/** Parses a "#rrggbb" hex to a Compose Color, falling back to the Piro brand blue. */
private fun parseColor(hex: String): Color = runCatching {
    val clean = hex.removePrefix("#")
    Color(("ff" + clean).toLong(16))
}.getOrDefault(PiroColors.Blue)
