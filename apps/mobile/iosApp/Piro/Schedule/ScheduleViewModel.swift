import Foundation
import Shared

/// Loads the signed-in user's resolved on-call shifts for the visible month and projects them onto local
/// calendar days — the SwiftUI counterpart of the Android `ScheduleViewModel`. Data comes resolved from
/// the backend (`getMyOnCallSlots`); no client-side RRULE math.
@MainActor
final class ScheduleViewModel: ObservableObject {
    @Published var monthAnchor = Date()          // any date within the visible month
    @Published var loading = true
    @Published var error: String?
    @Published var slots: [OnCallSlot] = []
    /// Local day (start-of-day) → the on-call user's avatar hex, for the calendar dots.
    @Published var onCallDays: [Date: String] = [:]

    private let api: PiroApiClient
    private let cal = Calendar.current

    init(api: PiroApiClient) {
        self.api = api
    }

    func previousMonth() {
        monthAnchor = cal.date(byAdding: .month, value: -1, to: monthAnchor) ?? monthAnchor
        Task { await load() }
    }

    func nextMonth() {
        monthAnchor = cal.date(byAdding: .month, value: 1, to: monthAnchor) ?? monthAnchor
        Task { await load() }
    }

    func load() async {
        loading = true
        error = nil
        guard let interval = cal.dateInterval(of: .month, for: monthAnchor) else { loading = false; return }
        // Pad a day on each side so shifts straddling the month boundary come back.
        let from = cal.date(byAdding: .day, value: -1, to: interval.start) ?? interval.start
        let to = cal.date(byAdding: .day, value: 1, to: interval.end) ?? interval.end
        let iso = ISO8601DateFormatter()
        do {
            let result = try await api.getMyOnCallSlots(from: iso.string(from: from), to: iso.string(from: to))
            slots = result
            onCallDays = projectDays(result)
        } catch {
            self.error = PiroError.message(error, networkFallback: "Could not load the schedule.")
        }
        loading = false
    }

    /// Marks each local date in the visible month the user is on call, keyed to their avatar color.
    private func projectDays(_ slots: [OnCallSlot]) -> [Date: String] {
        guard let month = cal.dateInterval(of: .month, for: monthAnchor) else { return [:] }
        let iso = ISO8601DateFormatter()
        iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let isoPlain = ISO8601DateFormatter()

        var days: [Date: String] = [:]
        for slot in slots {
            let start = iso.date(from: slot.startsAt) ?? isoPlain.date(from: slot.startsAt)
            let end = iso.date(from: slot.endsAt) ?? isoPlain.date(from: slot.endsAt)
            guard let start, let end else { continue }
            var day = cal.startOfDay(for: start)
            let last = cal.startOfDay(for: end)
            while day <= last {
                if month.contains(day) { days[day] = slot.userColor.isEmpty ? "#3D96FE" : slot.userColor }
                guard let next = cal.date(byAdding: .day, value: 1, to: day) else { break }
                day = next
            }
        }
        return days
    }
}
