import Foundation
import Shared

/// Loads and edits the signed-in user's profile: display name and avatar color (persisted via
/// `PUT /api/v1/auth/me`), plus their notification-delivery preferences (read-only for now).
@MainActor
final class ProfileViewModel: ObservableObject {
    @Published private(set) var profile: UserProfile?
    @Published private(set) var preferences: [UserNotificationPreferenceDto] = []
    @Published var loading = true
    @Published var error: String?

    // Editable fields, seeded from the loaded profile.
    @Published var name = ""
    @Published var colorHex = ""
    @Published var saving = false

    private let api: PiroApiClient

    init(api: PiroApiClient) {
        self.api = api
    }

    func load() async {
        loading = true
        error = nil
        do {
            let profile = try await api.me()
            apply(profile)
            // Preferences are best-effort — a user with none, or a permissions gap, shouldn't blank the page.
            preferences = (try? await api.getNotificationPreferences(userId: profile.id)) ?? []
        } catch {
            self.error = "Could not load your profile."
        }
        loading = false
    }

    var isDirty: Bool {
        guard let profile else { return false }
        return name != profile.name || colorHex != profile.color
    }

    func save() async {
        guard let profile, isDirty, !saving else { return }
        saving = true
        error = nil
        do {
            let updated = try await api.updateProfile(name: name, color: colorHex, timeZone: profile.timeZone)
            apply(updated)
        } catch {
            self.error = PiroError.message(error, networkFallback: "Could not save your changes.")
        }
        saving = false
    }

    private func apply(_ profile: UserProfile) {
        self.profile = profile
        name = profile.name
        colorHex = profile.color
    }
}
