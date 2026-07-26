import SwiftUI
import Shared

/// The Schedule tab: a month calendar of the signed-in user's on-call rotation, mirroring the Android
/// `ScheduleScreen`. On-call days are dotted in the user's avatar color; the month's shifts are listed
/// below. Shifts come resolved from the backend.
struct ScheduleView: View {
    @StateObject private var vm: ScheduleViewModel
    @Environment(\.colorScheme) private var scheme

    private let cal = Calendar.current

    init(api: PiroApiClient) {
        _vm = StateObject(wrappedValue: ScheduleViewModel(api: api))
    }

    var body: some View {
        PiroScreen(title: "Schedule") {
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    monthHeader
                    weekdayRow
                    calendarGrid
                    shiftSection
                }
                .padding(.horizontal, 20)
            }
        }
        .task { await vm.load() }
    }

    private var monthHeader: some View {
        HStack {
            Text(monthTitle)
                .font(.title2.bold())
                .foregroundStyle(PiroColors.onSurface(scheme))
            Spacer()
            Button { vm.previousMonth() } label: {
                Image(systemName: "chevron.left").foregroundStyle(PiroColors.muted(scheme))
            }
            Button { vm.nextMonth() } label: {
                Image(systemName: "chevron.right").foregroundStyle(PiroColors.muted(scheme))
            }
        }
        .padding(.vertical, 8)
    }

    private var weekdayRow: some View {
        HStack(spacing: 0) {
            ForEach(mondayFirstSymbols, id: \.self) { s in
                Text(s)
                    .font(.caption)
                    .foregroundStyle(PiroColors.muted(scheme))
                    .frame(maxWidth: .infinity)
            }
        }
    }

    private var calendarGrid: some View {
        let columns = Array(repeating: GridItem(.flexible(), spacing: 0), count: 7)
        return LazyVGrid(columns: columns, spacing: 6) {
            // Single ForEach over typed cells with disjoint stable IDs. Two sibling ForEachs — one over
            // 0..<leadingBlanks and one over 1...daysInMonth, both keyed by `\.self` — collide on the
            // integers they share (1,2,3…), so LazyVGrid drops the leading day cells when the blank count
            // shrinks between months. Prefixed String IDs keep the identity spaces from ever overlapping.
            ForEach(gridCells) { cell in
                switch cell {
                case .blank: Color.clear.frame(height: 44)
                case .day(let day): dayCell(day)
                }
            }
        }
        .padding(.top, 4)
    }

    private enum CalendarCell: Identifiable {
        case blank(Int)
        case day(Int)
        var id: String {
            switch self {
            case .blank(let i): return "blank-\(i)"
            case .day(let d): return "day-\(d)"
            }
        }
    }

    private var gridCells: [CalendarCell] {
        (0..<leadingBlanks).map { CalendarCell.blank($0) }
            + (1...daysInMonth).map { CalendarCell.day($0) }
    }

    private func dayCell(_ day: Int) -> some View {
        let date = dateFor(day)
        let isToday = cal.isDateInToday(date)
        let color = vm.onCallDays[cal.startOfDay(for: date)]
        return VStack(spacing: 4) {
            Text("\(day)")
                .font(.callout)
                .fontWeight(isToday ? .bold : .regular)
                .foregroundStyle(isToday ? PiroColors.brand : PiroColors.onSurface(scheme))
                .frame(width: 30, height: 30)
            Circle()
                .fill(color != nil ? Color.fromHex(color) : Color.clear)
                .frame(width: 6, height: 6)
        }
        .frame(height: 44)
    }

    @ViewBuilder private var shiftSection: some View {
        if vm.loading {
            SkeletonList(count: 5).padding(.top, 16)
        } else if let error = vm.error {
            Text(error).foregroundStyle(PiroColors.down).padding(.top, 24)
        } else if vm.slots.isEmpty {
            Text("No on-call shifts this month.")
                .foregroundStyle(PiroColors.muted(scheme))
                .padding(.top, 24)
        } else {
            VStack(spacing: 8) {
                ForEach(Array(vm.slots.enumerated()), id: \.offset) { _, slot in
                    ShiftRow(slot: slot)
                }
            }
            .padding(.top, 16)
        }
    }

    // MARK: - Date helpers

    private var monthTitle: String {
        let f = DateFormatter()
        f.dateFormat = "LLLL yyyy"
        return f.string(from: vm.monthAnchor).capitalized
    }

    private var daysInMonth: Int {
        cal.range(of: .day, in: .month, for: vm.monthAnchor)?.count ?? 30
    }

    private func firstOfMonth() -> Date {
        cal.date(from: cal.dateComponents([.year, .month], from: vm.monthAnchor)) ?? vm.monthAnchor
    }

    private func dateFor(_ day: Int) -> Date {
        cal.date(byAdding: .day, value: day - 1, to: firstOfMonth()) ?? firstOfMonth()
    }

    /// Leading blank cells so day 1 sits under its weekday, Monday-first (Piro web's M-T-W-T-F-S-S).
    private var leadingBlanks: Int {
        let weekday = cal.component(.weekday, from: firstOfMonth()) // 1 = Sunday
        return (weekday + 5) % 7
    }

    private var mondayFirstSymbols: [String] {
        // Narrow symbols, rotated to Monday-first regardless of locale's first weekday.
        let s = cal.veryShortStandaloneWeekdaySymbols // [Sun..Sat]
        return Array(s[1...6]) + [s[0]]
    }
}

private struct ShiftRow: View {
    let slot: OnCallSlot
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        HStack(spacing: 12) {
            Circle().fill(Color.fromHex(slot.userColor)).frame(width: 10, height: 10)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.callout.weight(.medium))
                    .foregroundStyle(PiroColors.onSurface(scheme))
                Text(range)
                    .font(.caption)
                    .foregroundStyle(PiroColors.muted(scheme))
            }
            Spacer()
            if let name = slot.scheduleName, !name.isEmpty {
                Text(name).font(.caption2).foregroundStyle(PiroColors.muted(scheme))
            }
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassCard(cornerRadius: 10)
    }

    private var title: String {
        let base = slot.userName.isEmpty ? "On-call" : slot.userName
        return slot.isOverride ? "\(base) (override)" : base
    }

    private var range: String {
        let iso = ISO8601DateFormatter(); iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let plain = ISO8601DateFormatter()
        func parse(_ s: String) -> Date? { iso.date(from: s) ?? plain.date(from: s) }

        // All-day shifts read as a date (or span) + "All day", not a drifting time range.
        if slot.isAllDay {
            let dayFmt = DateFormatter(); dayFmt.dateFormat = "MMM d"
            guard let start = parse(slot.startsAt) else { return "All day" }
            let cal = Calendar.current
            // The end is exclusive local midnight; the last covered day is the day before it. Only render a
            // span when the last day is genuinely after the start — never a backwards "Jul 31 – Jul 30".
            let lastDay = parse(slot.endsAt).flatMap { cal.date(byAdding: .day, value: -1, to: $0) }
            if let last = lastDay, last > start, !cal.isDate(last, inSameDayAs: start) {
                return "\(dayFmt.string(from: start)) – \(dayFmt.string(from: last)) · All day"
            }
            return "\(dayFmt.string(from: start)) · All day"
        }

        let out = DateFormatter(); out.dateFormat = "MMM d, HH:mm"
        func fmt(_ s: String) -> String {
            guard let d = parse(s) else { return s }
            return out.string(from: d)
        }
        return "\(fmt(slot.startsAt)) → \(fmt(slot.endsAt))"
    }
}
