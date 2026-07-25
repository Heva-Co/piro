import Foundation

/// Build-time configuration. Piro is self-hosted, so the API base URL is entered by the user on the
/// login screen and persisted (see `ServerStore`), not baked in. This only provides a convenience
/// prefill for local development — the field starts empty in release builds so each self-hosted user
/// types their own server.
enum AppConfig {
    static var defaultServerURL: String {
        #if DEBUG
        return "http://localhost:5117"
        #else
        return ""
        #endif
    }
}
