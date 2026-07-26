import Foundation

/// The bottom-tab destinations, mirroring the Android `MainTab`. On-call and Alerts are real; Schedule is
/// a placeholder; Profile hosts the user's account, avatar, notification preferences and sign-out.
enum MainTab: Hashable, CaseIterable {
    case onCall, alerts, schedule, profile

    var title: String {
        switch self {
        case .onCall: return "On-call"
        case .alerts: return "Alerts"
        case .schedule: return "Schedule"
        case .profile: return "Profile"
        }
    }

    /// SF Symbol for the tab. Chosen to match the Android outlined icon set.
    var systemImage: String {
        switch self {
        case .onCall: return "shield"
        case .alerts: return "bell.badge"
        case .schedule: return "calendar"
        case .profile: return "person.crop.circle"
        }
    }
}
