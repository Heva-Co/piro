import Foundation
import Shared

/// Drives the login screen and the signed-in/out state — the SwiftUI counterpart of the Android
/// `LoginViewModel`, extended for self-hosting: the user also enters their Piro **server URL**, which is
/// persisted and used to build the API client. On launch it restores an existing Keychain session
/// (validated with `/me`), loads that server's SSO configuration, signs in by email/password or
/// completes an SSO callback, and on success arms push so the backend can page this device.
@MainActor
final class SessionViewModel: ObservableObject {
    @Published var serverURL = ""
    @Published var email = ""
    @Published var password = ""
    @Published var isSubmitting = false
    @Published var error: String?
    @Published var ssoOnly = false
    @Published var providers: [OidcProvider] = []
    @Published var signedIn = false

    private let services: ServiceLocator
    private let push: PushManager

    // Always read through the locator so we use the client for the currently-selected server.
    private var api: PiroApiClient { services.api }
    private var tokens: TokenStorage { services.tokenStorage }

    init(services: ServiceLocator, push: PushManager) {
        self.services = services
        self.push = push
        self.serverURL = services.baseURL ?? AppConfig.defaultServerURL
    }

    func onAppear() {
        restoreSession()
        loadSsoConfig()
    }

    /// If a server and token are already stored, validate the token with `/me` and go straight in —
    /// reopening the app shouldn't force a re-login. A failed/expired token falls through to login.
    private func restoreSession() {
        guard services.baseURL != nil, tokens.accessToken != nil else { return }
        isSubmitting = true
        Task {
            do {
                let profile = try await api.me()
                email = profile.email
                isSubmitting = false
                signedIn = true
                push.onSignedIn()
            } catch {
                isSubmitting = false
                // Do NOT clear the token on a transient/network error — a backend blip must not log the
                // user out. The client already clears tokens on a genuine auth failure (refresh 401),
                // so if the token survived, keep the session and enter optimistically; the first real
                // API call re-authenticates if the token is actually invalid.
                if tokens.accessToken != nil {
                    signedIn = true
                    push.onSignedIn()
                }
            }
        }
    }

    /// Applies the entered server URL (normalizing + persisting) and reloads that server's SSO config.
    /// Called when the server field is committed, so SSO buttons reflect the chosen server before
    /// signing in. Returns the normalized URL, or `nil` if the input wasn't a valid URL.
    @discardableResult
    func applyServer() -> String? {
        guard let base = ServerStore.normalize(serverURL) else {
            error = "Enter a valid server URL (e.g. https://piro.example.com)."
            return nil
        }
        if base != services.baseURL {
            services.updateBaseURL(base)
            ssoOnly = false
            providers = []
        }
        serverURL = base
        error = nil
        loadSsoConfig()
        return base
    }

    private func loadSsoConfig() {
        guard services.baseURL != nil else { return }
        Task {
            // A missing SSO endpoint is non-fatal: fall back to password login.
            if let mode = try? await api.getSsoMode() { ssoOnly = mode.ssoOnly }
            if let list = try? await api.getOidcProviders() { providers = list }
        }
    }

    func signIn() {
        guard !isSubmitting else { return }
        guard let _ = applyServer() else { return }
        let e = email.trimmingCharacters(in: .whitespaces)
        guard !e.isEmpty, !password.isEmpty else {
            error = "Enter your email and password."
            return
        }
        isSubmitting = true
        error = nil
        Task {
            do {
                _ = try await api.signIn(email: e, password: password)
                await afterSignedIn()
            } catch {
                isSubmitting = false
                self.error = PiroError.message(error, networkFallback: "Could not reach the server.")
            }
        }
    }

    /// Called when the SSO browser round-trip delivers a code+state (see `SSOAuthenticator`).
    func completeSso(code: String, state: String) {
        isSubmitting = true
        error = nil
        Task {
            do {
                _ = try await api.completeOidcCallback(code: code, state: state)
                await afterSignedIn()
            } catch {
                isSubmitting = false
                self.error = "SSO sign-in failed."
            }
        }
    }

    func oidcStartURL(_ provider: OidcProvider) -> URL? {
        URL(string: api.oidcStartUrl(providerId: provider.id))
    }

    private func afterSignedIn() async {
        isSubmitting = false
        signedIn = true
        password = ""
        push.onSignedIn()
    }

    func signOut() {
        Task {
            _ = try? await api.signOut()
            push.onSignedOut()
            email = ""
            password = ""
            error = nil
            signedIn = false
            // Keep the server URL so the next login targets the same host; just reload its SSO config.
            loadSsoConfig()
        }
    }
}
