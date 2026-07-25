import Foundation

/// Persists the Piro API base URL the user points the app at. Piro is self-hosted, so there is no single
/// server — each user enters their own on the login screen, and it's remembered across launches. Stored
/// in `UserDefaults` (it's configuration, not a secret; tokens live in the Keychain).
final class ServerStore {
    private let defaults: UserDefaults
    private let key = "piro.api.baseURL"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    var baseURL: String? {
        get { defaults.string(forKey: key) }
        set { defaults.set(newValue, forKey: key) }
    }

    /// Normalizes user input into a usable base URL: trims whitespace, defaults a missing scheme to
    /// `https://`, and strips trailing slashes. Returns `nil` if the result isn't a valid http(s) URL
    /// with a host, so the UI can prompt for a correction.
    static func normalize(_ raw: String) -> String? {
        var s = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !s.isEmpty else { return nil }
        let lower = s.lowercased()
        if !lower.hasPrefix("http://") && !lower.hasPrefix("https://") {
            s = "https://" + s
        }
        while s.hasSuffix("/") { s.removeLast() }
        guard let url = URL(string: s), let host = url.host, !host.isEmpty else { return nil }
        return s
    }
}
