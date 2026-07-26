import Foundation
import Shared

/// Minimal hand-rolled dependency container — the mirror of the Android app's `ServiceLocator`. Holds
/// the single Keychain-backed `TokenStorage` and the shared `PiroApiClient`.
///
/// Because Piro is self-hosted, the API base URL is chosen by the user at login rather than baked in, so
/// the client is rebuilt whenever the server changes (`updateBaseURL`). The token storage is stable
/// across rebuilds, so a stored session survives pointing the app at the same server again.
final class ServiceLocator {
    let tokenStorage: TokenStorage
    let serverStore = ServerStore()
    private(set) var api: PiroApiClient

    init() {
        let storage = KeychainTokenStorage()
        tokenStorage = storage
        api = PiroApiClient(baseUrl: serverStore.baseURL ?? "", tokens: storage)
    }

    /// The saved server URL, or `nil` if the user hasn't configured one yet.
    var baseURL: String? { serverStore.baseURL }

    /// Points the app at [normalized] (assumed already normalized), persisting it and rebuilding the
    /// client so every subsequent call targets the new server.
    func updateBaseURL(_ normalized: String) {
        serverStore.baseURL = normalized
        api = PiroApiClient(baseUrl: normalized, tokens: tokenStorage)
    }
}
